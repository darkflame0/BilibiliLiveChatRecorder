using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using System.Threading;

namespace Darkflame.BilibiliLiveApi
{
    public class CachedBilibiliLiveHttpClient : IBilibiliLiveApi
    {
        private readonly IDistributedCache _cache;
        private readonly IMemoryCache _memory;
        private readonly BilibiliLiveHttpClient _source;

        public CachedBilibiliLiveHttpClient(BilibiliLiveHttpClient source, IDistributedCache cache, IMemoryCache memory)
        {
            _cache = cache;
            _memory = memory;
            _source = source;
        }

        public async Task<LiverInfo?> GetLiverInfo(int roomId)
        {
            if (roomId == 0)
                return null;
            roomId = await GetRealRoomId(roomId).ConfigureAwait(false);
            return await _cache.GetOrCreateAsync($"liver:{roomId}", async e =>
            {
                e.SetAbsoluteExpiration(TimeSpan.FromDays(1));
                return await _source.GetLiverInfo(roomId).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public async Task<IEnumerable<LiverInfo>> GetLiverInfoByUid(IEnumerable<long> uids)
        {
            return await _source.GetLiverInfoByUid(uids).ConfigureAwait(false);
        }

        public async Task<LiverInfo?> GetLiverInfoByUid(long uid)
        {
            var roomId = await GetRoomIdByUid(uid).ConfigureAwait(false);
            return await GetLiverInfo(roomId).ConfigureAwait(false);
        }

        public async Task<string?> GetLiverName(int roomId)
        {
            return (await GetLiverInfo(roomId).ConfigureAwait(false))?.Name;
        }

        public async Task<IEnumerable<(LiverInfo Liver, LiveInfo LiveInfo)>> GetLiveRoomByUid(IEnumerable<long> uids)
        {
            return await _source.GetLiveRoomByUid(uids).ConfigureAwait(false);
        }

        public Task<IEnumerable<int>> GetLiveRoomId(IEnumerable<int> roomIds)
        {
            return _source.GetLiveRoomId(roomIds);
        }

        public Task<bool> GetLiveStatus(int roomId)
        {
            return _source.GetLiveStatus(roomId);
        }

        public async ValueTask<int> GetRealRoomId(int shortId)
        {
            if (shortId > 10000)
            {
                return shortId;
            }
            var roomId = await _cache.GetAsync<int?>($"roomId:{shortId}").ConfigureAwait(false);
            if (roomId.HasValue)
            {
                return roomId.Value;
            }
            var op = new DistributedCacheEntryOptions() { SlidingExpiration = TimeSpan.FromDays(3) };
            roomId = (await _source.GetRealRoomId(shortId).ConfigureAwait(false));
            _ = _cache.SetAsync($"roomId:{shortId}", roomId, op).ConfigureAwait(false);
            return roomId.Value;
        }
        private static T? CheckMiss<T>(T data, ref int miss)
        {
            if (data == null && Interlocked.CompareExchange(ref miss, 1, 0) == 0)
            {
                throw new Exception();
            }
            return data;
        }
        public async Task<IEnumerable<int>> GetRealRoomId(IEnumerable<int> shortIds)
        {
            var r = Enumerable.Empty<int>();
            var miss = 0;
            try
            {
                var tasks = shortIds.Select(async shortId =>
shortId > 10000 ? shortId : miss == 1 ? default : CheckMiss(await _cache.GetAsync<int?>($"roomId:{shortId}").ConfigureAwait(false), ref miss).GetValueOrDefault());
                r = (await Task.WhenAll(tasks).ConfigureAwait(false))!;
            }
            catch (Exception)
            {
                var op = new DistributedCacheEntryOptions() { SlidingExpiration = TimeSpan.FromDays(3) };
                r = Enumerable.Empty<int>();
                foreach (var (roomId, shortId, _) in await _source.GetRoomInit(shortIds).ConfigureAwait(false))
                {
                    _ = _cache.SetAsync($"roomId:{roomId}", roomId, op).ConfigureAwait(false);
                    if (shortId != 0)
                    {
                        _ = _cache.SetAsync($"roomId:{shortId}", roomId, op).ConfigureAwait(false);
                    }
                    r = r.Append(roomId);
                }
            }
            return r.ToList();
        }

        public async Task<IEnumerable<(long Uid, int RoomId)>> GetRoomIdByUid(IEnumerable<long> uids)
        {
            var r = Enumerable.Empty<(long Uid, int RoomId)>();
            var miss = 0;
            try
            {
                var tasks = uids.Select(async uid => (uid, miss == 1 ? default : CheckMiss(await _cache.GetAsync<int?>($"roomId-by-uid:{uid}").ConfigureAwait(false), ref miss).GetValueOrDefault()));
                r = (await Task.WhenAll(tasks).ConfigureAwait(false))!;
            }
            catch (Exception)
            {
                var op = new DistributedCacheEntryOptions() { SlidingExpiration = TimeSpan.FromDays(3) };
                r = Enumerable.Empty<(long Uid, int RoomId)>();
                foreach (var (uid, roomId) in await _source.GetRoomIdByUid(uids).ConfigureAwait(false))
                {
                    _ = _cache.SetAsync($"roomId-by-uid:{uid}", roomId, op).ConfigureAwait(false);
                    r = r.Append((uid, roomId));
                }
            }
            return r.ToList();
        }

        public Task<int> GetRoomIdByUid(long uid)
        {
            return _cache.GetOrCreateAsync($"roomId-by-uid:{uid}", async op =>
            {
                op.SetSlidingExpiration(TimeSpan.FromDays(3));
                return (await GetRoomIdByUid(new [] { uid }).ConfigureAwait(false)).SingleOrDefault().RoomId;
            });
        }

        public Task<LiveInfo> GetLiveInfo(int roomId)
        {
            return _source.GetLiveInfo(roomId);
        }

        public Task<IEnumerable<(int, LiveInfo)>> GetLiveInfo(IEnumerable<int> roomIds)
        {
            return _source.GetLiveInfo(roomIds);
        }

        public async Task<(IEnumerable<(string Host, int Port, int WsPort)> Servers, string Token)> GetRoomServerConf(int roomId)
        {
            var r = await _source.GetRoomServerConfWithMaxDeley(roomId).ConfigureAwait(false);
            return (r.Servers, r.Token);
        }

        public ValueTask<int> GetShortId(int roomId)
        {
            return _source.GetShortId(roomId);
        }

        public async Task<UserInfo?> GetUserInfo(long uid)
        {
            if (uid == 0)
            {
                return null;
            }
            return await _cache.GetOrCreateAsync($"user:{uid}", async e =>
            {
                e.SetSlidingExpiration(TimeSpan.FromDays(7));
                e.SetAbsoluteExpiration(TimeSpan.FromDays(15));
                return (await _source.GetUserInfo(uid).ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        public async Task<IEnumerable<LiverInfo>> GetLiverInfo(IEnumerable<int> roomIds)
        {
            roomIds = roomIds.Where(a => a != 0).Distinct();
            var r = Enumerable.Empty<LiverInfo>();
            var miss = 0;
            try
            {
                var tasks = roomIds.Select(async roomId => miss == 1 ? default : CheckMiss(await _cache.GetAsync<LiverInfo>($"liver:{roomId}").ConfigureAwait(false), ref miss));
                r = (await Task.WhenAll(tasks).ConfigureAwait(false))!;
            }
            catch (Exception)
            {
                var op = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromDays(1));
                r = Enumerable.Empty<LiverInfo>();
                foreach (var liverInfo in await _source.GetLiverInfo(roomIds).ConfigureAwait(false))
                {
                    _ = _cache.SetAsync($"liver:{liverInfo.RoomId}", liverInfo, op).ConfigureAwait(false);
                    r = r.Append(liverInfo);
                }
            }
            return r.ToList();
        }

        public async Task<List<(LiverInfo Liver, LiveInfo LiveInfo)>> GetVLiverRoomList()
        {
            return await _source.GetVLiverRoomList().ConfigureAwait(false);
        }

        public async Task<IEnumerable<UserInfo>> GetUserInfo(IEnumerable<long> uids)
        {
            uids = uids.Where(a => a != 0).Distinct();
            var miss = 0;
            var r = Enumerable.Empty<UserInfo>();
            try
            {
                var tasks = uids.Select(async uid => miss == 1 ? default : CheckMiss(await _cache.GetAsync<UserInfo>($"user:{uid}").ConfigureAwait(false), ref miss));
                r = (await Task.WhenAll(tasks).ConfigureAwait(false))!;
            }
            catch (Exception)
            {
                var op = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromDays(3))
                .SetAbsoluteExpiration(TimeSpan.FromDays(7));
                r = Enumerable.Empty<UserInfo>();
                foreach (var info in await _source.GetUserInfo(uids).ConfigureAwait(false))
                {
                    _ = _cache.SetAsync($"user:{info.Uid}", info, op).ConfigureAwait(false);
                    r = r.Append(info);
                }
            }
            return r.ToList();
        }

        public Task<List<(LiverInfo Liver, LiveInfo LiveInfo)>> GetRecentlyVLiverRoomList()
        {
            return _source.GetRecentlyVLiverRoomList();
        }
    }
}
