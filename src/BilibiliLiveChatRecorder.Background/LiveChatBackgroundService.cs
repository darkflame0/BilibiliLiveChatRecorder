using System.Collections.Concurrent;
using Darkflame.BilibiliLiveApi;
using Darkflame.BilibiliLiveChatRecorder.Background.Options;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Darkflame.BilibiliLiveChatRecorder.Options;
using Darkflame.BilibiliLiveChatRecorder.Transport;
using EasyNetQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Darkflame.BilibiliLiveChatRecorder.Background
{
    public class LiveChatBackgroundService : BackgroundService
    {
        private RoomOptions _roomOptions;
        private LiverOptions _liverOptions;
        private AutoKeepOptions _autoKeepOptions;
        private readonly IDistributedCache _cache;
        private readonly IBilibiliLiveApi _bilibiliLiveApi;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPersistenceService _persistenceService;
        private readonly IBus _bus;
        private readonly ILogger<LiveChatBackgroundService> _logger;
        private readonly IHostApplicationLifetime _lifetime;
        private HashSet<int> _excludeRooms = new();
        private IEnumerable<(int RoomId, long Uid)> _specificRooms = Enumerable.Empty<(int RoomId, long Uid)>();
        private readonly CancellationTokenSource _cts = new();
        private readonly IOptionsMonitor<RoomOptions> _roomOptionsMonitor;
        public LiveChatBackgroundService(IOptionsMonitor<RoomOptions> roomOptions, IOptionsMonitor<LiverOptions> liverOptions, IOptionsMonitor<AutoKeepOptions> autoKeepOptions, IServiceProvider serviceProvider, IDistributedCache cache, IBilibiliLiveApi bilibiliLiveApi, IBus bus, ILogger<LiveChatBackgroundService> logger, IHostApplicationLifetime lifetime)
        {
            _roomOptions = roomOptions.CurrentValue;
            _liverOptions = liverOptions.CurrentValue;
            _autoKeepOptions = autoKeepOptions.CurrentValue;
            _cache = cache;
            _bilibiliLiveApi = bilibiliLiveApi;
            _serviceProvider = serviceProvider.CreateScope().ServiceProvider;
            _persistenceService = _serviceProvider.GetRequiredService<IPersistenceService>();
            _bus = bus;
            _logger = logger;
            _lifetime = lifetime;
            _roomOptionsMonitor = roomOptions;
            autoKeepOptions.OnChange(op =>
            {
                if (op.Enable && (op.Enable != _autoKeepOptions.Enable || op.Reload != _autoKeepOptions.Reload))
                    OnAutoKeepOptionsChange(op).GetAwaiter().GetResult();
                Interlocked.Exchange(ref _autoKeepOptions, op);
            });
            liverOptions.OnChange(op => { Interlocked.Exchange(ref _liverOptions, op); OnLiverOptionsChange(op).GetAwaiter().GetResult(); });
        }

        public ConcurrentDictionary<int, BilibiliLiveChatClient> RoomDic { get; } = new();

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() => _cts.Cancel());
            _logger.LogDebug("服务已启动");
            await OnRoomOptionsChange(_roomOptions);
            await OnLiverOptionsChange(_liverOptions);
            _ = _persistenceService.StartAsync();
            await Task.WhenAll(
                Loop(stoppingToken)
                );
        }
        async Task OnRoomOptionsChange(RoomOptions op)
        {
            await Task.CompletedTask;
            _excludeRooms = op.ExcludeRoom;
            if (RoomDic.Any())
            {
                foreach (var item in _excludeRooms)
                {
                    if (!op.SpecificRoom.Contains(item) && RoomDic.TryGetValue(item, out var client))
                    {
                        client.Dispose();
                    }
                }
                await AddKeepRooms(await _bilibiliLiveApi.GetLiverInfo(op.SpecificRoom));
            }
        }

        async Task OnLiverOptionsChange(LiverOptions op)
        {
            _specificRooms = op.Livers.Where(a => !a.Retire && !a.Keep).Select(a => (a.RoomId, a.Uid));
            if (RoomDic.Any())
            {
                var keepRooms = op.Livers.Where(a => a.Keep).Select(a => a.RoomId).Concat(_roomOptionsMonitor.CurrentValue.SpecificRoom).ToHashSet();
                await AddKeepRooms(await _bilibiliLiveApi.GetLiverInfo(keepRooms));
                foreach (var (_, item) in RoomDic)
                {
                    if (!keepRooms.Contains(item.RealRoomId))
                    {
                        item.PersistentKeep = false;
                    }
                }
            }
        }

        async Task OnAutoKeepOptionsChange(AutoKeepOptions op)
        {
            if (op.Enable)
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LiveChatDbContext>();
                if (await db.Database.CanConnectAsync())
                {
                    var exclude = _excludeRooms.Concat(_autoKeepOptions.Exclude);
                    var now = DateTime.Now;
                    var hourly = await db.HourlyData.Where(a => a.Time >= now.AddHours(-1).Date && a.Data.MaxPopularity >= op.NormalThreshold).Select(a => new { a.Time, a.Data.RoomId, a.Data.MaxPopularity }).ToListAsync();
                    var roomIds = (await db.DailyData.AsQueryable().Where(a => a.Date >= now.Subtract(op.LongActiveTime).Date)
                        .Where(a => a.Data.MaxPopularity >= op.LongThreshold).Select(a => a.RoomId).Distinct().Intersect(db.DailyData.AsQueryable().Where(a => a.Date >= now.Subtract(op.LongLastLiveTime).Date && a.Data.MaxPopularity != 0).Select(a => a.RoomId))
                        .ToListAsync())
                        .Union(hourly.Where(a => a.MaxPopularity >= op.LongThreshold).Select(a => a.RoomId)).ToHashSet();
                    roomIds.ExceptWith(exclude);
                    var livers = await _bilibiliLiveApi.GetLiverInfo(roomIds);
                    foreach (var item in livers)
                    {
                        if (!RoomDic.TryGetValue(item.RoomId, out var c))
                        {
                            c = AddRoom(item, getLiveInfo: false, keepType: KeepType.Long);
                        }
                        else
                        {
                            c!.AutoKeepType = KeepType.Long;
                        }
                    }
                    roomIds = (await db.DailyData.AsQueryable().Where(a => a.Date >= now.Subtract(op.NormalActiveTime).Date)
                        .Where(a => a.Data.MaxPopularity >= op.NormalThreshold).Select(a => a.RoomId).Distinct().Intersect(db.DailyData.AsQueryable().Where(a => a.Date >= now.Subtract(op.NormalLastLiveTime).Date && a.Data.MaxPopularity != 0).Select(a => a.RoomId))
                        .ToListAsync())
                        .Union(hourly.Where(a => a.MaxPopularity >= op.NormalThreshold).Select(a => a.RoomId)).ToHashSet();
                    roomIds.ExceptWith(exclude);
                    livers = await _bilibiliLiveApi.GetLiverInfo(roomIds);
                    foreach (var item in livers)
                    {
                        if (!RoomDic.TryGetValue(item.RoomId, out var c))
                        {
                            c = AddRoom(item, getLiveInfo: false, keepType: KeepType.Normal);
                        }
                        else if (c!.AutoKeepType < KeepType.Normal)
                        {
                            c!.AutoKeepType = KeepType.Normal;
                        }
                    }
                }
            }
        }

        private async Task Loop(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Loop已启动");
            try
            {
                await AddRooms((await _bilibiliLiveApi.GetLiveRoomByUid(
                    ((await _bilibiliLiveApi.GetVLiverRoomList()).Select(a => a.Liver.Uid)).Union(
                    _liverOptions.Livers.Where(a => !a.Retire).Select(a => a.Uid))
                    )).OrderByDescending(a => a.LiveInfo.Popularity));
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (await _cache.GetAsync<JToken>("last_rooms_is_not_V", stoppingToken) is { } notVrooms)
                        {
                            await AddRooms((await _bilibiliLiveApi.GetLiveRoomByUid(notVrooms.Select(a => a["uid"]!.Value<long>()))));
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "添加非虚拟区房间失败");
                    }
                }, stoppingToken);
                await AddKeepRooms(await _bilibiliLiveApi.GetLiverInfo(_liverOptions.Livers.Where(a => a.Keep).Select(a => a.RoomId).Concat(_roomOptions.SpecificRoom)));
#if !DEBUG
                await OnAutoKeepOptionsChange(_autoKeepOptions);
#endif
                var updateCoverAndKeyframeDelay = Task.Delay(TimeSpan.FromSeconds(_roomOptions.PullingKeyframeInterval), stoppingToken);
                Task delay;
                _roomOptionsMonitor.OnChange(op =>
                {
                    _logger.LogInformation("Configuration was reloaded");
                    if (op.PullingKeyframeInterval != _roomOptions.PullingKeyframeInterval)
                    {
                        updateCoverAndKeyframeDelay = Task.Delay(TimeSpan.FromSeconds(op.PullingKeyframeInterval), stoppingToken);
                    }
                    if (op.PullingRoomsInterval != _roomOptions.PullingRoomsInterval)
                    {
                        delay = Task.Delay(TimeSpan.FromSeconds(op.PullingRoomsInterval), stoppingToken);
                    }
                    Interlocked.Exchange(ref _roomOptions, op);
                    OnRoomOptionsChange(op).GetAwaiter().GetResult();
                });
                while (!stoppingToken.IsCancellationRequested)
                {
                    delay = Task.Delay(TimeSpan.FromSeconds(_roomOptions.PullingRoomsInterval), stoppingToken);
                    try
                    {
                        var roomList = Enumerable.Empty<(LiverInfo Liver, LiveInfo LiveInfo)>()
                        .Concat((await _bilibiliLiveApi.GetLiveRoomByUid(_specificRooms.Select(a => a.Uid))))
                        .Concat(updateCoverAndKeyframeDelay.IsCompleted ? (await _bilibiliLiveApi.GetVLiverRoomList()) : (await _bilibiliLiveApi.GetRecentlyVLiverRoomList()))
                        ;
                        await AddRooms(roomList);
                        if (updateCoverAndKeyframeDelay.IsCompleted)
                        {
                            _ = _cache.SetAsync("last_rooms_is_not_V", RoomDic.Select(a => a.Value).Where(a => a.Live && a.ParentArea != "虚拟主播").Select(a => new { roomId = a.RoomId, uid = a.UId }).ToList(), TimeSpan.FromHours(1), stoppingToken);
#if DEBUG
                            _logger.LogDebug($"RoomCount:{RoomDic.Count},Connected:{RoomDic.Where(a => a.Value.Connected).Count()}");
#endif 
                            var roomListDic = roomList.DistinctBy(a => a.Liver.RoomId).ToDictionary(a => a.Liver.RoomId);
                            updateCoverAndKeyframeDelay = Task.Delay(TimeSpan.FromSeconds(_roomOptions.PullingKeyframeInterval / 10), stoppingToken);
                            var liveInfos = await _bilibiliLiveApi.GetLiveInfo(RoomDic.Values.Where(a => a.Live && !roomListDic.ContainsKey(a.RealRoomId)).Select(a => a.RealRoomId));
                            updateCoverAndKeyframeDelay = Task.Delay(TimeSpan.FromSeconds(_roomOptions.PullingKeyframeInterval), stoppingToken);
                            foreach (var item in liveInfos.Concat(roomList.Select(a => (a.Liver.RoomId, a.LiveInfo))))
                            {
                                if (RoomDic.TryGetValue(item.RoomId, out var c))
                                {
                                    UpdateCoverAndKeyframe(c, item.LiveInfo);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.ToString());
                    }
                    finally
                    {
                        await delay;
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                _lifetime.StopApplication();
            }
            finally
            {
            }

        }
        private static void UpdateCoverAndKeyframe(BilibiliLiveChatClient c, LiveInfo liveInfo)
        {
            if (liveInfo.UserCover != null && (liveInfo.UserCover.PathAndQuery != c.UserCover?.PathAndQuery || liveInfo.Title != c.Title || (liveInfo.LiveTime != null && liveInfo.LiveTime != c.LiveTime)) && (c.LiveTime != null || liveInfo.LiveTime != null))
            {
                //应对某些Api没有liveTime
                liveInfo.LiveTime ??= c.LiveTime;
                c.UpdateLiveInfo(liveInfo);
            }
            else
            {
                c.UName = liveInfo.UName!;
                c.Keyframe = liveInfo.Keyframe;
                c.LiveStatus = liveInfo.Live;
            }
        }
        async Task AddRooms(IEnumerable<(LiverInfo Liver, LiveInfo LiveInfo)> list)
        {
            foreach (var item in list)
            {
                if (!_excludeRooms.Contains(item.Liver.RoomId))
                {
                    if (!RoomDic.TryGetValue(item.Liver.RoomId, out var c))
                    {
                        if (item.LiveInfo.LiveTime != null)
                        {
                            AddRoom(item.Liver, item.LiveInfo, getLiveInfo: false);
                        }
                        else
                        {
                            AddRoom(item.Liver, null, getLiveInfo: true);
                        }
                    }
                }
            }
            await Task.CompletedTask;
        }
        async Task AddKeepRooms(IEnumerable<LiverInfo> list)
        {
            foreach (var item in list)
            {
                if (!_excludeRooms.Contains(item.RoomId))
                {
                    if (!RoomDic.TryGetValue(item.RoomId, out var c))
                    {
                        AddRoom(item, null, false, true);
                    }
                    else
                    {
                        c.PersistentKeep = true;
                    }
                }
            }
            await Task.CompletedTask;
        }

        BilibiliLiveChatClient? AddRoom(LiverInfo liverInfo, LiveInfo? liveInfo = default, bool getLiveInfo = false, bool persist = false, KeepType keepType = KeepType.None)
        {
            BilibiliLiveChatClient c;
            if (liveInfo == null)
            {
                c = ActivatorUtilities.CreateInstance<BilibiliLiveChatClient>(_serviceProvider, liverInfo, getLiveInfo);
            }
            else
            {
                c = ActivatorUtilities.CreateInstance<BilibiliLiveChatClient>(_serviceProvider, liverInfo, liveInfo, getLiveInfo);
            }

            c.PersistentKeep = persist;
            c.AutoKeepType = keepType;
            if (RoomDic.TryAdd(c.RoomId, c))
            {
                _ = MessageLoop(c)
                .ConfigureAwait(false);
                return c;
            }
            return RoomDic.GetValueOrDefault(c.RoomId);
        }
        private async Task MessageLoop(BilibiliLiveChatClient client, CancellationToken cancellationToken = default)
        {
            try
            {
                await client.ConnectAsync();

                try
                {
                    await foreach (var msg in client.ReadAllAsync(cancellationToken))
                    {
                        await ProcessMessage(msg);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    client.Dispose();
                    if (client.TryReadAll(out var msgs))
                    {
                        foreach (var msg in msgs)
                        {
                            await ProcessMessage(msg);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"RoomId: {client.RoomId} Exception:{e}");
            }
            finally
            {
                RoomDic.Remove(client.RoomId, out _);
                _logger.LogDebug($"{client.RoomId} 已断开");
            }
            async Task ProcessMessage((DateTime Time, JToken Raw) msg)
            {
                var chat = new ChatMessage(client.RealRoomId, msg.Time, msg.Raw);
                await _persistenceService.SaveAsync(chat);
                if (_bus.Advanced.IsConnected)
                {
                    _ = _bus.PubSub.PublishAsync(chat, $"{chat.RoomId}.{chat.Raw["cmd"]}", CancellationToken.None);
                }
            }
        }
    }
}
