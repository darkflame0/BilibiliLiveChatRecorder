using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Xml.Linq;
using Darkflame.BilibiliLiveApi;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using Darkflame.BilibiliLiveChatRecorder.Transport.Internal;

namespace Darkflame.BilibiliLiveChatRecorder.Transport
{
    public class BilibiliLiveChatClient : IDisposable
    {
        static readonly SemaphoreSlim ConnectSem = new SemaphoreSlim(Math.Max(16, Environment.ProcessorCount * 2));
        static readonly PrioritySemaphoreSlim<int> ReconnetSem = new(Math.Max(16, Environment.ProcessorCount * 2));
        const string DefaultHost = "broadcastlv.chat.bilibili.com";
        const int DefaultPort = 2244;
        private int DisconnectDelay
        {
            get
            {
                if (_liveInfo.Live || PersistentKeep)
                {
                    return Timeout.Infinite;
                }
                return AutoKeepType switch
                {
                    //KeepType.Short => _autoKeepOptions.ShortDelay,
                    KeepType.Normal => (int)_autoKeepOptions.NormalDelay.TotalSeconds / _liveOptions.HeartbeatInterval,
                    KeepType.Long => (int)_autoKeepOptions.LongDelay.TotalSeconds / _liveOptions.HeartbeatInterval,
                    KeepType.None or _ => (int)_disconnectTimeout.TotalSeconds / _liveOptions.HeartbeatInterval
                };
            }
        }
        private readonly ILogger<BilibiliLiveChatClient> _logger;
        private readonly IBilibiliLiveApi _api;
        private readonly IDistributedCache _cache;
        private AutoKeepOptions _autoKeepOptions;
        private LiveOptions _liveOptions;

        private HashSet<string> IgnoreCmds => _liveOptions.IgnoreCmds;
        private Channel<(DateTime Time, JToken Raw)> _q = default!;
        private IConnection _connection = default!;
        private readonly LiveInfo? _initialLiveInfo;
        private LiveInfo _liveInfo = new LiveInfo();
        private volatile bool _alive = false;
        private int _popularity = 1;
        volatile int _liveEndCount = 0;
        volatile int _disconnectCount = -1;
        private bool _persistentKeep;
        private readonly CancellationTokenSource _disconnectCts = new();
        private TimeSpan _disconnectTimeout => _liveOptions.OfflineTimeount;
        KeepType _autoKeepType;
        readonly SemaphoreSlim _getLiveInfoSem = new SemaphoreSlim(0, 1);
        readonly List<IDisposable> _disposables = new List<IDisposable>();
        public BilibiliLiveChatClient(ILogger<BilibiliLiveChatClient> logger, IBilibiliLiveApi api, IDistributedCache cache, IOptionsMonitor<LiveOptions> msgOptions, IOptionsMonitor<AutoKeepOptions> autoKeepOptions, LiverInfo liverInfo, LiveInfo? liveInfo = null, bool getLiveInfo = false) : this(logger, api, cache, msgOptions, autoKeepOptions, liverInfo.RoomId, getLiveInfo)
        {
            LiverInfo = liverInfo;
            _initialLiveInfo = liveInfo;
        }
        public BilibiliLiveChatClient(ILogger<BilibiliLiveChatClient> logger, IBilibiliLiveApi api, IDistributedCache cache, IOptionsMonitor<LiveOptions> msgOptions, IOptionsMonitor<AutoKeepOptions> autoKeepOptions, LiverInfo liverInfo, bool getLiveInfo = false) : this(logger, api, cache, msgOptions, autoKeepOptions, liverInfo, null, getLiveInfo)
        {
        }
        public BilibiliLiveChatClient(ILogger<BilibiliLiveChatClient> logger, IBilibiliLiveApi api, IDistributedCache cache, IOptionsMonitor<LiveOptions> msgOptions, IOptionsMonitor<AutoKeepOptions> autoKeepOptions, int roomId, bool getLiveInfo = false)
        {
            RoomId = roomId;
            _logger = logger;
            _api = api;
            _cache = cache;
            _autoKeepOptions = autoKeepOptions.CurrentValue;
            _liveOptions = msgOptions.CurrentValue;
            _disposables.Add(autoKeepOptions.OnChange(op => Interlocked.Exchange(ref _autoKeepOptions, op)));
            _disposables.Add(msgOptions.OnChange(op => Interlocked.Exchange(ref _liveOptions, op)));
            if (getLiveInfo)
            {
                _getLiveInfoSem.TryRelease();
            }
        }
        public int RoomId { get; private set; }

        int _inited;

        public string Host { get; private set; } = "";
        public string ParentArea => LiveInfo.ParentArea;
        public string Area => LiveInfo.Area;

        public string Title => LiveInfo.Title;

        public Uri? UserCover => LiveInfo.UserCover;

        public string Keyframe { get => LiveInfo.Keyframe; set => _liveInfo.Keyframe = value; }

        public bool Live => _liveInfo.Live || Stay;
        public bool LiveStatus { get => LiveInfo.Live; set { if (_liveInfo.Live != value) { _liveInfo.Live = value; ResetDisconnectTimer(); } } }
        private bool Stay => _liveEndCount > 0;
        public DateTime? LiveTime => LiveInfo.LiveTime;

        public string UName
        {
            get => LiverInfo?.Name ?? "";
            set
            {
                if (LiverInfo != null)
                {
                    LiverInfo.Name = value;
                }
            }
        }


        public long UId => LiverInfo?.Uid ?? 0;

        public LiverInfo? LiverInfo { get; set; }

        private int _samePopularityCount;

        public int Popularity { get => _liveInfo.Live ? _popularity : 0; }
        public int RealRoomId => LiverInfo?.RoomId ?? 0;

        public int ShortId => LiverInfo?.ShortId ?? 0;

        public bool Connected => _connection?.Connected ?? false;

        public LiveInfo LiveInfo { get => _liveInfo; private set => _liveInfo = value; }
        public bool PersistentKeep
        {
            get => _persistentKeep; set
            {
                if (value != _persistentKeep)
                {
                    _persistentKeep = value;
                    ResetDisconnectTimer();
                }
            }
        }
        public bool AutoKeep => _autoKeepOptions.Enable && !_autoKeepOptions.Exclude.Contains(RealRoomId);
        public int AutoKeepThresholdPop => _autoKeepOptions.LongThreshold;
        private bool Keeping => PersistentKeep || (AutoKeep && _autoKeepType != KeepType.None);

        public KeepType AutoKeepType
        {
            get => AutoKeep ? _autoKeepType : KeepType.None; set
            {
                if (_autoKeepType != value)
                {
                    _autoKeepType = value;
                    ResetDisconnectTimer();
                }
            }
        }
        private (string host, int port, int wsPort) SelectServer(IEnumerable<(string Host, int WsPort, int wsPort)> servers, bool prefer)
        {
            if (prefer)
            {
                foreach (var serverLocation in _liveOptions.ServerLocation)
                {
                    if (servers.FirstOrDefault(s => s.Host.StartsWith(serverLocation)) is { Host: not null } server)
                    {
                        return server;
                    }
                }
            }
            return servers.First();
        }
        private async Task<(string Host, int WsPort, string Token)> GetHostAndToken(bool prefer = true)
        {
            try
            {
                var (servers, token) = await _api.GetRoomServerConf(RoomId);
                if (servers.Any())
                {
                    var (host, _, wsPort) = SelectServer(servers, prefer);
                    return (host, wsPort, token);
                }
                return (DefaultHost, DefaultPort, token);
            }
            catch (Exception e)
            {
                LogError(e);
                return (DefaultHost, DefaultPort, "");
            }

        }
        private void ResetDisconnectTimer()
        {
            if (_liveInfo.Live)
            {
                _liveEndCount = _liveOptions.StayTimeout / _liveOptions.HeartbeatInterval;
            }
            _disconnectCount = DisconnectDelay;
        }
        public async Task ConnectAsync()
        {
            try
            {
                await ConnectCoreAsync();
            }
            catch (Exception e)
            {
                LogError(e, Host);
                await Reconnect();
            }
        }
        private async Task ConnectCoreAsync(bool prefer = true)
        {
            if (LiverInfo == default)
            {
                LiverInfo = await _api.GetLiverInfo(RoomId);
            }
            await ConnectSem.WaitAsync();
            string token;
            try
            {
                int wsPort;
                string host;
                (host, wsPort, token) = await GetHostAndToken(prefer);
                Host = host;
                _connection = new WebSocketConnection();
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _connection.ConnectAsync($"{(_liveOptions.Wss ? "wss" : "ws")}://{host}/sub", _liveOptions.Wss ? 443 : wsPort, cts.Token);
            }
            finally
            {
                ConnectSem.Release();
            }
            var q = Interlocked.Exchange(ref _q, Channel.CreateUnbounded<(DateTime Time, JToken Raw)>(new UnboundedChannelOptions() { SingleReader = true }));
            q?.Writer.TryComplete();
            if (Interlocked.Exchange(ref _inited, 1) == 0)
            {
                if (_initialLiveInfo != null || _getLiveInfoSem.CurrentCount != 0)
                {
                    _liveInfo.Live = true;
                    ResetDisconnectTimer();
                }
                _ = RoomInfoUpdateLoop(_disconnectCts.Token);
            }
            await SendJoinMessage(token);
            _ = RecvLoop(_disconnectCts.Token);
        }
        public bool TryReadAll(out IEnumerable<(DateTime Time, JToken Raw)> data)
        {
            data = Enumerable.Empty<(DateTime Time, JToken Raw)>();
            var reader = _q.Reader;
            while (reader.TryRead(out var item))
            {
                data = data.Append(item);
            }
            return data.Any();
        }
        public async IAsyncEnumerable<(DateTime Time, JToken Raw)> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
retry:
            var reader = _q.Reader;
            var writer = _q.Writer;
            using (_disconnectCts.Token.Register(() => writer.TryComplete()))
            {
                while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var item))
                    {
                        yield return item;
                    }
                }
            }
            if (!_disconnectCts.IsCancellationRequested)
            {
                goto retry;
            }
        }

        static (IMemoryOwner<byte> Owner, int Length) Inflate(ReadOnlyMemory<byte> source)
        {
            MemoryMarshal.TryGetArray(source.Slice(2, source.Length - 6), out var segment);
            using var memoryStream = new MemoryStream(segment.Array!, segment.Offset, segment.Count);
            using var decompressionStream = new DeflateStream(memoryStream, CompressionMode.Decompress);
            return DecompressCore(source, decompressionStream);
        }
        static (IMemoryOwner<byte> Owner, int Length) Brotli(ReadOnlyMemory<byte> source)
        {
            MemoryMarshal.TryGetArray(source, out var segment);
            using var memoryStream = new MemoryStream(segment.Array!, segment.Offset, segment.Count);
            using var decompressionStream = new BrotliStream(memoryStream, CompressionMode.Decompress);
            return DecompressCore(source, decompressionStream);
        }

        private static (IMemoryOwner<byte> Owner, int Length) DecompressCore(ReadOnlyMemory<byte> source, Stream decompressionStream)
        {
            var rent = MemoryPool<byte>.Shared.Rent(source.Length * 10);
            try
            {
                var offset = 0;
                var size = decompressionStream.Read(rent.Memory.Span);
                do
                {
                    offset += size;
                    if (offset == rent.Memory.Length)
                    {
                        var expand = MemoryPool<byte>.Shared.Rent(rent.Memory.Length * 2);
                        rent.Memory.CopyTo(expand.Memory);
                        rent.Dispose();
                        rent = expand;
                    }
                    size = decompressionStream.Read(rent.Memory.Span.Slice(offset));
                    if (size == 0)
                    {
                        return (rent, offset);
                    }
                } while (true);
            }
            catch
            {
                rent.Dispose();
                throw;
            }
        }
        static (IMemoryOwner<char> Owner, int Length) GetString(ReadOnlySpan<byte> source)
        {
            var rent = MemoryPool<char>.Shared.Rent(source.Length);
            try
            {
                var memory = rent.Memory;
                var size = Encoding.UTF8.GetChars(source, rent.Memory.Span);
                return (rent, size);
            }
            catch
            {
                rent.Dispose();
                throw;
            }
        }
        private void ProcessMessage(ref PacketHeader header, ReadOnlyMemory<byte> bodyMemory)
        {
            if (header.Version > 1)
            {
                var (owner, length) = header.Version switch
                {
                    3 => Brotli(bodyMemory.Slice(0, header.BodyLength)),
                    2 => Inflate(bodyMemory.Slice(0, header.BodyLength)),
                    _ => throw new NotImplementedException()
                };
                using (owner)
                {
                    var inflated = owner.Memory;
                    var offset = 0;
                    while (offset < length)
                    {
                        header = PacketHeader.Parse(inflated.Slice(offset, PacketHeader.HeaderSize).Span);
                        ProcessMessageCore(ref header, inflated.Slice(offset + PacketHeader.HeaderSize, header.BodyLength));
                        offset += header.Length;
                    }
                }
                return;
            }
            ProcessMessageCore(ref header, bodyMemory);
        }
        private void ProcessMessageCore(ref PacketHeader header, ReadOnlyMemory<byte> bodyMemory)
        {
            (DateTime Now, JToken msg) item = default;
            if (header.Operation == Operation.Notification)
            {
                var (owner, length) = GetString(bodyMemory.Span);
                var raw = string.Empty;
                JToken? msg = default;
                try
                {
                    using (owner)
                    {
                        raw = owner.Memory.Span.Slice(0, length).ToString();
                        msg = JsonConvert.DeserializeObject<JToken>(raw);
                    }
                    if (msg["cmd"] != null)
                    {
                        var cmd = msg["cmd"]!.Value<string>();
                        if (!cmd.StartsWith("PK_") && !IgnoreCmds.Contains(cmd))
                        {
                            item = (DateTime.Now, msg);
                            switch (cmd)
                            {
                                case Cmd.LiveStart:
                                    if (!_liveInfo.Live)
                                    {
                                        _liveInfo.Live = true;
                                        ResetDisconnectTimer();
                                        _getLiveInfoSem.TryRelease();
                                    }
                                    break;
                                case Cmd.ROOM_CHANGE:
                                    var info = _liveInfo.Clone();
                                    info.Title = msg["data"]!["title"]!.Value<string>();
                                    info.Area = msg["data"]!["area_name"]!.Value<string>();
                                    info.ParentArea = msg["data"]!["parent_area_name"]!.Value<string>();
                                    UpdateLiveInfoCore(info);
                                    break;
                                case Cmd.LiveEnd:
                                    _liveInfo.Live = false;
                                    _liveInfo.LiveTime = null;
                                    ResetDisconnectTimer();
                                    break;
                            }
                            if (!_liveInfo.Live)
                            {
                                if (!Keeping)
                                {
                                    switch (cmd)
                                    {
                                        case Cmd.USER_TOAST_MSG:
                                        case Cmd.SUPER_CHAT_MESSAGE:
                                        case Cmd.SendGift when msg["data"]!.Value<string?>("coin_type") == "gold":
                                            ResetDisconnectTimer();
                                            break;
                                        default: break;
                                    }
                                }
                                if (!Stay)
                                {
                                    if (_liveOptions.IgnoreWhenOffline.Contains(cmd) && (cmd != Cmd.SendGift || msg["data"]!.Value<string?>("coin_type") == "silver"))
                                    {
                                        item = default;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        item = (DateTime.Now, msg);
                    }
                }
                catch (JsonReaderException)
                {
                    _logger.LogError($"RoomId: {RoomId} invalid json string:\n{raw}\n ");
                    throw;
                }
            }
            else if (header.Operation == Operation.HeartBeatResponse)
            {
                _heartBeat = true;
                MemoryMarshal.AsMemory(bodyMemory).Span.Reverse();
                var popularity = MemoryMarshal.Read<int>(bodyMemory.Span);
                if ((popularity != _popularity) || (_liveInfo.Live && ++_samePopularityCount >= 120 / _liveOptions.HeartbeatInterval))
                {
                    _samePopularityCount = 0;
                    var rawjo = JObject.FromObject(new { cmd = Cmd.Popularity, popularity });
                    item = (DateTime.Now, rawjo);
                    if (popularity == 1)
                    {
                        if (_liveInfo.Live)
                        {
                            _liveInfo.Live = false;
                            ResetDisconnectTimer();
                        }
                        else
                        {
                            item = default;
                        }
                    }
                    else
                    {
                        if (!_liveInfo.Live)
                        {
                            _liveInfo.Live = true;
                            ResetDisconnectTimer();
                            _getLiveInfoSem.TryRelease();
                        }
                        if (_autoKeepType < KeepType.Long)
                        {
                            if (popularity > _autoKeepOptions.LongThreshold)
                            {
                                AutoKeepType = KeepType.Long;
                            }
                            else if (AutoKeepType < KeepType.Normal && popularity > _autoKeepOptions.NormalThreshold)
                            {
                                AutoKeepType = KeepType.Normal;
                            }
                            //else if (AutoKeepType < KeepType.Short && popularity > _autoKeepOptions.ShortThreshold)
                            //{
                            //    AutoKeepType = KeepType.Short;
                            //}
                        }
                    }
                    _popularity = popularity;
                }
                if (!_liveInfo.Live)
                {
                    if (Stay)
                    {
                        --_liveEndCount;
                    }
                    if (_disconnectCount > 0 && Interlocked.Decrement(ref _disconnectCount) == 0)
                    {
                        _disconnectCts.Cancel();
                    }
                }
            }
            else if (header.Operation == Operation.JoinResponse)
            {
                _logger.LogDebug($" 已连接到 {RoomId}, Host: {Host}");
            }
            else
            {
                _logger.LogError($"Unknwon Message:{(int)header.Operation}");
            }
            if (item != default)
            {
                _q.Writer.TryWrite(item);
            }
        }
        async Task Reconnect()
        {
            _connection?.Dispose();
            var prefer = true;
            while (!_disconnectCts.IsCancellationRequested)
            {
                try
                {
                    await ReconnetSem.WaitAsync(-_popularity, _disconnectCts.Token);
                    try
                    {
                        await ConnectCoreAsync(prefer);
                        return;
                    }
                    catch (Exception e)
                    {
                        LogError(e, Host);
                        prefer = false;
                        await Task.Delay(TimeSpan.FromSeconds(5), _disconnectCts.Token);
                    }
                    finally
                    {
                        ReconnetSem.Release();
                    }
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
        }

        private async Task RecvLoop(CancellationToken cancellationToken)
        {
            var cts = new CancellationTokenSource();
            try
            {
                using var d = _disconnectCts.Token.Register(() => { _connection.Dispose(); });
                _ = HeartBeat(cts.Token);
                var rent = MemoryPool<byte>.Shared.Rent(4096);
                try
                {
                    var memory = rent.Memory;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (await _connection.ReadAsync(memory.Slice(0, PacketHeader.HeaderSize), cancellationToken) == 0)
                        {
                            break;
                        }
                        var header = PacketHeader.Parse(memory.Span);
                        if (header.HeaderLength != PacketHeader.HeaderSize)
                        {
                            throw new InvalidOperationException($"header error:{header}");
                        }
                        var contentSize = header.BodyLength;
                        if (header.Length > rent.Memory.Length)
                        {
                            rent.Dispose();
                            rent = MemoryPool<byte>.Shared.Rent(header.Length);
                            memory = rent.Memory;
                        }
                        try
                        {
                            if (contentSize != 0 && await _connection.ReadAsync(memory.Slice(0, contentSize), cancellationToken) == 0)
                            {
                                break;
                            }
                        }
                        catch (Exception e) when (e is (TaskCanceledException or OperationCanceledException))
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            throw new InvalidOperationException($"header:{header}", e);
                        }
                        _alive = true;
                        ProcessMessage(ref header, memory.Slice(0, contentSize));
                    }
                }
                finally
                {
                    rent.Dispose();
                }
            }
            catch (WebSocketException e) when (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
            }
            catch (WebSocketException e)
            {
                _logger.LogError($"RoomId: {RoomId} ErrorCode:{e.ErrorCode} WebsocketErrorCode:{e.WebSocketErrorCode} Exception:{e}");
            }
            catch (IOException e) when (e.InnerException is SocketException se && new[] { 995, 125, 104 }.Contains(se.ErrorCode))
            {
            }
            catch (Exception e) when (e is (TaskCanceledException or OperationCanceledException) || e.InnerException is ObjectDisposedException)
            {
            }
            catch (Exception e)
            {
                LogError(e);
            }
            finally
            {
                cts.Cancel();
                if (!_disconnectCts.IsCancellationRequested)
                {
                    _logger.LogWarning($"RoomId: {RoomId,-9} Aborted,Liver: {UName,-20} {nameof(Popularity)}: {Popularity,-7} Host: {Host}");
                    await Reconnect();
                    _logger.LogWarning($"RoomId: {RoomId,-9} Reconnected, Host: {Host}");
                }
            }
        }
        volatile bool _heartBeat = false;
        private async Task HeartBeat(CancellationToken cancellationToken)
        {
            try
            {
                using var rent = MemoryPool<byte>.Shared.Rent(PacketHeader.HeaderSize);
                var size = PacketHeader.GetBytes(string.Empty, Operation.HeartBeat, rent.Memory.Span);
                var slice = rent.Memory.Slice(0, size);
                var count = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    count = 0;
                    _heartBeat = false;
                    _alive = false;
                    var delay = Task.Delay(TimeSpan.FromSeconds(_liveOptions.HeartbeatInterval), cancellationToken);
                    _ = _connection.WriteAsync(slice, cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(_liveOptions.HeartbeatTimeout), cancellationToken);
                    while (!_heartBeat)
                    {
                        if (count++ >= _liveOptions.HeartbeatRetry)
                        {
                            if (_alive)
                            {
                                delay = Task.CompletedTask;
                                break;
                            }
                            _logger.LogWarning($"RoomId: {RoomId,-9} was disconnect because of no heartbeat,Liver: {UName,-20} {nameof(Popularity)}: {Popularity,-7} Host: {Host}");
                            _connection.Dispose();
                            return;
                        }
                        _ = _connection.WriteAsync(slice, cancellationToken);
                        await Task.Delay(TimeSpan.FromSeconds(_liveOptions.HeartbeatTimeout), cancellationToken);
                    }
                    if (count != 0)
                    {
                        if (count > _liveOptions.HeartbeatRetry)
                        {
                            _logger.LogWarning($"RoomId: {RoomId,-9} heartbeat missing but is alive,Liver: {UName,-20} {nameof(Popularity)}: {Popularity,-7} Host: {Host}");
                        }
                        else
                        {
                            _logger.LogWarning($"RoomId: {RoomId,-9} heartbeat missing,count:{count},Liver: {UName,-20} {nameof(Popularity)}: {Popularity,-7} Host: {Host}");
                        }
                    }
                    await delay;
                }
            }
            catch (Exception e) when (e is (TaskCanceledException or OperationCanceledException or WebSocketException or ObjectDisposedException))
            {
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }
        struct RoomLiveInfo
        {
            public string Title { get; set; }
            public Uri? Cover { get; set; }
            public string Area { get; set; }
            public string ParentArea { get; set; }
            public DateTime LiveTime { get; set; }

            internal void Deconstruct(out string title, out Uri? cover, out string area, out string parentArea, out DateTime? liveTime)
            {
                title = Title;
                area = Area;
                liveTime = LiveTime;
                cover = Cover;
                parentArea = ParentArea;
            }
        }

        static readonly DistributedCacheEntryOptions CacheOp = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromDays(1));
        static int GetLiveInfo412 = 0;
        private async Task RoomInfoUpdateLoop(CancellationToken cancellationToken)
        {
            using var _ = cancellationToken.Register(() => Dispose());
            try
            {
                (_liveInfo.Title, _liveInfo.UserCover, _liveInfo.Area, _liveInfo.ParentArea, _liveInfo.LiveTime) = await _cache.GetAsync<RoomLiveInfo>($"liveinfo:{RealRoomId}", CancellationToken.None);
            }
            catch (Exception e)
            {
                LogError(e);
            }
            if (_initialLiveInfo != null)
            {
                UpdateLiveInfoCore(_initialLiveInfo);
            }
            var retry = Task.CompletedTask;
            var logDelay = Task.CompletedTask;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _getLiveInfoSem.WaitAsync(cancellationToken);
                    var info = await _api.GetLiveInfo(RealRoomId);
                    GetLiveInfo412 = 0;
                    UpdateLiveInfoCore(info);
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (Exception e) when (e is (TaskCanceledException or OperationCanceledException) && cancellationToken.IsCancellationRequested)
                {
                }
                catch (HttpRequestException e) when (e.Message.Contains("412"))
                {
                    if (Interlocked.CompareExchange(ref GetLiveInfo412, 1, 0) == 0)
                    {
                        LogError(e);
                    }
                    if (retry.IsCompleted)
                    {
                        retry = Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ContinueWith((_) => { if (Live) _getLiveInfoSem.TryRelease(); }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                }
                catch (Exception e)
                {
                    if (logDelay.IsCompleted)
                    {
                        LogError(e);
                        logDelay = Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                    }
                    if (retry.IsCompleted)
                    {
                        retry = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ContinueWith((_) => _getLiveInfoSem.TryRelease(), TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                }
            }
        }
        public void UpdateLiveInfo(LiveInfo info)
        {
            UpdateLiveInfoCore(info);
        }
        void UpdateLiveInfoCore(LiveInfo info)
        {
            if (info.Live)
            {
                if (!_liveInfo.Live)
                {
                    _liveInfo.Live = true;
                    ResetDisconnectTimer();
                }
                if (info.Title != Title || (info.UserCover != null && info.UserCover.PathAndQuery != UserCover?.PathAndQuery) || info.LiveTime != LiveTime || info.Area != Area)
                {
                    var roomInfo = new { title = info.Title, area = info.Area, parentArea = info.ParentArea, liveTime = info.LiveTime, cover = info.UserCover };
                    _q.Writer.TryWrite((DateTime.Now, JObject.FromObject(new { cmd = Cmd.RoomInfo, data = roomInfo })));
                    _ = _cache.SetAsync($"liveinfo:{RealRoomId}", roomInfo, CacheOp);
                }
                if (info.UName != null)
                {
                    UName = info.UName;
                }
                info.Keyframe ??= Keyframe;
            }
            else
            {
                if (_liveInfo.Live)
                {
                    _liveInfo.Live = false;
                    ResetDisconnectTimer();
                }
            }
            LiveInfo = info;
        }

        private void LogError(Exception e, string host = "")
        {
            _logger.LogError($"RoomId: {RoomId}{(host != "" ? $" Host:{host} " : "")} Exception:{e}");
        }

        private async Task SendJoinMessage(string key)
        {
            var body = JsonConvert.SerializeObject(new
            {
                roomid = RealRoomId,
                protover = 3,
                platform = "web",
                type = 2,
                uid = 0,
                key
            });
            using var rent = MemoryPool<byte>.Shared.Rent(512);
            var size = PacketHeader.GetBytes(body, Operation.Join, rent.Memory.Span);
            await _connection.WriteAsync(rent.Memory.Slice(0, size));
        }

        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _disposedValue = true;
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)
                    _disconnectCts.Cancel();
                    _disposables.ForEach(a => a.Dispose());
                }

                // TODO: 释放未托管的资源(未托管的对象)并替代终结器
                // TODO: 将大型字段设置为 null
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~BilibiliLiveChatClient()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
