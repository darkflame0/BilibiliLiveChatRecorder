using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public static class MonthlyDataExtensions
    {
        public static IQueryable<MonthlyData> GetCore(this DbSet<MonthlyData> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default, IEnumerable<int>? excludeRoom = default)
        {
            var q = dbSet.Where(a => a.Date >= start.Date && a.Date < end.Date);
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
        public static async Task Update(this DbSet<MonthlyData> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default)
        {
            end = new DateTime(end.Ticks - (end.Ticks % TimeSpan.TicksPerDay));
            if (start >= end)
            {
                return;
            }
            if (end > DateTime.Now.Date)
            {
                end = DateTime.Now.Date;
            }
            if ((end - start).TotalDays <= 15 || (includeRoom?.Any() ?? false))
            {
                await dbSet.UpdateFromDaily(start, end, includeRoom);
            }
            else
            {
                while ((end - start).TotalDays > 15)
                {
                    var t = start.AddDays(15);
                    await dbSet.UpdateFromDaily(start, t, includeRoom);
                    start = t;
                }
                await dbSet.UpdateFromDaily(start, end, includeRoom);
            }
        }
        private static async Task UpdateFromDaily(this DbSet<MonthlyData> dbSet, DateTime start, DateTime end, IEnumerable<int>? includeRoom = default)
        {
            var db = dbSet.GetContext<LiveChatDbContext>()!;
            var dailyData = (await db.DailyData.GetCore(start.Date, end, includeRoom).OrderBy(a => a.Date).ToListAsync()).GroupBy(a => (a.RoomId, Date: new DateTime(start.Year, start.Month, 1))).ToDictionary(a => a.Key, a => a.ToList());
            var roomIds = dailyData.Select(a => a.Key.RoomId).Distinct();
            var monthlyData = db.MonthlyData.GetCore(new DateTime(start.Year, start.Month, 1), end, roomIds).AsAsyncEnumerable();
            using var db0 = db.GetNewInstance();
            using var trans = await db0.Database.BeginTransactionAsync();
            await foreach (var monthly in monthlyData)
            {
                var daily = dailyData[(monthly.RoomId, monthly.Date)];
                dailyData.Remove((monthly.RoomId, monthly.Date));
                if (monthly.Date >= start && monthly.UpdateTime <= daily.Last().UpdateTime)
                {
                    monthly.Data = new AggregateData();
                    monthly.LastLiveEndTime = default;
                    monthly.LastLiveStartTime = default;
                    monthly.UpdateTime = default;
                }
                await Save(db0, daily, monthly);
            }
            foreach (var daily in dailyData)
            {
                var monthly = new MonthlyData() { RoomId = daily.Key.RoomId, Date = daily.Key.Date };
                await Save(db0, daily.Value, monthly);
            }
            await trans.CommitAsync();

            static async Task Save(LiveChatDbContext db, IEnumerable<DailyData> daily, MonthlyData monthly)
            {
                monthly.Union(daily);
                db.MonthlyData.Update(monthly);
                await db.SaveChangesAsync();
                db.Entry(monthly).State = EntityState.Detached;
                monthly.Data = null!;
            }
        }
    }
}
