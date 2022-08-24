using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public static class DailyDataExtensions
    {
        public static IQueryable<DailyData> GetCore(this DbSet<DailyData> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default, IEnumerable<int>? excludeRoom = default)
        {
            var q = dbSet.AsQueryable();
            if (start.Date == end.Date)
            {
                q = dbSet.Where(a => a.Date == start.Date);
            }
            else
            {
                q = dbSet.Where(a => a.Date >= start.Date && a.Date < end);
            }
            if (includeRoom?.Any() ?? false)
            {
                q = q.Where(a => includeRoom.Contains(a.RoomId));
            }
            if (excludeRoom?.Any() ?? false)
            {
                q = q.Where(a => !excludeRoom.Contains(a.RoomId));
            }
            return q;
        }
        public static async Task Update(this DbSet<DailyData> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default)
        {
            end = new DateTime(end.Ticks - (end.Ticks % TimeSpan.TicksPerHour));
            if (start >= end)
            {
                return;
            }
            if ((end - start).TotalDays <= 10 || (includeRoom?.Any() ?? false))
            {
                await dbSet.UpdateCore(start, end, includeRoom);
            }
            else
            {
                await dbSet.UpdateCore(end.Date, end, includeRoom);
                end = end.Date;
                while ((end - start).TotalDays > 10)
                {
                    var t = end.AddDays(-10);
                    await dbSet.UpdateCore(t, end, includeRoom);
                    end = t;
                }
                await dbSet.UpdateCore(start, end, includeRoom);
            }
        }
        private static async Task UpdateCore(this DbSet<DailyData> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default)
        {
            var db = dbSet.GetContext<LiveChatDbContext>();
            using var db0 = db!.GetNewInstance();
            var hourlyData = await db0.HourlyData.GetCore(start, end, includeRoom).OrderBy(a=>a.Time).ToListAsync();
            var roomIds = hourlyData.Select(a => a.Data.RoomId).Distinct();
            var dailyData = await db0.DailyData.GetCore(start, end, roomIds).ToDictionaryAsync(a => (a.RoomId, a.Date));
            var list = hourlyData.GroupBy(a => new { a.Data.RoomId, a.Time.Date }).Select(a =>
            {
                if (!dailyData.TryGetValue((a.Key.RoomId, a.Key.Date), out var daily) ||  a.Key.Date >= start && daily.UpdateTime <= a.Last().Data.EndTime)
                {
                    daily = new DailyData() { Id = daily?.Id ?? 0, RoomId = a.Key.RoomId, Date = a.Key.Date };
                }
                daily.Union(a);
                foreach (var item in a)
                {
                    item.Data = null!;
                }
                return daily;
            });
            db0.DailyData.UpdateRange(list);
            await db0.SaveChangesAsync();
        }
    }
}
