using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Darkflame.BilibiliLiveChatRecorder.Job
{
    internal class TimedHostedService : BackgroundService, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly IDistributedCache _cache;
        private readonly IOptionsMonitor<JobOptions> _jobOptionsMonitor;
        private readonly ILogger _logger;
        private readonly IHostEnvironment _environment;

        public TimedHostedService(IServiceProvider services, IDistributedCache cache, IOptionsMonitor<JobOptions> jobOptionsMonitor, ILogger<TimedHostedService> logger, IHostEnvironment environment)
        {
            _services = services;
            _cache = cache;
            _jobOptionsMonitor = jobOptionsMonitor;
            _logger = logger;
            _environment = environment;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.WhenAll(
                HourlyLoop(stoppingToken)
                );
        }
        private async Task LiverCacheFlush(CancellationToken cancellationToken)
        {
            var is412 = false;
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LiveChatDbContext>();
            var api = scope.ServiceProvider.GetRequiredService<BilibiliLiveApi.IBilibiliLiveApi>();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var roomIds = await db.DailyData.AsQueryable().Where(a => a.Date >= now.AddYears(-1) && a.Data.MaxPopularity > 10000).Select(a => a.RoomId).Distinct().ToListAsync();
                    var livers = (await api.GetLiverInfo(roomIds)).ToList();
                    is412 = false;
                    foreach (var item in livers)
                    {
                        await _cache.SetAsync($"liver:{item.RoomId}", item);
                        await _cache.SetAsync($"roomId:{(item.ShortId == 0 ? item.RoomId : item.ShortId)}", item.RoomId);
                    }
                }
                catch (HttpRequestException e) when (e.Message.Contains("412"))
                {
                    if (!is412)
                    {
                        _logger.LogError(e.ToString());
                    }
                    await Task.Delay(TimeSpan.FromMinutes(30));
                }
                catch (Exception e)
                {
                    _logger.LogError(e.ToString());
                }
                await Task.Delay(TimeSpan.FromHours(12), cancellationToken);
            }
        }
        private async Task HourlyLoop(CancellationToken cancellationToken)
        {
            async Task MonthlyDataUpdate(DateTime end, CancellationToken cancellationToken)
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LiveChatDbContext>();
                await db.MonthlyData.EnsureTable();
                var start = (await db.MonthlyData.AsQueryable().MaxAsync(a => (DateTime?)a.UpdateTime))?.Date.AddDays(1);
                if (start == null)
                {
                    start = await db.DailyData.AsQueryable().MinAsync(a => a.Date);
                }
                await db.MonthlyData.Update(start.Value, end);
            }

            async Task DailyDataUpdate(DateTime end, CancellationToken cancellationToken)
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LiveChatDbContext>();
                await db.DailyData.EnsureTable();
                var start = (await db.DailyData.AsQueryable().MaxAsync(a => (DateTime?)a.UpdateTime));
                start = start?.Date.AddHours(start.Value.Hour + 1);
                if (start == null)
                {
                    start = await db.HourlyData.AsQueryable().MinAsync(a => a.Time);
                }
                await db.DailyData.Update(start.Value, end);
            }

            async Task LiveHistoryUpdate(DateTime end, CancellationToken cancellationToken)
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LiveChatDbContext>();
                await db.LiveHistory.EnsureTable();
                var start = (await db.LiveHistory.AsQueryable().MaxAsync(a => (DateTime?)a.Data.StartTime));
                start = start?.Date.AddHours(start.Value.Hour + 1);
                if (start == null)
                {
                    //start = await db.HourlyData.MinAsync(a => a.Time);
                    start = new DateTime(2020, 3, 31);
                }
                await db.LiveHistory.Update(start.Value, end);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    DateTime end = default;
                    using var scope = _services.CreateScope();
                    using var db = scope.ServiceProvider.GetRequiredService<LiveChatDbContext>();
                    await db.HourlyData.EnsureTable();
                    end = (await db.ChatMessage.AsQueryable().MaxAsync(a => a.Time));
                    end = new DateTime(end.Ticks - (end.Ticks % (TimeSpan.TicksPerMinute * 60)));
                    if (end > _jobOptionsMonitor.CurrentValue.LimitTime)
                    {
                        end = _jobOptionsMonitor.CurrentValue.LimitTime.Value;
                    }
                    try
                    {
                        var maxTime = (await db.HourlyData.AsQueryable().MaxAsync(a => (DateTime?)a.Time));
                        if (maxTime == null)
                        {
                            var t = end.AddMonths(-1);
                            maxTime = new DateTime(t.Year, t.Month, 1);
                        }
                        var start = maxTime.Value.AddMinutes(HourlyData.IntervalMins);
                        await db.HourlyData.Update(start, end);
                        await LiveHistoryUpdate(end, cancellationToken);
                        await DailyDataUpdate(end, cancellationToken);
                        if (!_environment.IsDevelopment())
                        {
                            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                        }
                        await MonthlyDataUpdate(end, cancellationToken);
                        _logger.LogInformation("小时报表生成完成，start:{start} , end:{end}", start, end);
                    }
                    finally
                    {
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                    }
                    var nextTime = new DateTime(end.Year, end.Month, end.Day, end.Hour, 0, 0).AddHours(1);
                    var delay = (int)(nextTime.AddMinutes(5).AddSeconds(5) - DateTime.Now).TotalMilliseconds;
                    if (delay > 0)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    }
                    if (await db.ChatMessage.Where(a => a.Time >= nextTime.AddHours(-1) && a.Time < nextTime).GroupBy(a => LiveChatDbContext.DateTrunc("minute", a.Time)).Select(a => a.Count()).ToListAsync()
                        is { } tlist && (tlist.Count < 60 || tlist.Min() * 3 <= tlist.Average())
                        )
                    {
                        _logger.LogWarning("数据可能缺失，小时表停止生成");
                        await Task.Delay(int.MaxValue, cancellationToken);
                    }

                }
                catch (Exception e)
                {
                    _logger.LogError(e.ToString());
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
            }
        }
    }
}
