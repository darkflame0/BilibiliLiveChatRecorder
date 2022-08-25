using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Darkflame.BilibiliLiveApi
{
    public class BilibiliLiveHttpClient : IBilibiliLiveApi, IDisposable
    {

        public BilibiliLiveHttpClient(HttpClient httpClient, IOptionsMonitor<BilbiliApiOptions> apiOptions, ILogger<BilibiliLiveHttpClient> logger)
        {
            _logger = logger;
            _httpClient = httpClient;
            _apiOptins = apiOptions.CurrentValue;
            _httpClient.Timeout = TimeSpan.FromSeconds(_apiOptins.Timeout);
            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.71 Safari/537.36 Core/1.94.160.400");
            _disposeOptions = apiOptions.OnChange(op =>
            {
                if (_urlOptions.GetRoomListUrl.Count > op.Urls.GetRoomListUrl.Count)
                {
                    for (var i = op.Urls.GetRoomListUrl.Count; i < _urlOptions.GetRoomListUrl.Count; i++)
                    {
                        _getVLiverRoomListLastCount.Remove(i);
                        _getVLiverRoomListLastCall.Remove(i);
                    }
                }
                Interlocked.Exchange(ref _apiOptins, op);
            }
);
        }
        private readonly ILogger<BilibiliLiveHttpClient> _logger;
        private readonly ConcurrentDictionary<string, object> _412Set = new();
        private readonly HttpClient _httpClient;
        private BilbiliApiOptions _apiOptins;
        private BilbiliApiUrlOptions _urlOptions => _apiOptins.Urls;
        private IDisposable _disposeOptions;
        private readonly ParallelOptions ParallelOptions = new() { MaxDegreeOfParallelism = 8 };
#nullable disable warnings
        public async Task<bool> GetLiveStatus(int roomId)
        {
            try
            {
                return (await GetRoomInit(roomId).ConfigureAwait(false)).Live;
            }
            catch
            {
                return false;
            }
        }
        public async Task<IEnumerable<int>> GetLiveRoomId(IEnumerable<int> roomIds)
        {
            return (await GetRoomInit(roomIds).ConfigureAwait(false)).Where(a => a.Live).Select(a => a.RoomId);
        }
        public async Task<IEnumerable<(LiverInfo Liver, LiveInfo LiveInfo)>> GetLiveRoomByUid(IEnumerable<long> uids)
        {
            try
            {
                var r = await ParallelEx.WhenAll(uids.Chunk(300), ParallelOptions, async (a, _) => await GetLiveRoomByUidCore(a)).ConfigureAwait(false);
                _412Set.Remove(nameof(GetLiveRoomByUid), out _);
                return r.SelectMany(a => a).ToList();
            }
            catch (HttpRequestException e) when (e.Message.Contains("412"))
            {
                if (!_412Set.ContainsKey(nameof(GetLiveRoomByUid)))
                {
                    _412Set.TryAdd(nameof(GetLiveRoomByUid), null);
                    _logger.LogError(e.ToString());
                }
                return Enumerable.Empty<(LiverInfo Liver, LiveInfo LiveInfo)>().ToList();
            }
        }

        private async Task<IEnumerable<(LiverInfo Liver, LiveInfo LiveInfo)>> GetLiveRoomByUidCore(IEnumerable<long> uids)
        {
            if (!uids.Any())
                return Enumerable.Empty<(LiverInfo Liver, LiveInfo LiveInfo)>();
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetLiveStatusByUidUrl}&{GetArrayQueryString("uids", uids)}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            return (jo["data"] as JObject)?.Properties().Values()
                .Select(a => (new LiverInfo(a["room_id"].Value<int>(), a["uid"].Value<long>(), a["uname"].Value<string>(), a["face"].Value<string>(), a["short_id"].Value<int>()),
               new LiveInfo(a["title"].Value<string>(), a["area_v2_name"].Value<string>(), DateTimeOffset.FromUnixTimeSeconds(a["live_time"].Value<int>()).LocalDateTime, a["cover_from_user"].Value<string>(), a["keyframe"].Value<string>(), true, a["online"].Value<int>(), a["uname"].Value<string>(), a["area_v2_parent_name"].Value<string>())))
                ?? Enumerable.Empty<(LiverInfo Liver, LiveInfo LiveInfo)>();
        }
        public async ValueTask<int> GetRealRoomId(int shortId)
        {
            var roomId = (await GetRoomInit(shortId).ConfigureAwait(false)).RoomId;
            return roomId;
        }

        public async ValueTask<int> GetShortId(int roomId)
        {
            if (roomId < 1001)
            {
                return roomId;
            }
            return (await GetLiverInfo(roomId).ConfigureAwait(false)).ShortId;
        }
        public async Task<IEnumerable<int>> GetRealRoomId(IEnumerable<int> shortIds)
        {
            return (await GetRoomInit(shortIds)).Select(a => a.RoomId);
        }
        public async Task<LiveInfo> GetLiveInfo(int roomId)
        {
            try
            {
                return await GetLiveInfo1(roomId);
            }
            catch
            {
                return await GetLiveInfo2(roomId);
            }
        }
        public async Task<LiveInfo> GetLiveInfo1(int roomId)
        {
            if (roomId == 0)
            {
                return default;
            }
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetInfoByRoomUrl}{roomId}").ConfigureAwait(false);
            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            jo = jo["data"];
            var roomInfo = jo["room_info"];
            var userInfo = jo["anchor_info"]["base_info"];
            var liveTime = roomInfo["live_start_time"].Value<int>();
            return new LiveInfo(roomInfo["title"].Value<string>(), roomInfo["area_name"].Value<string>(), liveTime == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(liveTime).LocalDateTime, roomInfo["cover"].Value<string>(), roomInfo["keyframe"]?.Value<string>(), roomInfo["live_status"].Value<int>() == 1, roomInfo["online"].Value<int>(), userInfo["uname"].Value<string>(), roomInfo["parent_area_name"].Value<string>());
        }
        public async Task<LiveInfo> GetLiveInfo2(int roomId)
        {
            if (roomId == 0)
            {
                return null;
            }
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetRoomInfoUrl}{roomId}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            jo = jo["data"];
            return new LiveInfo(jo["title"].Value<string>(), jo["area_name"].Value<string>(), DateTime.TryParse(jo["live_time"].Value<string>(), out var liveTime) ? liveTime : null, jo["user_cover"].Value<string>(), jo["keyframe"].Value<string>(), jo["live_status"].Value<int>() == 1, jo["online"].Value<int>(), null, jo["parent_area_name"].Value<string>());
        }
        public async Task<LiveInfo> GetH5LiveInfo(int roomId)
        {
            if (roomId == 0)
            {
                return default;
            }
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetH5InfoByRoomUrl}{roomId}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            jo = jo["data"];
            var roomInfo = jo["room_info"];
            var userInfo = jo["anchor_info"]["base_info"];
            var liveTime = roomInfo["live_start_time"].Value<int>();
            return new LiveInfo(roomInfo["title"].Value<string>(), roomInfo["area_name"].Value<string>(), liveTime == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(liveTime).LocalDateTime, roomInfo["cover"].Value<string>(), null, roomInfo["live_status"].Value<int>() == 1, roomInfo["online"].Value<int>(), userInfo["uname"].Value<string>(), roomInfo["parent_area_name"].Value<string>());
        }
        public async Task<IEnumerable<(int RoomId, LiveInfo LiveInfo)>> GetLiveInfo(IEnumerable<int> roomIds)
        {
            var r = await ParallelEx.WhenAll(roomIds.Chunk(300), ParallelOptions, (a, _) => GetLiveInfoCore(a)).ConfigureAwait(false);
            return r.SelectMany(a => a).ToList();
        }
        public async Task<IEnumerable<(int RoomId, LiveInfo LiveInfo)>> GetLiveInfoCore(IEnumerable<int> roomIds)
        {
            if (!roomIds.Any())
                return Enumerable.Empty<(int, LiveInfo)>();
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetLiveInfoByIdsUrl}{GetArrayQueryString("ids", roomIds)}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            return (jo["data"] as JObject)?.Properties()
                .Select(a => (
                a.Value["roomid"].Value<int>(),
                new LiveInfo(a.Value["title"].Value<string>(), a.Value["area_v2_name"].Value<string>(), DateTime.TryParse(a.Value["live_time"].Value<string>(), out var liveTime) ? liveTime : (DateTime?)null, a.Value["user_cover"].Value<string>(), a.Value["cover"].Value<string>(), a.Value["live_status"].Value<int>() == 1, a.Value["online"].Value<int>(), a.Value["uname"].Value<string>(), a.Value["area_v2_parent_name"].Value<string>()))
                )
                ?? Enumerable.Empty<(int, LiveInfo)>();
        }
        public async Task<(IEnumerable<(string Host, int Port, int WsPort)> Servers, string Token, int MaxDelay)> GetRoomServerConfWithMaxDeley1(int roomId)
        {
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetConf}{roomId}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                return (Enumerable.Empty<(string Host, int Port, int WsPort)>(), "", default);
            }
            jo = jo["data"];
            var servers = jo["host_server_list"].Select(server => (server["host"].Value<string>(), server["port"].Value<int>(), server["ws_port"].Value<int>()));
            return (servers, jo["token"].ToString(), jo["max_delay"].Value<int>());
        }
        public async Task<(IEnumerable<(string Host, int Port, int WsPort)> Servers, string Token, int MaxDelay)> GetRoomServerConfWithMaxDeley2(int roomId)
        {
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetDanmuInfo}{roomId}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                return (Enumerable.Empty<(string Host, int Port, int WsPort)>(), "", default);
            }
            jo = jo["data"];
            var servers = jo["host_list"].Select(server => (server["host"].Value<string>(), server["port"].Value<int>(), server["ws_port"].Value<int>()));
            return (servers, jo["token"].ToString(), jo["max_delay"].Value<int>());
        }

        private static readonly Random Ran = new Random();
        internal async Task<(IEnumerable<(string Host, int Port, int WsPort)> Servers, string Token, int MaxDelay)> GetRoomServerConfWithMaxDeley(int roomId)
        {
            try
            {
                (IEnumerable<(string Host, int Port, int WsPort)> Servers, string Token, int MaxDelay) r = default;
                try
                {
                    r = await GetRoomServerConfWithMaxDeley2(roomId).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.LogWarning($"RoomId: {roomId} GetDanmakuInfo fallback ,Exception:{e.Message}");
                    r = await GetRoomServerConfWithMaxDeley1(roomId).ConfigureAwait(false);
                }
                _412Set.Remove(nameof(GetRoomServerConfWithMaxDeley), out _);
                return r;
            }
            catch (HttpRequestException e) when (e.Message.Contains("412"))
            {
                if (!_412Set.ContainsKey(nameof(GetRoomServerConfWithMaxDeley)))
                {
                    _412Set.TryAdd(nameof(GetRoomServerConfWithMaxDeley), null);
                    _logger.LogError(e.ToString());
                }
                return (Enumerable.Empty<(string Host, int Port, int WsPort)>(), "", 0);
            }

        }

        public async Task<(IEnumerable<(string Host, int Port, int WsPort)> Servers, string Token)> GetRoomServerConf(int roomId)
        {
            var r = await GetRoomServerConfWithMaxDeley(roomId).ConfigureAwait(false);
            return (r.Servers, r.Token);
        }

        public async Task<LiverInfo?> GetLiverInfoByUid(long uid)
        {
            var roomId = await GetRoomIdByUid(uid).ConfigureAwait(false);
            return await GetLiverInfo(roomId);
        }
        public async Task<LiverInfo> GetLiverInfo(int roomId)
        {
            if (roomId == 0)
            {
                return default;
            }
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetInfoByRoomUrl}{roomId}").ConfigureAwait(false);
            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            jo = jo["data"];
            var roomInfo = jo["room_info"];
            var userInfo = jo["anchor_info"]["base_info"];
            return new LiverInfo(roomInfo["room_id"].Value<int>(), roomInfo["uid"].Value<long>(), userInfo["uname"].ToString(), userInfo["face"].ToString(), roomInfo["short_id"].Value<int>());
        }
        public async Task<(LiverInfo Liver, LiveInfo Live)> GetLiverLiveInfo(int roomId)
        {
            if (roomId == 0)
            {
                return default;
            }
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetInfoByRoomUrl}{roomId}").ConfigureAwait(false);
            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            jo = jo["data"];
            var roomInfo = jo["room_info"];
            var userInfo = jo["anchor_info"]["base_info"];
            var liveTime = roomInfo["live_start_time"].Value<int>();
            return (new LiverInfo(roomInfo["room_id"].Value<int>(), roomInfo["uid"].Value<long>(), userInfo["uname"].ToString(), userInfo["face"].ToString(), roomInfo["short_id"].Value<int>()),
             new LiveInfo(roomInfo["title"].Value<string>(), roomInfo["area_name"].Value<string>(), liveTime == 0 ? (DateTime?)null : DateTimeOffset.FromUnixTimeSeconds(liveTime).LocalDateTime, roomInfo["cover"].Value<string>(), roomInfo["keyframe"]?.Value<string>(), roomInfo["live_status"].Value<int>() == 1, roomInfo["online"].Value<int>(), userInfo["uname"].Value<string>(), roomInfo["parent_area_name"].Value<string>()));
        }

        public async Task<IEnumerable<LiverInfo>> GetLiverInfo(IEnumerable<int> roomIds)
        {
            var r = await ParallelEx.WhenAll(roomIds.Chunk(300), ParallelOptions, (a, _) => GetLiverInfoCore(a)).ConfigureAwait(false);
            return r.SelectMany(a => a).ToList();
        }
        private async Task<IEnumerable<LiverInfo>> GetLiverInfoCore(IEnumerable<int> roomIds)
        {
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetLiverInfoByIdsUrl}&{GetArrayQueryString("ids", roomIds)}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            return (jo["data"] as JObject)?.Properties()
                .Select(a => new LiverInfo(a.Value["roomid"].Value<int>(), a.Value["uid"].Value<long>(), a.Value["uname"].Value<string>(), a.Value["face"].Value<string>(), a.Value["short_id"].Value<int>()))
                ?? Enumerable.Empty<LiverInfo>();
        }
        public async Task<IEnumerable<LiverInfo>> GetLiverInfoByUid(IEnumerable<long> uids)
        {
            var r = await ParallelEx.WhenAll(uids.Chunk(300),ParallelOptions,(a,_) => GetLiverInfoByUidCore(a)).ConfigureAwait(false);
            return r.SelectMany(a => a).ToList();
        }
        private async Task<IEnumerable<LiverInfo>> GetLiverInfoByUidCore(IEnumerable<long> uids)
        {
            if (!uids.Any())
                return Enumerable.Empty<LiverInfo>();
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetLiverInfoByUidUrl}&{GetArrayQueryString("uids", uids)}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            return (jo["data"] as JObject)?.Properties().Select(a => new LiverInfo(a.Value["room_id"].Value<int>(), a.Value["uid"].Value<long>(), a.Value["uname"].Value<string>(), a.Value["face"].Value<string>()))
                ?? Enumerable.Empty<LiverInfo>();
        }

        private async Task<DateTime> GetLiveTime(int roomId)
        {
            return (await GetLiveInfoCore(new int[] { roomId }).ConfigureAwait(false)).FirstOrDefault().LiveInfo?.LiveTime ?? default;
        }

        private async Task<(int Count, List<(LiverInfo Liver, LiveInfo LiveInfo)> Rooms)> GetVLiverAreaRoomListCore(int page, int index = 0)
        {
            try
            {
                var jo = await _httpClient.GetJTokenAsync(string.Format(_urlOptions.GetRoomListUrl.ElementAt(index), page)).ConfigureAwait(false);
                if (jo["code"].Value<int>() != 0)
                {
                    throw new InvalidOperationException(jo.ToString());
                }
                return (jo["data"]["count"].Value<int>(), jo["data"]["list"].Select(a =>
                {
                    var info = new LiverInfo(a["roomid"].Value<int>(), a["uid"].Value<long>(), a["uname"].ToString(), a["face"].ToString());
                    var liveInfo = new LiveInfo(a["title"].Value<string>(), a["area_v2_name"].Value<string>(), null, a["user_cover"].Value<string>() is { } cover && !cover.Contains("no-cover") ? cover : "", a["system_cover"].Value<string>(), true, a["online"].Value<int>(), a["uname"].ToString(), a["area_v2_parent_name"].Value<string>());
                    if (int.TryParse(a["link"].ToString().Substring(1), out var shortId))
                    {
                        info.ShortId = shortId == info.RoomId ? 0 : shortId;
                    }
                    return (info, liveInfo);
                }).ToList());
            }
            catch (ArgumentOutOfRangeException)
            {
                return (0, Enumerable.Empty<(LiverInfo Liver, LiveInfo LiveInfo)>().ToList());
            }
        }
        private async Task<(int Count, List<(LiverInfo Liver, LiveInfo LiveInfo)> Rooms)> GetMultiPageVLiverAreaRoomListCore(int startPage = 1, int index = 0)
        {
            var page = startPage;
            var time = DateTime.Now;
            var t = GetVLiverAreaRoomListCore(page, index);
            var (count, firstPage) = await t.ConfigureAwait(false);
            var tasks = new ConcurrentBag<(int Count, List<(LiverInfo Liver, LiveInfo LiveInfo)> Rooms)>(Enumerable.Empty<(int Count, List<(LiverInfo Liver, LiveInfo LiveInfo)> Rooms)>().Append((count, firstPage)));
            var totalPage = (int)Math.Ceiling(count / (float)firstPage.Count);
            var i = page + 1;
            await Parallel.ForEachAsync(Enumerable.Range(i, totalPage - startPage)
                , parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = 8 }
            , async (i, c) =>
            {
                var t = await GetVLiverAreaRoomListCore(i, index);
                tasks.Add(t);
            });
            if (totalPage > 1 && tasks.Min(a => a.Count) == firstPage.Count)
            {
                tasks.Add(await GetVLiverAreaRoomListCore(totalPage, index));
            }
            var r = tasks;
            return (count, r.SelectMany(a => a.Rooms).ToList());
        }
        private Dictionary<int, DateTime> _getVLiverRoomListLastCall = new();
        private Dictionary<long, int> _getVLiverRoomListLastCount = new();
        public async Task<List<(LiverInfo Liver, LiveInfo LiveInfo)>> GetRecentlyVLiverRoomList()
        {
            try
            {
                var tasks = Enumerable.Empty<Task<List<(LiverInfo Liver, LiveInfo LiveInfo)>>>();
                foreach (var i in _urlOptions.GetRoomListUrl.Select((_, i) => i))
                {
                    tasks = tasks.Append(GetRecentlyVLiverRoomListCore(i));
                }
                List<(LiverInfo Liver, LiveInfo LiveInfo)>[]? r = default;
                try
                {
                    r = await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    r = await Task.WhenAll(tasks.Where(a => !a.IsCanceled && !a.IsFaulted)).ConfigureAwait(false);
                    if (!r.Any())
                    {
                        throw;
                    }
                }
                _412Set.Remove(nameof(GetRecentlyVLiverRoomList), out _);
                return r.SelectMany(a => a).ToList();
            }
            catch (HttpRequestException e) when (e.Message.Contains("412"))
            {
                if (!_412Set.ContainsKey(nameof(GetRecentlyVLiverRoomList)))
                {
                    _412Set.TryAdd(nameof(GetRecentlyVLiverRoomList), null);
                    _logger.LogError(e.ToString());
                }
                return Enumerable.Empty<(LiverInfo Liver, LiveInfo LiveInfo)>().ToList();
            }
        }
        private async Task<List<(LiverInfo Liver, LiveInfo LiveInfo)>> GetRecentlyVLiverRoomListCore(int index = 0)
        {
            var time = DateTime.Now;
            var (count, r) = await GetVLiverAreaRoomListCore(1, index).ConfigureAwait(false);
            if (count > r.Count && (Math.Abs(count - _getVLiverRoomListLastCount.GetValueOrDefault(index)) > r.Count / 3 || (time - _getVLiverRoomListLastCall.GetValueOrDefault(index)).TotalSeconds > 30))
            {
                r = r.Concat((await GetMultiPageVLiverAreaRoomListCore(2, index).ConfigureAwait(false)).Rooms).ToList();
            }
            _getVLiverRoomListLastCount[index] = count;
            _getVLiverRoomListLastCall[index] = time;
            return r;
        }
        public async Task<List<(LiverInfo Liver, LiveInfo LiveInfo)>> GetVLiverRoomList()
        {
            try
            {
                var tasks = Enumerable.Empty<Task<List<(LiverInfo Liver, LiveInfo LiveInfo)>>>();
                foreach (var i in _urlOptions.GetRoomListUrl.Select((_, i) => i))
                {
                    tasks = tasks.Append(GetVLiverRoomListCore(i));
                }
                List<(LiverInfo Liver, LiveInfo LiveInfo)>[]? r = default;
                try
                {
                    r = await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    r = await Task.WhenAll(tasks.Where(a => !a.IsCanceled && !a.IsFaulted)).ConfigureAwait(false);
                    if (!r.Any())
                    {
                        throw;
                    }
                }
                _412Set.Remove(nameof(GetVLiverRoomList), out _);
                return r.SelectMany(a => a).ToList();
            }
            catch (HttpRequestException e) when (e.Message.Contains("412"))
            {
                if (!_412Set.ContainsKey(nameof(GetVLiverRoomList)))
                {
                    _412Set.TryAdd(nameof(GetVLiverRoomList), null);
                    _logger.LogError(e.ToString());
                }
                return Enumerable.Empty<(LiverInfo Liver, LiveInfo LiveInfo)>().ToList();
            }
        }
        private async Task<List<(LiverInfo Liver, LiveInfo LiveInfo)>> GetVLiverRoomListCore(int index = 0)
        {
            var time = DateTime.Now;
            var (count, rooms) = await GetMultiPageVLiverAreaRoomListCore(1, index);
            _getVLiverRoomListLastCount[index] = count;
            _getVLiverRoomListLastCall[index] = time;
            return rooms;
        }

        private async Task<IEnumerable<(long Uid, int RoomId)>> GetRoomIdByUidCore(IEnumerable<long> uids)
        {
            if (uids.Any())
            {
                return Enumerable.Empty<(long Uid, int RoomId)>();
            }
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetRoomIdsByIdsUrl}{GetArrayQueryString("uids", uids)}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            return (jo["data"] as JObject)?.Properties()
                .Select(a => (Convert.ToInt64(a.Name), Convert.ToInt32(a.Value)))
                ?? Enumerable.Empty<(long Uid, int RoomId)>();
        }
        public async Task<int> GetRoomIdByUid(long uid)
        {
            return (await GetRoomIdByUidCore(new[] { uid })).SingleOrDefault().RoomId;
        }
        public async Task<IEnumerable<(long Uid, int RoomId)>> GetRoomIdByUid(IEnumerable<long> uids)
        {
            var r = await ParallelEx.WhenAll(uids.Chunk(300), ParallelOptions, (a, _) => GetRoomIdByUidCore(a)).ConfigureAwait(false);
            return r.SelectMany(a => a).ToList();
        }

        private async Task<(int RoomId, int ShortId, bool Live)> GetRoomInit(int roomId)
        {
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetRoomInfoUrl}{roomId}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            jo = jo["data"];
            var shortId = jo["short_id"].Value<int>();
            roomId = jo["room_id"].Value<int>();
            return (roomId, shortId, jo["live_status"].Value<int>() == 1);
        }

        public async Task<IEnumerable<(int RoomId, int ShortId, bool Live)>> GetRoomInit(IEnumerable<int> roomIds)
        {
            var r = await ParallelEx.WhenAll(roomIds.Chunk(300), ParallelOptions, (a, _) => GetRoomInitCore(a)).ConfigureAwait(false);
            return r.SelectMany(a => a).ToList();
        }

        private async Task<IEnumerable<(int RoomId, int ShortId, bool Live)>> GetRoomInitCore(IEnumerable<int> roomIds)
        {
            if (!roomIds.Any())
            {
                return Enumerable.Empty<(int RoomId, int ShortId, bool Live)>();
            }
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetRoomInitByIdsUrl}{GetArrayQueryString("ids", roomIds)}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            var op = new DistributedCacheEntryOptions() { SlidingExpiration = TimeSpan.FromDays(3) };
            return (jo["data"] as JObject)?.Properties().Select(a =>
            {
                var shortId = a.Value["short_id"].Value<int>();
                var roomId = a.Value["roomid"].Value<int>();
                return (roomId, shortId, a.Value["live_status"].Value<int>() == 1);
            }) ?? Enumerable.Empty<(int RoomId, int ShortId, bool Live)>();
        }

        public async Task<UserInfo?> GetUserInfo(long uid)
        {
            try
            {
                return (await GetUserInfoCore(new[] { uid }).ConfigureAwait(false)).First();
            }
            catch (InvalidOperationException)
            {
                return default;
            }

        }

        public async Task<IEnumerable<UserInfo>> GetUserInfo(IEnumerable<long> uids)
        {
            var r = await ParallelEx.WhenAll(uids.Chunk(300), ParallelOptions, (a, _) => GetUserInfoCore(a)).ConfigureAwait(false);
            return r.SelectMany(a => a).ToList();
        }

        public async Task<string> GetLiverName(int roomId)
        {
            return (await GetLiverInfo(roomId)).Name;
        }

        private async Task<IEnumerable<UserInfo>> GetUserInfoCore(IEnumerable<long> uids)
        {
            var jo = await _httpClient.GetJTokenAsync($"{_urlOptions.GetMultipleUserUrl}&{GetArrayQueryString("uids", uids)}").ConfigureAwait(false);

            if (jo["code"].Value<int>() != 0)
            {
                throw new InvalidOperationException(jo.ToString());
            }
            return (jo["data"] as JObject)?.Properties().Select(a => new UserInfo(a.Value["info"]["uid"].Value<long>(), a.Value["info"]["uname"].ToString(), a.Value["info"]["gender"].Value<int>() switch
            {
                1 => "男",
                2 => "女",
                _ => "保密"

            }, a.Value["info"]["platform_user_level"].Value<int>(), a.Value["info"]["face"].ToString())) ?? Enumerable.Empty<UserInfo>();
        }
        private string GetArrayQueryString<T>(string key, IEnumerable<T> values)
        {
            if (!values.Any())
                return string.Empty;
            var sb = new StringBuilder();
            foreach (var item in values)
            {
                sb.Append($"{key}%5B%5D={item}&");
            }
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        public void Dispose()
        {
            _disposeOptions.Dispose();
        }
    }
}
