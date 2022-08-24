using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel.QueryEntities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public static class HourlyDataExtensions
    {
        public static IQueryable<HourlyData> GetCore(this DbSet<HourlyData> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default, IEnumerable<int>? excludeRoom = default)
        {
            var q = dbSet.Where(a => a.Time >= start && a.Time < end);
            if (includeRoom?.Any() ?? false)
            {
                q = q.Where(a => includeRoom.Contains(a.Data.RoomId));
            }
            if (excludeRoom?.Any() ?? false)
            {
                q = q.Where(a => !excludeRoom.Contains(a.Data.RoomId));
            }
            return q;
        }
        static readonly string GetDataOfLiveStartSql = @$"select h.* from (select a.""{nameof(HourlyData.Time)}"", a.""{nameof(RoomData.RoomId)}"",a.""{nameof(HourlyData.Data.StartTime)}"",a.""LatestEndTime"",a.""Title"" from 
    (select *, lead(""{nameof(HourlyData.Data.EndTime)}"") over(partition by ""{nameof(RoomData.RoomId)}"" order by ""{nameof(HourlyData.Time)}"" desc) ""LastEndTime"",max(""{nameof(HourlyData.Data.EndTime)}"") over(partition by ""{nameof(RoomData.RoomId)}"") ""LatestEndTime"" from ""{nameof(HourlyData)}"" where ({{0}}::timestamp IS NULL OR ""Time"">={{0}}) and (""MaxPopularity""!=0 or ""Title"" is not null)) a
        where (( a.""{nameof(HourlyData.Data.StartTime)}"" - a.""LastEndTime"" > interval '{(int)RoomData.LiveInterval.TotalMinutes} minute') or (a.""LastEndTime"" is null))) h where h.""Title"" is not null";
        internal static IQueryable<RoomLiveTime> GetDataOfLiveStart(this DbSet<HourlyData> dbSet, DateTime? since = default)
        {
            if (since.HasValue)
            {
                since.Value.AddMinutes(since.Value.Minute / IntervalMins * IntervalMins - since.Value.Minute);
            }
            return dbSet.GetContext().Set<RoomLiveTime>().FromSqlRaw(GetDataOfLiveStartSql, since);
        }
        public static async Task Update(this DbSet<HourlyData> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default)
        {
            end = new DateTime(end.Ticks - (end.Ticks % (TimeSpan.TicksPerMinute * 60)));
            if (start >= end)
            {
                return;
            }
            if ((end - start).TotalDays <= 1)
            {
                await dbSet.UpdateCore(start, end, includeRoom);
            }
            else
            {
                while ((end - start).TotalDays > 1)
                {
                    var t = start.AddDays(1);
                    await dbSet.UpdateCore(start, t, includeRoom);
                    start = t;
                }
                await dbSet.UpdateCore(start, end, includeRoom);
            }
        }
#nullable disable warnings
        public static readonly int IntervalMins = HourlyData.IntervalMins;
        public static async ValueTask<IEnumerable<(DateTime Time, RoomData Data)>> GetRoomData(this DbSet<HourlyData> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default, IEnumerable<int>? excludeRoom = default)
        {
            var include = includeRoom?.Any() ?? false;
            var exclude = excludeRoom?.Any() ?? false;
            var db = dbSet.GetContext<LiveChatDbContext>();
            Dictionary<(DateTime Time, int RoomId), (DateTime Time, int RoomId, DateTime StartTime, DateTime EndTime, int MaxPopularity)>? time = default;
            Dictionary<(DateTime Time, int RoomId), (int RoomId, DateTime Time, int FansIncrement)>? fansIncrement = default;
            Dictionary<(DateTime Time, int RoomId), (string Title, string Cover, string Area)>? info = default;
            Dictionary<(DateTime Time, int RoomId, bool IsGold), Dictionary<long, int>>? giftUser = default;
            Dictionary<(DateTime Time, int RoomId), HashSet<long>>? viewer = new();
            Dictionary<(DateTime Time, int RoomId, bool IsGift), Dictionary<long, int>>? danmakuUser = default;
            async Task GetTime()
            {
                var timeT = db.ChatMessage.GetCore(start, end)
.Where(a => (!include || includeRoom.Contains(a.RoomId)) || (EF.Functions.JsonTypeof(a.Raw["data"]["send_master"]["room_id"]) != null && includeRoom.Contains(a.Raw["data"]["send_master"]["room_id"].Value<int>())) && (!exclude || !excludeRoom.Contains(a.RoomId) || (EF.Functions.JsonTypeof(a.Raw["data"]["send_master"]["room_id"]) != null && !excludeRoom.Contains(a.Raw["data"]["send_master"]["room_id"].Value<int>()))))
.Where(a => RoomData.Cmds.Contains(a.Raw["cmd"]!.Value<string>()))
.GroupBy(a => new { Time = new DateTime(a.Time.Year, a.Time.Month, a.Time.Day, a.Time.Hour, a.Time.Minute / IntervalMins * IntervalMins, 0), RoomId = a.Raw["data"]["send_master"]["room_id"].Value<int?>() ?? a.RoomId })
.Select(g => new
{
    g.Key.Time,
    RoomId = g.Key.RoomId,
    StartTime = g.Min(a => a.Raw["cmd"].Value<string>() == Cmd.LiveStart ? a.Time : (DateTime?)null) ?? g.Min(a => a.Time),
    LiveEndTime = g.Max(a => a.Raw["cmd"].Value<string>() == Cmd.LiveEnd ? a.Time : (DateTime?)null),
    EndTime = g.Max(a => a.Time),
    MaxPopularity = g.Max(c => new[] { Cmd.Popularity, Cmd.LiveStart, Cmd.LiveEnd }.Contains(c.Raw["cmd"].Value<string>()) ? c.Raw["popularity"].Value<int?>() ?? 1 : null) ?? 0,


})
.ToListAsync();
                time = (await timeT)
                    .Select(a => (a.Time, a.RoomId, a.StartTime, EndTime: a.StartTime > a.LiveEndTime ? a.EndTime : a.LiveEndTime ?? a.EndTime, a.MaxPopularity))
                    .ToDictionary(a => (a.Time, a.RoomId));


            }
            await GetTime();
            if (!time.Any())
            {
                return Enumerable.Empty<(DateTime Time, RoomData Data)>();
            }
            var pool = Channel.CreateUnbounded<LiveChatDbContext>();
            Enumerable.Repeat(1, includeRoom?.Count() == 1 && (end - start).TotalDays < 1 ? 3 : 1).ToList().ForEach(a => pool.Writer.TryWrite(db.GetNewInstance()));
            async Task GetFans()
            {
                var db = await pool.Reader.ReadAsync();
                var fansIncrementT = db.ChatMessage.GetCore(start, end, includeRoom, excludeRoom)
.Where(a => a.Raw["cmd"].Value<string>() == Cmd.RoomRealTimeMessageUpdate).OrderBy(a => a.Time).Select(a => new { a.Time, a.RoomId, Fans = a.Raw["data"]["fans"].Value<int>() }).Where(a => a.Fans >= 0).ToListAsync();
                try
                {
                    await fansIncrementT;
                }
                finally
                {
                    pool.Writer.TryWrite(db);
                }
                fansIncrement = (await fansIncrementT).GroupBy(a => a.RoomId).SelectMany(a =>
                {
                    var lastFans = a.First();
                    return a.GroupBy(b => new DateTime(b.Time.Year, b.Time.Month, b.Time.Day, b.Time.Hour, b.Time.Minute / IntervalMins * IntervalMins, 0)).Select(b =>
                    {
                        var fans = b.Last();
                        if ((b.First().Time - lastFans.Time).TotalMinutes > 60)
                        {
                            lastFans = b.First();
                        }
                        var t = (RoomId: a.Key, Time: b.Key, FansIncrement: fans.Fans - lastFans.Fans);
                        lastFans = fans;
                        return t;
                    });
                }).ToDictionary(a => (a.Time, a.RoomId));
            }
            async Task GetInfo()
            {
                var db = await pool.Reader.ReadAsync();
                var sub = db.ChatMessage.GetCore(start, end, includeRoom, excludeRoom)
.Where(a => a.Raw["cmd"].Value<string>() == Cmd.RoomInfo)
.GroupBy(a => new { Time = new DateTime(a.Time.Year, a.Time.Month, a.Time.Day, a.Time.Hour, a.Time.Minute / IntervalMins * IntervalMins, 0), a.RoomId })
.Select(g => new { g.Key.RoomId, MaxTime = g.Max(g => g.Time) });
                var infoT = db.ChatMessage.Where(a => a.Raw["cmd"].Value<string>() == Cmd.RoomInfo).Join(sub, a => new { a.RoomId, a.Time }, (b) => new { b.RoomId, Time = b.MaxTime },
                    (a, b) => new
                    {
                        Time = new DateTime(a.Time.Year, a.Time.Month, a.Time.Day, a.Time.Hour, a.Time.Minute / IntervalMins * IntervalMins, 0)
                    ,
                        a.RoomId
                    ,
                        Title = a.Raw["data"]["title"].Value<string?>(),
                        Cover = a.Raw["data"]["cover"].Value<string?>(),
                        Area = a.Raw["data"]["area"].Value<string?>()
                    }).ToListAsync();
                try
                {
                    await infoT;
                }
                finally
                {
                    pool.Writer.TryWrite(db);
                }
                info = (await infoT)
    .Select(a => new { a.RoomId, a.Time, Title = a.Title, a.Cover, a.Area }).ToDictionary(a => (a.Time, a.RoomId), a => (a.Title, a.Cover, a.Area));
            }
            async Task GetGiftUser()
            {
                var db = await pool.Reader.ReadAsync();
                var giftUserT = db.ChatMessage.GetCore(start, end)
.Where(a => (!include || includeRoom.Contains(a.RoomId)) || (EF.Functions.JsonTypeof(a.Raw["data"]["send_master"]["room_id"]) != null && includeRoom.Contains(a.Raw["data"]["send_master"]["room_id"].Value<int>())) && (!exclude || !excludeRoom.Contains(a.RoomId) || (EF.Functions.JsonTypeof(a.Raw["data"]["send_master"]["room_id"]) != null && !excludeRoom.Contains(a.Raw["data"]["send_master"]["room_id"].Value<int>()))))
.Where(a => Cmd.GiftCmd.Contains(a.Raw["cmd"].Value<string>()))
.GroupBy(a => new
{
    Time = new DateTime(a.Time.Year, a.Time.Month, a.Time.Day, a.Time.Hour, a.Time.Minute / IntervalMins * IntervalMins, 0),
    RoomId = a.Raw["data"]["send_master"]["room_id"].Value<int?>() ?? a.RoomId,
    Uid = a.Raw["data"]["uid"].Value<long>()
,
    IsGold = a.Raw["data"]["coin_type"].Value<string>() == "gold"
|| a.Raw["cmd"].Value<string>() == Cmd.USER_TOAST_MSG
|| a.Raw["cmd"].Value<string>() == Cmd.SUPER_CHAT_MESSAGE,
})
.Select(g => new
{
    g.Key.Time,
    g.Key.RoomId,
    g.Key.IsGold,
    g.Key.Uid,
    TotalCoin = g.Sum(a => a.Raw["cmd"].Value<string>() == Cmd.SUPER_CHAT_MESSAGE ?
    a.Raw["data"]["price"].Value<int>() * 1000 :
    a.Raw["data"]["total_coin"].Value<int?>() ??
    a.Raw["data"]["price"].Value<int?>() ??
    (a.Raw["data"]["guard_level"].Value<int>() == 3 ? 198000 :
    a.Raw["data"]["guard_level"].Value<int>() == 2 ? 1980000 :
    a.Raw["data"]["guard_level"].Value<int>() == 1 ? 19800000 : 0) - (a.Raw["data"]["op_type"].Value<int>() == 2 ? 40000 * (int)Math.Pow(10, 4 - a.Raw["data"]["guard_level"].Value<int>()) : 0)
)
}).ToListAsync();
                try
                {
                    await giftUserT;
                }
                finally
                {
                    pool.Writer.TryWrite(db);
                }
                giftUser = (await giftUserT)
     .GroupBy(a => new { Time = a.Time, a.RoomId, a.IsGold })
     .ToDictionary(a => (a.Key.Time, a.Key.RoomId, a.Key.IsGold), a => a.ToDictionary(a => a.Uid, a => a.TotalCoin));
            }
            async Task GetViewer()
            {
                var db = await pool.Reader.ReadAsync();
                var viewerT = db.ChatMessage.GetCore(start, end, includeRoom, excludeRoom)
            .Where(a => a.Raw["cmd"].Value<string>() == Cmd.INTERACT_WORD)
            .GroupBy(a => new
            {
                Time = new DateTime(a.Time.Year, a.Time.Month, a.Time.Day, a.Time.Hour, a.Time.Minute / IntervalMins * IntervalMins, 0),
                a.RoomId,
                Uid = a.Raw["data"]["uid"].Value<long>(),
            })
            .Select(g => new { g.Key.Time, g.Key.RoomId, Uid = g.Key.Uid }).ToListAsync();
                try
                {
                    await viewerT;
                }
                finally
                {
                    pool.Writer.TryWrite(db);
                }
                viewer = (await viewerT)
    .GroupBy(a => new { a.Time, a.RoomId })
    .ToDictionary(a => (a.Key.Time, a.Key.RoomId), a => a.Select(a => a.Uid).ToHashSet());
            }
            async Task GetDanmakuUser()
            {
                var db = await pool.Reader.ReadAsync();
                var danmakuUserT = db.ChatMessage.GetCore(start, end, includeRoom, excludeRoom)
.Where(a => a.Raw["cmd"].Value<string>().StartsWith(Cmd.Danmaku))
.GroupBy(a => new
{
    Time = new DateTime(a.Time.Year, a.Time.Month, a.Time.Day, a.Time.Hour, a.Time.Minute / IntervalMins * IntervalMins, 0),
    a.RoomId,
    Uid = a.Raw["info"][2][0].Value<long>(),
    IsGift = a.Raw["info"][0][9].Value<int>() > 0
})
.Select(g => new { g.Key.Time, g.Key.RoomId, IsGift = g.Key.IsGift, Uid = g.Key.Uid, Count = g.Count() }).ToListAsync();
                try
                {
                    await danmakuUserT;
                }
                finally
                {
                    pool.Writer.TryWrite(db);
                }

                danmakuUser = (await danmakuUserT)
                    .GroupBy(a => new { a.Time, a.RoomId, a.IsGift })
                    .ToDictionary(a => (a.Key.Time, a.Key.RoomId, a.Key.IsGift), a => a.ToDictionary(b => b.Uid, b => b.Count));
            }
            await Task.WhenAll(GetDanmakuUser(), GetInfo(), GetFans(),
                //GetViewer(),
                GetGiftUser());
            pool.Writer.Complete();
            await foreach (var item in pool.Reader.ReadAllAsync())
            {
                await item.DisposeAsync();
            }
            var emptyList = Enumerable.Empty<int>();
            return time.Select(item =>
            {
                var key = item.Key;
                giftUser.TryGetValue((key.Time, key.RoomId, true), out var goldUser);
                giftUser.TryGetValue((key.Time, key.RoomId, false), out var silverUser);
                danmakuUser.TryGetValue((key.Time, key.RoomId, true), out var giftDanmakuUser);
                danmakuUser.TryGetValue((key.Time, key.RoomId, false), out var realDanmakuUser);
                fansIncrement.TryGetValue((key.Time, key.RoomId), out var fans);
                info.TryGetValue((key.Time, key.RoomId), out var info_);
                viewer.TryGetValue((key.Time, key.RoomId), out var viewer_);
                return (key.Time, new RoomData(item.Value.StartTime, item.Value.EndTime, null, item.Value.MaxPopularity, realDanmakuUser?.Sum(a => a.Value) ?? 0, giftDanmakuUser?.Sum(a => a.Value) ?? 0, goldUser?.Sum(a => a.Value) ?? 0, silverUser?.Sum(a => a.Value) ?? 0, goldUser, realDanmakuUser, silverUser?.Keys, giftDanmakuUser?.Keys, viewer_, fans.FansIncrement) { Title = info_.Title, Cover = info_.Cover, Area = info_.Area, RoomId = key.RoomId });
            }).ToList();
        }
        private static async Task UpdateCore(this DbSet<HourlyData> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default)
        {
            using var db0 = dbSet.GetContext<LiveChatDbContext>().GetNewInstance();
            var data = (await db0.HourlyData.GetRoomData(start, end, includeRoom)).Select(a => new HourlyData() { Data = a.Data, Time = a.Time });
            var dic = await db0.HourlyData.GetCore(start, end, includeRoom).Select(a => new { Id = a.Id, Time = a.Time, a.Data.RoomId }).ToDictionaryAsync(a => (a.RoomId, a.Time), a => a.Id);
            using var tran = await db0.Database.BeginTransactionAsync();
            var i = 0;
            foreach (var item in data)
            {
                if (dic.TryGetValue((item.Data.RoomId, item.Time), out var id))
                {
                    item.Id = id;
                }
                db0.HourlyData.Update(item);
                ++i;
                if (i > 255)
                {
                    await db0.SaveChangesAsync();
                    db0.ChangeTracker.Clear();
                    i = 0;
                }
            }
            await db0.SaveChangesAsync();
            await tran.CommitAsync();
        }
    }
}
