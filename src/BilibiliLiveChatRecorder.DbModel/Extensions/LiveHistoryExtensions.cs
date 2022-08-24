using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public static class LiveHistoryExtensions
    {
        public static IQueryable<LiveHistory> GetCoreByStart(this DbSet<LiveHistory> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default, IEnumerable<int>? excludeRoom = default)
        {
            var q = dbSet.AsQueryable();
            q = dbSet.Where(a => a.Data.StartTime >= start && a.Data.StartTime < end);
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
        public static IQueryable<LiveHistory> GetCoreByEnd(this DbSet<LiveHistory> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default, IEnumerable<int>? excludeRoom = default)
        {
            var q = dbSet.AsQueryable();
            q = dbSet.Where(a => a.Data.EndTime >= start && a.Data.EndTime < end);
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
        public static async Task Update(this DbSet<LiveHistory> dbSet, DateTime start, DateTime end, DateTime? earliestLiveStart = default, DateTime? latestLiveStart = default, IEnumerable<int>? includeRoom = default)
        {
            end = new DateTime(end.Ticks - (end.Ticks % TimeSpan.TicksPerHour));
            if (start >= end)
            {
                return;
            }
            if ((end - start).TotalDays <= 2 || (includeRoom?.Any() ?? false))
            {
                await dbSet.UpdateCore(start, end, earliestLiveStart, latestLiveStart, includeRoom);
            }
            else
            {
                while ((end - start).TotalDays > 2)
                {
                    var t = start.AddDays(2);
                    await dbSet.UpdateCore(start, t, earliestLiveStart, latestLiveStart, includeRoom);
                    start = t;
                }
                await dbSet.UpdateCore(start, end, earliestLiveStart, latestLiveStart, includeRoom);
            }
        }
        private static async Task UpdateCore(this DbSet<LiveHistory> dbSet, DateTime start, DateTime end, DateTime? earliestLiveStart = default, DateTime? latestLiveStart = default, IEnumerable<int>? includeRoom = default)
        {
            var db = dbSet.GetContext<LiveChatDbContext>();
            using var db0 = db!.GetNewInstance();
            var data = (await db0.HourlyData.GetCore(start, end, includeRoom).OrderBy(a => a.Time).ToListAsync()).Select(a => a.Data);
            start = data.Min(a => (DateTime?)a.StartTime) ?? default;
            if (start == default)
            {
                return;
            }
            var roomIds = data.Select(a => a.RoomId).Distinct();
            var q = db0.LiveHistory.Where(a => a.Data.EndTime.AddMinutes(30) > start && a.Data.StartTime < end && roomIds.Contains(a.Data.RoomId));
            if (latestLiveStart.HasValue)
            {
                q = q.Where(a => a.Data.StartTime < latestLiveStart);
            }
            var liveHistory = (await q.OrderBy(a => a.Data.StartTime).ToListAsync())
                .GroupBy(a => a.Data.RoomId).ToDictionary(a => a.Key, a => a.AsEnumerable());
            foreach (var a in data.GroupBy(a => a.RoomId))
            {
                var t = a.AggregateByPerLive();
                var r = Enumerable.Empty<LiveHistory>();
                if (t.Any())
                {
                    if (liveHistory.TryGetValue(a.Key, out var l))
                    {
                        if (t.Where(a => a.EndTime < l.First().Data.StartTime && a.Title != null) is IEnumerable<RoomData> rdl && rdl.Any())
                        {
                            r = r.Concat(rdl.Select(a => new LiveHistory() { Data = a }));
                        }
                        foreach (var h in l)
                        {
                            if (l.Any(a => a != h && a.Data.StartTime <= h.Data.StartTime && a.Data.EndTime >= h.Data.EndTime))
                            {
                                db0.LiveHistory.Remove(h);
                                continue;
                            }
                            if (t.Where(a => a.StartTime <= h.Data.StartTime && a.EndTime >= h.Data.EndTime).FirstOrDefault() is RoomData rd)
                            {
                                h.Data = rd;
                                r = r.Append(h);
                            }
                            t = t.SkipWhile(a => a.StartTime < h.Data.EndTime).ToList();
                        }

                        if (l.LastOrDefault() is LiveHistory lh && t.FirstOrDefault() is RoomData fr && ((fr.StartTime - lh.Data.EndTime) <= RoomData.LiveInterval
                                || ((fr.Title == null) && ((fr.StartTime - lh.Data.EndTime) < TimeSpan.FromMinutes(30)))))
                        {
                            lh.Data.Union(fr, true);
                            r = r.Append(lh);
                            t = t.Skip(1).ToList();
                        }
                    }

                    foreach (var item in t)
                    {
                        r = r.Append(new LiveHistory() { Data = item });
                    }
                    if (earliestLiveStart.HasValue)
                    {
                        r = r.Where(a => a.Data.StartTime >= earliestLiveStart);
                    }
                    if (latestLiveStart.HasValue)
                    {
                        r = r.Where(a => a.Data.StartTime < latestLiveStart);
                    }
                    db0.LiveHistory.UpdateRange(r = r.ToList());
                }
            }
            await db0.SaveChangesAsync();
        }
    }
}
