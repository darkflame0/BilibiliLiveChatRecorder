using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public static partial class ChatMessageExtensions
    {
        public static IQueryable<ChatMessage> GetCore(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default, IEnumerable<int>? excludeRoom = default)
        {
            var q = dbSet.Where(a => a.Time >= start && a.Time < end);
            if (includeRoom?.Any() ?? false)
            {
                q = q.Where(a => includeRoom.Contains(a.RoomId));
            }
            else if (excludeRoom?.Any() ?? false)
            {
                q = q.Where(a => !excludeRoom.Contains(a.RoomId));
            }
            return q;
        }

        public static IQueryable<ChatMessage> GetCore(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, int roomId)
        {
            if (roomId == 0)
            {
                return dbSet.GetCore(start, end);
            }
            return dbSet.GetCore(start, end, new int[] { roomId });
        }
#nullable disable warnings
        public static async ValueTask<IEnumerable<(int RoomId, int Gold)>> GetGoldCoinGroupByRoom(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, IEnumerable<int> includeRoom = null, IEnumerable<int> excludeRoom = null, int limit = 100)
        {
            var q = dbSet.GetCore(start, end, includeRoom, excludeRoom);
            var gold = await q.Where(a => (a.Raw["cmd"].Value<string>() == Cmd.SendGift && a.Raw["data"]["coin_type"].Value<string>() == "gold") || a.Raw["cmd"].Value<string>() == Cmd.USER_TOAST_MSG || a.Raw["cmd"].Value<string>() == Cmd.SUPER_CHAT_MESSAGE)
                .GroupBy(a => a.RoomId)
                .Select(a => new
                {
                    RoomId = a.Key,
                    Gold = a.Sum(a => a.Raw["cmd"].Value<string>() == Cmd.SUPER_CHAT_MESSAGE ? a.Raw["data"]["price"].Value<int>() * 1000 : a.Raw["data"]["total_coin"].Value<int?>() ??
                    (a.Raw["data"]["price"].Value<int?>() == null ?
                        (a.Raw["data"]["guard_level"].Value<int>() == 3 ? 198000 :
                        a.Raw["data"]["guard_level"].Value<int>() == 2 ? 1980000 :
                        a.Raw["data"]["guard_level"].Value<int>() == 1 ? 19800000 : 0) - (a.Raw["data"]["op_type"].Value<int>() == 2 ? 40000 * (int)Math.Pow(10, 4 - a.Raw["data"]["guard_level"].Value<int>()) : 0)
                    : a.Raw["data"]["price"].Value<int>()))
                }).OrderByDescending(a => a.Gold).Take(limit).ToListAsync();
            return gold.Select(a => (a.RoomId, a.Gold));
        }
        public static ValueTask<IEnumerable<(long Uid, int Gold)>> GetGoldCoinGroupByUid(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, int roomId, int? limit = null)
        {
            return dbSet.GetGoldCoinGroupByUid(start, end, new int[] { roomId }, null, limit);
        }
        public static async ValueTask<IEnumerable<(long Uid, int Gold)>> GetGoldCoinGroupByUid(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, IEnumerable<int> includeRoom = null, IEnumerable<int> excludeRoom = null, int? limit = null)
        {
            var d = (await dbSet.GetRoomData(start, end, includeRoom, excludeRoom)).Aggregate();
            var r = d.GoldUser.Select(a => (a.Key, a.Value)).OrderByDescending(a => a.Value).AsEnumerable();
            if (limit > 0)
            {
                r = r.Take(limit.Value);
            }
            return r.ToList();
        }
        public static async ValueTask<IDictionary<long, DateTime>> GetParticipant(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = null)
        {
            var q = dbSet.GetCore(start, end, includeRoom);
            var q2 = q.Where(a => a.Raw["info"][0][9].Value<int>() == 0).Select(a => new { Uid = a.Raw["info"][2][0].Value<long>(), a.Time })
                .Concat(q.Where(a => Cmd.GiftCmd.Contains(a.Raw["cmd"].Value<string>())).Select(a => new { Uid = a.Raw["data"]["uid"].Value<long>(), a.Time }));
            var participantMap = await q2.GroupBy(a => a.Uid).Select(a => new { Uid = a.Key, Time = a.Max(a => a.Time) }).ToDictionaryAsync(a => a.Uid, a => a.Time);
            return participantMap;
        }
        public static async ValueTask<int> DeleteOnlineRankCount(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, ChatMessageTableType tableType = ChatMessageTableType.Default)
        {
            var tableName = tableType switch
            {
                ChatMessageTableType.Temp => "ChatMessageTemp",
                _ => "ChatMessage"
            };
            end = new DateTime(end.Ticks - (end.Ticks % (TimeSpan.TicksPerMinute * 60)));
            var t = start;
            var r = 0;
            while (t < end)
            {
                r += await dbSet.DeleteOnlineRankCountCore(t, t.AddDays(1) is { } t2 && t2 < end ? t2 : end, tableName);
                t = t2;
            }
            return r;
        }
        private static async ValueTask<int> DeleteOnlineRankCountCore(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, string tableName)
        {
            var db = dbSet.GetContext<LiveChatDbContext>();
            return await db.Database.ExecuteSqlRawAsync(@$"
delete from ""{tableName}"" AS a using (
    select row_number() over(partition by ""RoomId"", date_trunc('minute', ""Time"") order by(""Raw""->'data'->> 'count')::int desc, ""Id"")  rid, ""Id""
        from ""{tableName}""
        where
    ""Time"" >= {{0}} and ""Time"" < {{1}} and ""Raw""->> 'cmd' = 'ONLINE_RANK_COUNT') t
 where a.""Time"" >= {{0}} and a.""Time"" < {{1}} and t.rid <> 1 and a.""Id"" = t.""Id"" ", start, end);
        }
#nullable restore
        public static ValueTask<IDictionary<long, DateTime>> GetParticipant(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, int roomId)
        {
            return dbSet.GetParticipant(start, end, new int[] { roomId });
        }
        public static async ValueTask<IEnumerable<RoomData>> GetRoomDataNoCache(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default, IEnumerable<int>? excludeRoom = default)
        {
            var db = dbSet.GetContext<LiveChatDbContext>()!;
            return (await db.HourlyData.GetRoomData(start, end, includeRoom, excludeRoom)).OrderBy(a => a.Time).Select(a => a.Data);
        }
        public static ValueTask<IEnumerable<RoomData>> GetRoomData(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, int roomId, bool live = false, bool useCache = true)
        {
            return dbSet.GetRoomData(start, end, new int[] { roomId }, live: live, useCache: useCache);
        }
        public static async ValueTask<IEnumerable<RoomData>> GetRoomData(this DbSet<ChatMessage> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default, IEnumerable<int>? excludeRoom = default, bool live = false, bool useCache = true)
        {
            var db = dbSet.GetContext<LiveChatDbContext>()!;
            var data = Enumerable.Empty<RoomData>();
            if ((end - start).TotalHours > 1 && useCache)
            {
                var q = db.HourlyData.GetCore(start, end, includeRoom, excludeRoom);
                var list = (await q.OrderBy(a => a.Time).ToListAsync()).AsEnumerable();
                if (live)
                {
                    list = list.Where(a => a.Data.Title != null || a.Data.MaxPopularity != 0);
                }
                if (list.Any())
                {
                    var first = list.FirstOrDefault();
                    var last = list.LastOrDefault();
                    if (first?.Time > start && (first.Time - start).TotalHours < 6)
                    {
                        var t = await dbSet.GetRoomDataNoCache(start, first.Time, includeRoom, excludeRoom);
                        if (t != null)
                        {
                            data = data = data.Concat(t);
                        }
                    }
                    data = data.Concat(list.Select(a => a.Data));
                    var time = last?.Data.EndTime;
                    if (time < end)
                    {
                        if ((end - time).Value.TotalHours > 6)
                        {
                            time = end.AddHours(-6);
                        }
                        var t = await dbSet.GetRoomDataNoCache(time.Value, end, includeRoom, excludeRoom);
                        if (t.Any())
                        {
                            t = t.SkipWhile(a => a.EndTime <= data.LastOrDefault()?.EndTime).ToList();
                            data = data.Concat(t);
                        }
                    }
                    else if (time > end)
                    {
                        data = data.SkipLast(1);
                        var t = await dbSet.GetRoomDataNoCache(last!.Time, end, includeRoom, excludeRoom);
                        if (t.Any())
                        {
                            data = data.Concat(t);
                        }
                    }
                    return data.ToList();
                }
            }
            var t0 = await dbSet.GetRoomDataNoCache(start, end, includeRoom, excludeRoom);
            if (t0 != null)
            {
                data = data = data.Concat(t0);
            }
            return data.Where(a => includeRoom?.Contains(a.RoomId) ?? true && (!excludeRoom?.Contains(a.RoomId) ?? true)).ToList();
        }
        public static async ValueTask<IEnumerable<RoomData>> GetRoomDataPerLive(this DbSet<ChatMessage> dbSet, int roomId, DateTime start, DateTime end, bool useCache = true)
        {
            var db = dbSet.GetContext<LiveChatDbContext>()!;
            var data = (await db.ChatMessage.GetRoomData(start, end, roomId, true, useCache)).AggregateByPerLive().Reverse();
            return data.ToList();
        }
        public static async ValueTask<IEnumerable<RoomData>> GetLatest(this DbSet<ChatMessage> dbSet, int roomId, DateTime? since = null, DateTime? until = null, int? limit = 1)
        {
            var db = dbSet.GetContext<LiveChatDbContext>()!;
            var q = db.HourlyData.GetDataOfLiveStart(since).Where(a => a.RoomId == roomId);
            if (until.HasValue)
            {
                q = q.Where(a => a.StartTime <= until);
            }
            if (since.HasValue)
            {
                q = q.Where(a => a.StartTime >= since);
            }
            if (limit.HasValue)
            {
                q = q.OrderByDescending(a => a.Time).Take(limit.Value);
            }
            var t = (await q.LastOrDefaultAsync());
            var now = DateTime.Now;
            var r = await dbSet.GetRoomDataPerLive(roomId, t?.Time ?? now.AddHours(-1), now);
            if (until.HasValue)
            {
                r = r.Where(a => a.StartTime <= until).ToList().AsEnumerable();
            }
            return r.Take(limit!.Value);
        }
    }
    public enum ChatMessageTableType
    {
        Default = 0,
        Temp = 1,
    }
}
