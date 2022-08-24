using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveApi;
using Darkflame.BilibiliLiveChatRecorder.Api.Models;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Darkflame.BilibiliLiveChatRecorder.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Services
{
    public class RankingService : IRankingService
    {
        private LiveChatDbContext _db { get => db ??= _serviceProvider.GetRequiredService<LiveChatDbContext>(); }
        private LiveChatDbContext db = null!;

        private readonly IServiceProvider _serviceProvider;
        private readonly IBilibiliLiveApi _liveApi;
        private readonly IOptionsMonitor<LiverExOptions> _liverOptions;
        private readonly IMemoryCache _memory;
        private readonly IDistributedCache _cache;
        private readonly IOptionsMonitor<QueryOptions> _queryOptions;
        static private readonly SemaphoreSlim DailySem = new SemaphoreSlim(1);
        static private readonly SemaphoreSlim MonthilySem = new SemaphoreSlim(1);

        public RankingService(IServiceProvider serviceProvider, IBilibiliLiveApi liveApi, IMemoryCache memory, IDistributedCache cache, IOptionsMonitor<QueryOptions> queryOptions, IOptionsMonitor<LiverExOptions> liverOptions)
        {
            _serviceProvider = serviceProvider;
            _liveApi = liveApi;
            _liverOptions = liverOptions;
            _memory = memory;
            _cache = cache;
            _queryOptions = queryOptions;
        }
        public async Task<(DateTime Min, DateTime Max)> GetRange(bool fresh = false)
        {
            async Task<(DateTime Min, DateTime Max)> GetRangeCore()
            {
                var data = (await _db.DailyData.AsQueryable().GroupBy(a => true).Select(a => new { Max = a.Max(a => a.Date), Min = a.Min(a => a.Date) }).ToListAsync()).Select(a => (a.Min, a.Max)).SingleOrDefault();
                _memory.Set("oldrange", (data.Min, data.Max));
                return (data.Min, data.Max);
            }
            if (fresh)
            {
                return await GetRangeCore();
            }
            return await _memory.GetOrCreateAtomicAsync<(DateTime Min, DateTime Max)>("range", async op =>
             {

                 var c = _memory.Get<(DateTime Min, DateTime Max)>("oldrange");
                 var data = await GetRangeCore();
                 if (data == default)
                 {
                     return c;
                 }
                 var (min, max) = (data.Min, data.Max);
                 if (c.Max > max)
                 {
                     max = c.Max;
                 }
                 else
                 {
                     _memory.Set<(DateTime Min, DateTime Max)>("oldrange", (min, max));
                 }
                 return (min, max);
             }, a => a.Max >= DateTime.Now.Date);

        }
        private async Task<(DateTime MaxUpdateTime, IEnumerable<(int RoomId, LiveRankingItemData Data)> List)> GetLiveRanking(int year, int month, int day, string? organization, DateTime maxUpdateTime)
        {
            var date = new DateTime(year, month, day);
            var data = await _cache.GetOrCreateAsync(CacheKeys.GetRanking(year, month, day, "individual", organization: organization) + $".data:livehistory", async op =>
            {
                var q = _db.LiveHistory.GetCoreByStart(date.AddMinutes(-5), date.AddDays(1).AddMinutes(-5));
                if (!string.IsNullOrEmpty(organization))
                {
                    var orgRoomIds = _liverOptions.CurrentValue.OrganizationsDic.GetValueOrDefault(organization)?.Livers.Select(a => a.RoomId) ?? Enumerable.Empty<int>();
                    q = q.Where(a => orgRoomIds.Contains(a.Data.RoomId));
                }
                q = q.Where(a => !_queryOptions.CurrentValue.ExcludeRooms.Contains(a.Data.RoomId));
                await DailySem.WaitAsync();
                try
                {
                    var t = q;
                    t = null;
                    foreach (var item in Enum.GetValues<RankingSortType>())
                    {
                        if (SortBy(q, item)?.Take(100) is IQueryable<LiveHistory> lq)
                        {
                            t = t?.Union(lq) ?? lq;
                        }
                    }
                    q = t;
                    var list = await q.ToListAsync();
                    if (!list.Any())
                    {
                        throw new InvalidOperationException();
                    }
                    op.SetSlidingExpiration(TimeSpan.FromDays(2));
                    return (MaxUpdateTime: maxUpdateTime, List: list.Select(a => (a.Data.RoomId, new LiveRankingItemData(a.Data))).ToList());
                }
                finally
                {
                    DailySem.Release();
                }
            }, a => a.MaxUpdateTime >= maxUpdateTime);
            return data;
        }
        private async Task<(DateTime MaxUpdateTime, IEnumerable<(int RoomId, LiveRankingItemData Data)> List)> GetLiveRanking(int year, int month, string? organization, bool distinct, DateTime maxUpdateTime)
        {
            var date = new DateTime(year, month, 1);
            var data = await _cache.GetOrCreateAsync(CacheKeys.GetRanking(year, month, null, "individual", organization: organization) + $".data:livehistory.distinct:{distinct}", async op =>
            {
                var q = _db.LiveHistory.GetCoreByStart(date.AddMinutes(-5), date.AddMonths(1) > DateTime.Now ? DateTime.Now.Date.AddMinutes(-5) : date.AddMonths(1).AddMinutes(-5));
                if (!string.IsNullOrEmpty(organization))
                {
                    var orgRoomIds = _liverOptions.CurrentValue.OrganizationsDic.GetValueOrDefault(organization)?.Livers.Select(a => a.RoomId) ?? Enumerable.Empty<int>();
                    q = q.Where(a => orgRoomIds.Contains(a.Data.RoomId));
                }
                q = q.Where(a => !_queryOptions.CurrentValue.ExcludeRooms.Contains(a.Data.RoomId));
                await MonthilySem.WaitAsync();
                try
                {
                    var t = q;
                    t = null;
                    foreach (var item in Enum.GetValues<RankingSortType>())
                    {
                        if (SortBy(q, item) is IQueryable<LiveHistory> lq)
                        {
                            if (distinct)
                            {
                                var sub = lq;
                                lq = q.Select(a => a.Data.RoomId).Distinct().SelectMany(a => sub.Where(b => b.Data.RoomId == a).Take(1), (a, b) => b);
                                lq = SortBy(lq, item)!;
                            }
                            t = t?.Union(lq.Take(120)) ?? lq.Take(120);
                        }
                    }
                    var list = await t.ToListAsync();
                    if (!list.Any())
                    {
                        throw new InvalidOperationException();
                    }
                    op.SetSlidingExpiration(TimeSpan.FromDays(2));
                    return (MaxUpdateTime: maxUpdateTime, List: list.Select(a => (a.Data.RoomId, new LiveRankingItemData(a.Data))).ToList());
                }
                finally
                {
                    MonthilySem.Release();
                }
            }, a => a.MaxUpdateTime >= maxUpdateTime);
            return data;
        }
        public async Task<RankingList<LiveRankingItem>?> GetLiveRanking(int year, int month, int day, string? organization, RankingSortType? sortBy, bool distinct = false)
        {
            var date = new DateTime(year, month, day);
            if (date > DateTime.Now.Date)
            {
                return null;
            }
            try
            {
                var updateTime = await _memory.GetOrCreateAtomicAsync((nameof(GetLiveRanking), day, date), async (op) =>
                {
                    op.SetAbsoluteExpiration(TimeSpan.FromSeconds(10));
                    return await _db.LiveHistory.GetCoreByStart(date.AddMinutes(-5), date.AddDays(1).AddMinutes(-5)).MaxAsync(a => a.Data.EndTime);
                });
                var r = await _memory.GetOrCreateAtomicAsync(CacheKeys.GetRanking(year, month, day, "individual", sortBy, organization) + $".data:livehistory.distinct:{distinct}", async op =>
                {
                    int? top = !string.IsNullOrEmpty(organization) ? null : 60;
                    var data = await GetLiveRanking(year, month, day, organization, updateTime);
                    var list = SortBy(data.List, sortBy);
                    if (distinct)
                    {
                        list = list.Distinct(DynamicEqualityComparer<(int RoomId, LiveRankingItemData Data)>.Create((a, b) => a.RoomId == b.RoomId, a => a.RoomId.GetHashCode()));
                    }
                    if (top != null)
                    {
                        list = list.Take(top.Value);
                    }
                    if (!list.Any())
                    {
                        throw new InvalidOperationException();
                    }
                    op.SetSlidingExpiration(TimeSpan.FromDays(2));
                    var liverDic = (await _liveApi.GetLiverInfo(list.Select(a => a.RoomId).Distinct())).ToDictionary(a => a.RoomId);
                    return RankingList.Create
                    (
                         data.MaxUpdateTime,
                         list.Count(),
                         list.Select(a => new LiveRankingItem() { Data = a.Data, Liver = liverDic[a.RoomId] }).ToList().AsEnumerable()
                    );

                }, a => a.UpdateTime >= updateTime, TimeSpan.FromMilliseconds(200));
                return r;
            }
            catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
            {
                return null;
            }
        }

        public async Task<RankingList<LiveRankingItem>?> GetLiveRanking(int year, int month, string? organization, RankingSortType? sortBy, bool distinct = false)
        {
            var date = new DateTime(year, month, 1);
            if (date > DateTime.Now.Date)
            {
                return null;
            }
            try
            {
                var end = date.AddMonths(1) > DateTime.Now ? DateTime.Now.Date : date.AddMonths(1).AddMinutes(-5);
                var updateTime = await _memory.GetOrCreateAtomicAsync((nameof(GetLiveRanking), organization, date), async (op) =>
                {
                    op.SetAbsoluteExpiration(TimeSpan.FromSeconds(10));
                    var q = _db.LiveHistory.GetCoreByStart(end.AddDays(-1), end);
                    if (!string.IsNullOrEmpty(organization))
                    {
                        var orgRoomIds = _liverOptions.CurrentValue.OrganizationsDic.GetValueOrDefault(organization)?.Livers.Select(a => a.RoomId) ?? Enumerable.Empty<int>();
                        q = q.Where(a => orgRoomIds.Contains(a.Data.RoomId));
                    }
                    return await q.OrderByDescending(a => a.Data.MaxPopularity).Take(150).MaxAsync(a => (DateTime?)a.Data.EndTime) ?? end.AddMinutes(5);
                }
                );
                var r = await _memory.GetOrCreateAtomicAsync(CacheKeys.GetRanking(year, month, null, "individual", sortBy, organization) + $".data:livehistory.distinct:{distinct}", async op =>
                {
                    int? top = !string.IsNullOrEmpty(organization) ? null : 120;
                    var data = await GetLiveRanking(year, month, organization, distinct, updateTime);
                    var list = SortBy(data.List, sortBy);
                    if (top != null)
                    {
                        list = list.Take(top.Value);
                    }
                    if (!list.Any())
                    {
                        throw new InvalidOperationException();
                    }
                    op.SetSlidingExpiration(TimeSpan.FromDays(2));
                    var liverDic = (await _liveApi.GetLiverInfo(list.Select(a => a.RoomId).Distinct())).ToDictionary(a => a.RoomId);
                    return RankingList.Create
                    (
                         data.MaxUpdateTime,
                         list.Count(),
                         list.Select(a => new LiveRankingItem() { Data = a.Data, Liver = liverDic[a.RoomId] }).ToList().AsEnumerable()
                    );

                }, a => a.UpdateTime >= updateTime, TimeSpan.FromMilliseconds(200));
                return r;
            }
            catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
            {
                return null;
            }
        }
        private async Task<(DateTime MaxUpdateTime, IEnumerable<(int RoomId, RankingItemData Data)> List)> GetIndividualRanking(int year, int month, int day, string? organization, DateTime maxUpdateTime)
        {
            var date = new DateTime(year, month, day);
            var data = await _cache.GetOrCreateAsync(CacheKeys.GetRanking(year, month, day, organization: organization), async op =>
            {
                var q = _db.DailyData.AsQueryable().Where(a => a.Date == date);
                if (!string.IsNullOrEmpty(organization))
                {
                    var orgRoomIds = _liverOptions.CurrentValue.OrganizationsDic.GetValueOrDefault(organization)?.Livers.Select(a => a.RoomId) ?? Enumerable.Empty<int>();
                    q = q.Where(a => orgRoomIds.Contains(a.RoomId));
                }
                q = q.Where(a => !_queryOptions.CurrentValue.ExcludeRooms.Contains(a.RoomId));
                await DailySem.WaitAsync();
                try
                {
                    var t = q;
                    t = null;
                    foreach (var item in Enum.GetValues<RankingSortType>())
                    {
                        if (SortBy(q, item)?.Take(100) is IQueryable<DailyData> lq)
                        {
                            t = t?.Union(lq) ?? lq;
                        }
                    }
                    q = t;
                    var list = await q.ToListAsync();
                    if (!list.Any())
                    {
                        throw new InvalidOperationException();
                    }
                    op.SetSlidingExpiration(TimeSpan.FromDays(2));
                    return (MaxUpdateTime: maxUpdateTime, List: list.Select(a => (a.RoomId, new RankingItemData(a.Data))).ToList());
                }
                finally
                {
                    DailySem.Release();
                }
            }, a => a.MaxUpdateTime >= maxUpdateTime);
            return data;
        }

        private async Task<(DateTime MaxUpdateTime, IEnumerable<(int RoomId, RankingItemData Data)> List)> GetIndividualRanking(int year, int month, string? organization, DateTime maxUpdateTime)
        {
            var date = new DateTime(year, month, 1);
            var data = await _cache.GetOrCreateAsync(CacheKeys.GetRanking(year, month, null, organization: organization), async op =>
            {
                var q = _db.MonthlyData.AsQueryable().Where(a => a.Date == date);
                if (!string.IsNullOrEmpty(organization))
                {
                    var orgRoomIds = _liverOptions.CurrentValue.OrganizationsDic.GetValueOrDefault(organization)?.Livers.Select(a => a.RoomId) ?? Enumerable.Empty<int>();
                    q = q.Where(a => orgRoomIds.Contains(a.RoomId));
                }
                q = q.Where(a => !_queryOptions.CurrentValue.ExcludeRooms.Contains(a.RoomId));
                await MonthilySem.WaitAsync();
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                try
                {
                    var t = q;
                    t = null;
                    foreach (var item in Enum.GetValues<RankingSortType>())
                    {
                        if (SortBy(q, item)?.Take(120) is IQueryable<MonthlyData> lq)
                        {
                            t = t?.Union(lq) ?? lq;
                        }
                    }
                    var list = await t.ToListAsync();
                    if (!list.Any())
                    {
                        throw new InvalidOperationException();
                    }
                    op.SetSlidingExpiration(TimeSpan.FromDays(2));
                    return (MaxUpdateTime: maxUpdateTime, List: list.Select(a => (a.RoomId, new RankingItemData(a.Data))).ToList());
                }
                finally
                {
                    MonthilySem.Release();
                }
            }, a => a.MaxUpdateTime >= maxUpdateTime);
            return data;
        }

        private async Task<DateTime> GetDailyUpdateTime(DateTime date)
        {
            return await _memory.GetOrCreateAtomicAsync((nameof(GetDailyUpdateTime), date), async (op) =>
            {
                op.SetAbsoluteExpiration(TimeSpan.FromSeconds(10));
                return await _db.DailyData.AsQueryable().Where(a => a.Date == date).MaxAsync(a => a.UpdateTime);
            });
        }
        private async Task<DateTime> GetMonthlyUpdateTime(DateTime date)
        {
            return await _memory.GetOrCreateAtomicAsync((nameof(GetMonthlyUpdateTime), date), async (op) =>
            {
                op.SetAbsoluteExpiration(TimeSpan.FromSeconds(10));
                return await _db.MonthlyData.AsQueryable().Where(a => a.Date == date).MaxAsync(a => a.UpdateTime);
            });
        }
        public async Task<RankingList<RankingItem>?> GetIndividualRanking(int year, int month, int day, string? organization, RankingSortType? sortBy)
        {
            var date = new DateTime(year, month, day);
            if (date > DateTime.Now.Date)
            {
                return null;
            }
            try
            {
                var updateTime = await GetDailyUpdateTime(date);
                var r = await _memory.GetOrCreateAtomicAsync(CacheKeys.GetRanking(year, month, day, "individual", sortBy, organization), async op =>
                {
                    int? top = !string.IsNullOrEmpty(organization) ? null : 60;
                    var data = await GetIndividualRanking(year, month, day, organization, updateTime);
                    var list = SortBy(data.List, sortBy);
                    if (top != null)
                    {
                        list = list.Take(top.Value);
                    }
                    op.SetSlidingExpiration(TimeSpan.FromDays(2));
                    var liverDic = (await _liveApi.GetLiverInfo(list.Select(a => a.RoomId))).ToDictionary(a => a.RoomId);
                    return RankingList.Create
                    (
                       data.MaxUpdateTime,
                       list.Count(),
                       list.Select(a => new RankingItem() { Data = a.Data, Liver = liverDic[a.RoomId] }).ToList().AsEnumerable()
                    );


                }, a => a.UpdateTime >= updateTime, TimeSpan.FromMilliseconds(200));
                return r;
            }
            catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
            {
                return null;
            }
        }
        public async Task<RankingList<RankingItem>?> GetIndividualRanking(int year, int month, string? organization, RankingSortType? sortBy)
        {
            var date = new DateTime(year, month, 1);
            if (date > DateTime.Now.Date)
            {
                return null;
            }
            try
            {
                var updateTime = await GetMonthlyUpdateTime(date);
                var r = await _memory.GetOrCreateAtomicAsync(CacheKeys.GetRanking(year, month, null, "individual", sortBy, organization), async op =>
                {
                    int? top = !string.IsNullOrEmpty(organization) ? null : 120;
                    var data = await GetIndividualRanking(year, month, organization, updateTime);
                    var list = SortBy(data.List, sortBy);
                    if (top != null)
                    {
                        list = list.Take(top.Value);
                    }
                    op.SetSlidingExpiration(TimeSpan.FromDays(2));
                    var liverDic = (await _liveApi.GetLiverInfo(list.Select(a => a.RoomId))).ToDictionary(a => a.RoomId);
                    return RankingList.Create
                    (
                       data.MaxUpdateTime,
                       list.Count(),
                       list.Select(a => new RankingItem() { Data = a.Data, Liver = liverDic[a.RoomId] }).ToList().AsEnumerable()
                    );


                }, a => a.UpdateTime >= updateTime, TimeSpan.FromMilliseconds(200));
                return r;
            }
            catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
            {
                return null;
            }
        }
        public async Task<RankingList<RankingItem>?> GetIndividualRankingWithTopDailyData(int year, int month, string? organization, RankingSortType? sortBy)
        {
            var date = new DateTime(year, month, 1);
            if (date > DateTime.Now.Date)
            {
                return null;
            }
            try
            {
                var updateTime = await GetMonthlyUpdateTime(date);
                var r = await _cache.GetOrCreateAsync(CacheKeys.GetRanking(year, month, null, "individual", sortBy, organization) + ".data:dailytop", async op =>
                {
                    var q = _db.DailyData.AsQueryable().Where(a => a.Date >= date && a.Date < updateTime);
                    int? top = 120;
                    if (!string.IsNullOrEmpty(organization))
                    {
                        var orgRoomIds = _liverOptions.CurrentValue.OrganizationsDic.GetValueOrDefault(organization)?.Livers.Select(a => a.RoomId) ?? Enumerable.Empty<int>();
                        q = q.Where(a => orgRoomIds.Contains(a.RoomId));
                        top = null;
                    }
                    q = q.Where(a => !_queryOptions.CurrentValue.ExcludeRooms.Contains(a.RoomId));
                    var sub = SortBy(q, sortBy) ?? q.OrderByDescending(a => a.Data.Participants.Count);
                    q = q.Select(a => a.RoomId).Distinct().SelectMany(a => sub.Where(b => b.RoomId == a).Take(1), (a, b) => b);
                    q = SortBy(q, sortBy) ?? q.OrderByDescending(a => a.Data.Participants.Count);
                    if (top != null)
                    {
                        q = q.Take(top.Value);
                    }
                    await MonthilySem.WaitAsync();
                    try
                    {
                        var list = (await q
                            .ToListAsync()).Select(a => new { RoomId = a.RoomId, UpdateTime = a.UpdateTime, Item = new RankingItemData(a.Data) }).ToList();
                        if (!list.Any())
                        {
                            throw new InvalidOperationException();
                        }
                        op.SetSlidingExpiration(TimeSpan.FromDays(2));
                        var liverDic = (await _liveApi.GetLiverInfo(list.Select(a => a.RoomId))).ToDictionary(a => a.RoomId);
                        return RankingList.Create
                        (
                            updateTime,
                            list.Count,
                            list.Select(a => new RankingItem() { Data = a.Item, Liver = liverDic[a.RoomId] }).ToList().AsEnumerable()
                        );
                    }
                    finally
                    {
                        MonthilySem.Release();
                    }
                }, a => a.UpdateTime >= updateTime, TimeSpan.FromMilliseconds(200));
                return r;
            }
            catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
            {
                return null;
            }
        }
        public async Task<RankingList<SummaryItem>?> GetOrganizedRanking(int year, int month, int day, RankingSortType? sortBy)
        {
            var date = new DateTime(year, month, day);
            if (date > DateTime.Now.Date)
            {
                return null;
            }
            try
            {

                var maxUpdateTime = await GetDailyUpdateTime(date);
                var r = await _memory.GetOrCreateAtomicAsync(CacheKeys.GetRanking(year, month, day, "organization", sortBy), async op =>
                {
                    var data = await _cache.GetOrCreateAsync(CacheKeys.GetRanking(year, month, day, "organization"), async op =>
                    {
                        var q = _db.DailyData.AsQueryable().Where(a => a.Date == date);
                        q = q.Where(a => !_queryOptions.CurrentValue.ExcludeRooms.Contains(a.RoomId));
                        await DailySem.WaitAsync();
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                        try
                        {
                            var list = await q.ToListAsync();
                            if (!list.Any())
                            {
                                throw new InvalidOperationException();
                            }
                            op.SetSlidingExpiration(TimeSpan.FromDays(2));
                            return (MaxUpdateTime: maxUpdateTime, List: Summary.Parse(list.Select(a => a.Data), _liverOptions.CurrentValue).ToList().AsEnumerable());
                        }
                        finally
                        {
                            DailySem.Release();
                        }
                    }, a => a.MaxUpdateTime >= maxUpdateTime);
                    data.List = SortBy(data.List, sortBy);
                    var top = 60;
                    data.List = data.List.Take(top);
                    var liverDic = (await _liveApi.GetLiverInfo(data.List.Where(a => !a.IsOrganization).Select(a => Convert.ToInt32(a.Key)))).ToDictionary(a => a.RoomId);
                    var orgChannelDic = (await _liveApi.GetUserInfo(_liverOptions.CurrentValue.Organizations.Where(a => a.Uid.HasValue).Select(a => a.Uid!.Value))).ToDictionary(a => a.Uid);
                    var list = data.List
                        .Select(a =>
                        {
                            var item = new SummaryItem
                            {
                                Data = a.Data,
                                Liver = a.IsOrganization ? new LiverModel() { Uid = _liverOptions.CurrentValue.OrganizationsDic[a.Key].Uid, RoomId = _liverOptions.CurrentValue.OrganizationsDic[a.Key].RoomId, Name = _liverOptions.CurrentValue.OrganizationsDic[a.Key].Label }
              : liverDic[Convert.ToInt32(a.Key)]
                            };
                            if (a.IsOrganization)
                                item.Liver.Face = item.Liver.Uid.HasValue ? orgChannelDic[item.Liver.Uid.Value].Face : _liverOptions.CurrentValue.OrganizationsDic[a.Key].Face;
                            return item;
                        }).ToList();
                    op.SetSlidingExpiration(TimeSpan.FromDays(2));
                    return RankingList.Create
                    (
                        data.MaxUpdateTime.Date.AddHours(data.MaxUpdateTime.Hour + 1),
                        list.Count,
                        list
                    );
                }, a => a.UpdateTime >= maxUpdateTime, TimeSpan.FromMilliseconds(200));
                return r;
            }
            catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
            {
                return null;
            }
        }
        public async Task<RankingList<SummaryItem>?> GetOrganizedRanking(int year, int month, RankingSortType? sortBy)
        {
            var date = new DateTime(year, month, 1);
            if (date > DateTime.Now.Date)
            {
                return null;
            }
            try
            {

                var maxUpdateTime = await GetMonthlyUpdateTime(date);
                var r = await _cache.GetOrCreateAsync(CacheKeys.GetRanking(year, month, null, "organization", sortBy), async op =>
                {
                    var data = await _cache.GetOrCreateAsync(CacheKeys.GetRanking(year, month, null, "organization"), async op =>
                    {
                        await MonthilySem.WaitAsync();
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                        try
                        {
                            var q = _db.MonthlyData.AsQueryable().Where(a => a.Date == date);
                            q = q.Where(a => !_queryOptions.CurrentValue.ExcludeRooms.Contains(a.RoomId));
                            if (!q.Any())
                            {
                                throw new InvalidOperationException();
                            }
                            op.SetSlidingExpiration(TimeSpan.FromDays(2));
                            return (MaxUpdateTime: maxUpdateTime, List: (await Summary.Parse(q.AsAsyncEnumerable(), _liverOptions.CurrentValue)).ToList().AsEnumerable());
                        }
                        finally
                        {
                            MonthilySem.Release();
                        }

                    }, a => a.MaxUpdateTime >= maxUpdateTime);
                    data.List = SortBy(data.List, sortBy);
                    var top = 120;
                    data.List = data.List.Take(top);
                    var liverDic = (await _liveApi.GetLiverInfo(data.List.Where(a => !a.IsOrganization).Select(a => Convert.ToInt32(a.Key)))).ToDictionary(a => a.RoomId);
                    var orgChannelDic = (await _liveApi.GetUserInfo(_liverOptions.CurrentValue.Organizations.Where(a => a.Uid.HasValue).Select(a => a.Uid!.Value))).ToDictionary(a => a.Uid);
                    var list = data.List
                        .Select(a =>
                        {
                            var item = new SummaryItem
                            {
                                Data = a.Data,
                                Liver = a.IsOrganization ? new LiverModel() { Uid = _liverOptions.CurrentValue.OrganizationsDic[a.Key].Uid, RoomId = _liverOptions.CurrentValue.OrganizationsDic[a.Key].RoomId, Name = _liverOptions.CurrentValue.OrganizationsDic[a.Key].Label }
              : liverDic[Convert.ToInt32(a.Key)]
                            };
                            if (a.IsOrganization)
                                item.Liver.Face = item.Liver.Uid.HasValue ? orgChannelDic[item.Liver.Uid.Value].Face : _liverOptions.CurrentValue.OrganizationsDic[a.Key].Face;
                            return item;
                        }).ToList();
                    op.SetSlidingExpiration(TimeSpan.FromDays(2));
                    return RankingList.Create
                    (
                        data.MaxUpdateTime.AddDays(1).Date,
                        list.Count,
                        list
                    );
                }, a => a.UpdateTime >= maxUpdateTime, TimeSpan.FromMilliseconds(200));
                return r;
            }
            catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
            {
                return null;
            }
        }
        public async Task<(DateTime UpdateTime, Summary Data)?> GetSummary(int year, int month, int day)
        {
            var date = new DateTime(year, month, day);
            if (date > DateTime.Now.Date)
            {
                return null;
            }
            try
            {
                var maxUpdateTime = await GetDailyUpdateTime(date);
                var updateTime = maxUpdateTime.Date.AddHours(maxUpdateTime.Hour + 1);
                var r = await _cache.GetOrCreateAsync(CacheKeys.GetSummary(year, month, day), async op =>
                {
                    var q = _db.DailyData.AsQueryable().Where(a => a.Date == date);
                    q = q.Where(a => !_queryOptions.CurrentValue.ExcludeRooms.Contains(a.RoomId));
                    await DailySem.WaitAsync();
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect();
                    try
                    {
                        var list = await q.ToListAsync();
                        if (!list.Any())
                        {
                            throw new InvalidOperationException();
                        }
                        op.SetSlidingExpiration(TimeSpan.FromDays(2));
                        return
                        (
                            UpdateTime: updateTime,
                            Data: Models.Summary.Parse(list.Select(a => a.Data))
                        );
                    }
                    finally
                    {
                        DailySem.Release();
                    }
                }, a => a.UpdateTime >= updateTime, TimeSpan.FromMilliseconds(200));
                return r;
            }
            catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
            {
                return null;
            }

        }
        public async Task<(DateTime UpdateTime, Summary Data)?> GetSummary(int year, int month)
        {
            var date = new DateTime(year, month, 1);
            if (date > DateTime.Now.Date)
            {
                return null;
            }
            try
            {
                var maxUpdateTime = await GetMonthlyUpdateTime(date);
                var updateTime = maxUpdateTime.AddDays(1).Date;
                var r = await _cache.GetOrCreateAsync(CacheKeys.GetSummary(year, month), async op =>
                {
                    var q = _db.MonthlyData.AsQueryable().Where(a => a.Date == date);
                    q = q.Where(a => !_queryOptions.CurrentValue.ExcludeRooms.Contains(a.RoomId));
                    await MonthilySem.WaitAsync();
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect();
                    try
                    {
                        if (!await q.AnyAsync())
                        {
                            throw new InvalidOperationException();
                        }
                        op.SetSlidingExpiration(TimeSpan.FromDays(2));
                        return
                        (
                            UpdateTime: updateTime,
                            Data: await Models.Summary.Parse(q.AsAsyncEnumerable())
                        );
                    }
                    finally
                    {
                        MonthilySem.Release();
                    }


                }, a => a.UpdateTime >= updateTime, TimeSpan.FromMilliseconds(200));
                return r;
            }
            catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
            {
                return null;
            }
        }
        private IQueryable<T>? SortBy<T>(IQueryable<T> q, RankingSortType? sortBy)
        {
            object? r = q;
            r = q switch
            {
                IQueryable<DailyData> dq => sortBy switch
                {
                    RankingSortType.Participant => dq.OrderByDescending(a => a.Data.Participants),
                    RankingSortType.Income => dq.OrderByDescending(a => a.Data.GoldCoin),
                    RankingSortType.Paiduser => dq.OrderByDescending(a => a.Data.GoldUser),
                    RankingSortType.Livetime => dq.OrderByDescending(a => a.Data.DurationOfLive),
                    _ => null
                },
                IQueryable<MonthlyData> dq => sortBy switch
                {
                    RankingSortType.Participant => dq.OrderByDescending(a => a.Data.Participants),
                    RankingSortType.Income => dq.OrderByDescending(a => a.Data.GoldCoin),
                    RankingSortType.Paiduser => dq.OrderByDescending(a => a.Data.GoldUser),
                    RankingSortType.Livetime => dq.OrderByDescending(a => a.Data.DurationOfLive),
                    _ => null
                },
                IQueryable<LiveHistory> dq => sortBy switch
                {
                    RankingSortType.Participant => dq.OrderByDescending(a => a.Data.Participants),
                    RankingSortType.Income => dq.OrderByDescending(a => a.Data.GoldCoin),
                    RankingSortType.Paiduser => dq.OrderByDescending(a => a.Data.GoldUser),
                    RankingSortType.Fansincrement => dq.OrderByDescending(a => a.Data.FansIncrement),
                    _ => null
                },
                _ => throw new NotImplementedException(typeof(T).ToString())
            };
            return (r as IQueryable<T>);
        }
        private IEnumerable<T> SortBy<T>(IEnumerable<T> q, RankingSortType? sortBy)
        {
            object r = q;
            r = q switch
            {
                IEnumerable<DailyData> dq => sortBy switch
                {
                    RankingSortType.Participant => dq.OrderByDescending(a => a.Data.Participants),
                    RankingSortType.Income => dq.OrderByDescending(a => a.Data.GoldCoin),
                    RankingSortType.Paiduser => dq.OrderByDescending(a => a.Data.GoldUser),
                    RankingSortType.Livetime => dq.OrderByDescending(a => a.Data.DurationOfLive),
                    _ => dq.OrderByDescending(a => a.Data.Participants)
                },
                IEnumerable<MonthlyData> dq => sortBy switch
                {
                    RankingSortType.Participant => dq.OrderByDescending(a => a.Data.Participants),
                    RankingSortType.Income => dq.OrderByDescending(a => a.Data.GoldCoin),
                    RankingSortType.Paiduser => dq.OrderByDescending(a => a.Data.GoldUser),
                    RankingSortType.Livetime => dq.OrderByDescending(a => a.Data.DurationOfLive),
                    _ => dq.OrderByDescending(a => a.Data.Participants)
                },
                IEnumerable<LiveHistory> dq => sortBy switch
                {
                    RankingSortType.Participant => dq.OrderByDescending(a => a.Data.Participants),
                    RankingSortType.Income => dq.OrderByDescending(a => a.Data.GoldCoin),
                    RankingSortType.Paiduser => dq.OrderByDescending(a => a.Data.GoldUser),
                    RankingSortType.Fansincrement => dq.OrderByDescending(a => a.Data.FansIncrement),
                    _ => dq.OrderByDescending(a => a.Data.Participants.Count)
                },
                IEnumerable<(int RoomId, LiveRankingItemData Data)> dq => sortBy switch
                {
                    RankingSortType.Participant => dq.OrderByDescending(a => a.Data.Participants),
                    RankingSortType.Income => dq.OrderByDescending(a => a.Data.GoldCoin),
                    RankingSortType.Paiduser => dq.OrderByDescending(a => a.Data.GoldUser),
                    RankingSortType.Fansincrement => dq.OrderByDescending(a => a.Data.FansIncrement),
                    _ => dq.OrderByDescending(a => a.Data.Participants)
                },
                IEnumerable<(int RoomId, RankingItemData Data)> dq => sortBy switch
                {
                    RankingSortType.Participant => dq.OrderByDescending(a => a.Data.Participants),
                    RankingSortType.Income => dq.OrderByDescending(a => a.Data.GoldCoin),
                    RankingSortType.Paiduser => dq.OrderByDescending(a => a.Data.GoldUser),
                    RankingSortType.Livetime => dq.OrderByDescending(a => a.Data.HourOfLive),
                    _ => dq.OrderByDescending(a => a.Data.Participants)
                },
                IEnumerable<(string Key, bool IsOrganization, Summary Data)> dq => sortBy switch
                {
                    RankingSortType.Participant => dq.OrderByDescending(a => a.Data.Participants),
                    RankingSortType.Income => dq.OrderByDescending(a => a.Data.GoldCoin),
                    RankingSortType.Paiduser => dq.OrderByDescending(a => a.Data.GoldUser),
                    RankingSortType.Livetime => dq.OrderByDescending(a => a.Data.HourOfLive),
                    _ => dq.OrderByDescending(a => a.Data.Participants)
                },
                _ => throw new NotImplementedException(typeof(T).ToString())
            };
            return (r as IEnumerable<T>)!;
        }
    }
}
