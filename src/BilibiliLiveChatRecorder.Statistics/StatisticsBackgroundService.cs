using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Darkflame.BilibiliLiveChatRecorder.Statistics.Models;
using EasyNetQ;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Darkflame.BilibiliLiveChatRecorder.Statistics
{
    public class StatisticsBackgroundService : BackgroundService
    {
        public class Context : IAsyncDisposable
        {
            static SemaphoreSlim DbSem = new SemaphoreSlim(1, 1);
            private readonly IHubContext<RoomHub, IRoomClient> _hubContext;
            private readonly IServiceProvider _service;
            private readonly ConcurrentDictionary<int, Context> _contextDic;
            private readonly ILogger<Context> _logger;
            private readonly Subject<RoomStatistic> _ob;
            private int? _firstFansCount;
            private readonly CompositeDisposable _disposables = new CompositeDisposable();
            private readonly Channel<ChatMessage> _channel = Channel.CreateBounded<ChatMessage>(new BoundedChannelOptions(64) { SingleReader = true, SingleWriter = true });
            private DateTime _lastHeartBeat;

            public Context(int roomId, IHubContext<RoomHub, IRoomClient> hubContext, IServiceProvider service, ConcurrentDictionary<int, Context> contextDic, ILogger<Context> logger)
            {
                RoomId = roomId;
                _hubContext = hubContext;
                _service = service;
                _contextDic = contextDic;
                _logger = logger;
                _ob = new Subject<RoomStatistic>();
                var ob = _ob.Throttle(TimeSpan.FromMilliseconds(400));
                _disposables.Add(_ob);
                _disposables.Add(ob.Subscribe(_ =>
                          _hubContext.Clients.Group(RoomId.ToString()).ReceiveRoomData(RoomId, Statistics)));
                _ = Loop();
            }
            public int RoomId { get; set; }
            public RoomData? Room { get; set; }
            public ConcurrentDictionary<long, DateTime> Participants10Min { get; set; } = new();
            public IDictionary<long, int> IncomeDic => Room?.GoldUser ?? new Dictionary<long, int>(0);

            public RoomStatistic Statistics { get; } = new RoomStatistic();
            public DateTime LastHeartBeat { get => _lastHeartBeat; set => _lastHeartBeat = value; }

            public async Task Push(ChatMessage message)
            {
                await _channel.Writer.WriteAsync(message);
            }
            public Task Disconnect()
            {
                return _hubContext.Clients.Group(RoomId.ToString()).Disconnect();
            }
            private async Task Loop(CancellationToken cancellationToken = default)
            {
                var reader = _channel.Reader;
                try
                {
                    await foreach (var msg in reader.ReadAllAsync(cancellationToken))
                    {
                        try
                        {
                            await ProcessMessage(msg);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"RoomId:{RoomId}:{e}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    _logger.LogError($"RoomId:{RoomId}:{e}");
                }
                finally
                {
                    _contextDic.TryRemove(RoomId, out _);
                }
            }
            async public ValueTask DisposeAsync()
            {
                _channel.Writer.Complete();
                _disposables.Dispose();
                await Disconnect();
            }
#nullable disable warnings
            private async Task ProcessMessage(ChatMessage msg)
            {
                if ((msg.Raw["cmd"].Value<string>() is (Cmd.Popularity or Cmd.LiveStart or Cmd.RoomInfo) && (msg.Time - LastHeartBeat) > RoomData.LiveInterval))
                {
                    var cts = new CancellationTokenSource(10000);
                    try
                    {
                        await DbSem.WaitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        while (_channel.Reader.TryRead(out _)) ;
                        return;
                    }
                    try
                    {
                        _firstFansCount = null;
                        using var scope = _service.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<LiveChatDbContext>();
                        Room = (await db.ChatMessage.GetLatest(msg.RoomId, since: msg.Time.Date.AddDays(-30))).FirstOrDefault();
                        if (Room == null || (msg.Time - Room.EndTime) > RoomData.LiveInterval)
                        {
                            Room = new RoomData() { StartTime = msg.Time, RoomId = msg.RoomId };
                            Participants10Min.Clear();
                        }
                        else
                        {
                            Room.Popularity = await db.ChatMessage.GetCore(Room.EndTime.AddMinutes(-5), Room.EndTime, Room.RoomId).Where(a => a.Raw["cmd"]!.Value<string>() == Cmd.Popularity).OrderByDescending(a => a.Time).Select(a => a.Raw["popularity"].Value<int>()).FirstOrDefaultAsync();
                            Room.GiftDanmakuUser.ExceptWith(Room.Participants);
                            Room.Viewer.UnionWith(Room.Participants);
                            Participants10Min = new ConcurrentDictionary<long, DateTime>(await db.ChatMessage.GetParticipant(Room.EndTime.AddMinutes(-10) > Room.StartTime ? Room.EndTime.AddMinutes(-10) : Room.StartTime, Room.EndTime.AddMilliseconds(1), msg.RoomId));
                        }
                        LastHeartBeat = Room.EndTime == default ? Room.StartTime : Room.EndTime;
                        Statistics.Set(Room);
                    }
                    finally
                    {
                        DbSem.Release();
                    }
                }
                if ((msg.Time - LastHeartBeat) > RoomData.LiveInterval * 2)
                {
                    await Disconnect();
                    return;
                }
                if (Room.EndTime >= msg.Time)
                {
                    return;
                }
                var cmd = msg.Raw["cmd"].Value<string>();
                if (cmd.StartsWith(Cmd.Danmaku))
                {
                    var uid = msg.Raw["info"][2][0].Value<long>();
                    if (msg.Raw["info"][0][9].Value<int>() > 0)
                    {
                        ++Room.GiftDanmaku;
                        if (!Room.Participants.Contains(uid))
                        {
                            Room.GiftDanmakuUser.Add(uid);
                        }
                    }
                    else
                    {
                        ++Room.RealDanmaku;
                        Room.GiftDanmakuUser.Remove(uid);
                        if (Room.RealDanmakuUser.ContainsKey(uid))
                        {
                            ++Room.RealDanmakuUser[uid];
                        }
                        else
                        {
                            Room.RealDanmakuUser[uid] = 1;
                        }
                        Room.Participants.Add(uid);
                        Room.Viewer.Add(uid);
                        Participants10Min[uid] = msg.Time;
                    }
                }
                else
                {
                    switch (cmd)
                    {
                        case Cmd.Popularity:
                            LastHeartBeat = msg.Time;
                            Room.Popularity = msg.Raw["popularity"].Value<int>();
                            if (Room.MaxPopularity < Room.Popularity)
                            {
                                Room.MaxPopularity = Room.Popularity.Value;
                            }
                            break;

                        case Cmd.RoomRealTimeMessageUpdate:
                            var fans = msg.Raw["data"]["fans"].Value<int>();
                            if (fans >= 0)
                            {
                                if (_firstFansCount == null)
                                {
                                    _firstFansCount = fans - Room.FansIncrement;
                                }
                                else
                                {
                                    Room.FansIncrement = fans - _firstFansCount.Value;
                                }
                            }
                            break;

                        case Cmd.RoomInfo:
                            LastHeartBeat = msg.Time;
                            Room.Title = msg.Raw["data"]["title"].Value<string>();
                            Room.Cover = msg.Raw["data"]["cover"].Value<string>();
                            Room.Area = msg.Raw["data"]["area"].Value<string>();
                            break;
                        case Cmd.SendGift:
                        case Cmd.USER_TOAST_MSG:
                        case Cmd.SUPER_CHAT_MESSAGE:
                            {
                                var uid = msg.Raw["data"]["uid"].Value<long>();
                                if (msg.Raw["data"]["coin_type"]?.Value<string>() == "silver")
                                {
                                    Room.SilverCoin += msg.Raw["data"]["total_coin"].Value<int>();
                                    Room.SilverUser.Add(uid);
                                }
                                else
                                {
                                    var gold = 0;
                                    switch (msg.Raw["cmd"].Value<string>())
                                    {
                                        case Cmd.SendGift:
                                            gold = msg.Raw["data"]["total_coin"].Value<int>();
                                            break;

                                        case Cmd.USER_TOAST_MSG:
                                            gold = (msg.Raw["data"]["price"].Value<int?>() ??
                            (msg.Raw["data"]["guard_level"].Value<int>() == 3 ? 198000 :
                            msg.Raw["data"]["guard_level"].Value<int>() == 2 ? 1980000 :
                            msg.Raw["data"]["guard_level"].Value<int>() == 1 ? 19800000 : 0) - (msg.Raw["data"]["op_type"].Value<int>() == 2 ? 40 * (int)Math.Pow(10, 4 - msg.Raw["data"]["guard_level"].Value<int>()) : 0));
                                            break;

                                        case Cmd.SUPER_CHAT_MESSAGE:
                                            gold = msg.Raw["data"]["price"].Value<int>() * 1000;
                                            break;
                                        default:
                                            break;
                                    }
                                    Room.GoldCoin += gold;
                                    if (Room.GoldUser.TryGetValue(uid, out var _))
                                    {
                                        Room.GoldUser[uid] += gold;
                                    }
                                    else
                                    {
                                        Room.GoldUser[uid] = gold;
                                    }
                                }
                                Room.GiftDanmakuUser.Remove(uid);
                                Room.Participants.Add(uid);
                                Room.Viewer.Add(uid);
                                Participants10Min[uid] = msg.Time;
                                break;
                            }
                        case Cmd.INTERACT_WORD:
                            {
                                var uid = msg.Raw["data"]["uid"].Value<long>();
                                Room.Viewer.Add(uid);
                                break;
                            }
                        case Cmd.LiveEnd:
                            {
                                Room.Popularity = 1;
                                break;
                            }
                        default:
                            break;
                    }
                }
                Room.EndTime = msg.Time;
                _ob.OnNext(Statistics.Set(Room));
            }
        }
#nullable restore
        private readonly IServiceProvider _serviceProvider;
        private readonly IBus _bus;
        private readonly ILogger<StatisticsBackgroundService> _logger;

        public StatisticsBackgroundService(IServiceProvider serviceProvider, IBus bus, ILogger<StatisticsBackgroundService> logger)
        {
            _serviceProvider = serviceProvider.CreateScope().ServiceProvider;
            _logger = logger;
            _bus = bus;
            _cmds = new HashSet<string>(RoomData.Cmds.Append(Cmd.INTERACT_WORD));
        }
        public ConcurrentDictionary<int, Context> ContextDic { get; private set; } = new();
        private HashSet<string> _cmds = new HashSet<string>();

        public IDictionary<int, int> GetParticipantDuring10MinDic()
        {
            return ContextDic.ToDictionary(a => a.Key, a => a.Value.Participants10Min.Count);
        }
        public bool TryGetRoomContext(int roomId, out Context? context)
        {
            if (ContextDic.TryGetValue(roomId, out context))
            {
                return true;
            }
            return false;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var sub = _bus.PubSub.Subscribe<ChatMessage>(nameof(StatisticsBackgroundService) + "_" + Guid.NewGuid().ToString()[..8]
                , (msg) =>
             {
                 var cmd = msg.Raw["cmd"]?.Value<string>() ?? "";
                 if (_cmds.Contains(cmd) || cmd.StartsWith(Cmd.Danmaku))
                 {
                     var context = ContextDic.GetOrAdd(msg.RoomId, (_) => ActivatorUtilities.CreateInstance<Context>(_serviceProvider, msg.RoomId, ContextDic));
                     try
                     {
                         context.Push(msg).GetAwaiter().GetResult();
                     }
                     catch (ChannelClosedException)
                     {
                     }
                 }
             }, op => op
             .WithExpires(5000)
             .AsExclusive().WithDurable(false));
            await Task.WhenAll(CleanLoop(stoppingToken), P10MinCleanLoop(stoppingToken));
        }
        async Task P10MinCleanLoop(CancellationToken cancellationToken)
        {

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    var now = DateTime.Now;
                    foreach (var (_, context) in ContextDic)
                    {
                        foreach (var item in context.Participants10Min.Where(a => (now - a.Value).TotalMinutes > 10))
                        {
                            context.Participants10Min.TryRemove(item.Key, out var _);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e.ToString());
                }
            }
        }
        async Task CleanLoop(CancellationToken cancellationToken)
        {

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
                    var now = DateTime.Now;
                    foreach (var (_, context) in ContextDic)
                    {
                        if (context.Room == null)
                        {
                            continue;
                        }
                        if ((now - context.Room.EndTime) > TimeSpan.FromDays(1))
                        {
                            await context.DisposeAsync();
                            continue;
                        }
                        if ((now - context.Room.EndTime) > RoomData.LiveInterval)
                        {
                            context.Room = new RoomData();
                            await context.Disconnect();
                            continue;
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e.ToString());
                }
            }
        }
    }
}
