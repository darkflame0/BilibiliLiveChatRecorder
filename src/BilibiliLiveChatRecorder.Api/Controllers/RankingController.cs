using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Darkflame.BilibiliLiveChatRecorder.Api.Services;
using Darkflame.BilibiliLiveChatRecorder.Api.Filters;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "*" })]
    public class RankingController : ControllerBase
    {
        private readonly IRankingService _rankingService;
        private readonly IMemoryCache _memory;

        public RankingController(IMemoryCache memory, IRankingService rankingService)
        {
            _memory = memory;
            _rankingService = rankingService;
        }
        [HttpGet("range")]
        public async Task<IActionResult> GetRange()
        {
            var (min, max) = await _rankingService.GetRange();
#if DEBUG
            return Ok(new { min, max });
#else
            return Ok(new { min = min < new DateTime(2020, 1, 1) ? new DateTime(2020, 1, 1) : min, max });
#endif
        }
        [Polly("RankingCircuitBreaker")]
        [HttpGet("day")]
        public async Task<IActionResult> GetDailyRanking([FromQuery] RankingQueryModel m)
        {
            var range = await _rankingService.GetRange();
            if (range == default)
            {
                return NotFound();
            }
            var r = await GetDailyRanking(range.Max.Year, range.Max.Month, range.Max.Day, m);
            if (r is NotFoundResult)
            {
                var max = range.Max.AddDays(-1);
                return await GetDailyRanking(max.Year, max.Month, max.Day, m);
            }
            return r;

        }
        [Polly("RankingCircuitBreaker")]
        [HttpGet("month")]
        public async Task<IActionResult> GetMonthlyRanking([FromQuery] RankingQueryModel m)
        {
            var range = await _rankingService.GetRange();
            if (range == default)
            {
                return NotFound();
            }
            var r = await GetMonthlyRanking(range.Max.AddDays(-1).Year, range.Max.AddDays(-1).Month, m);
            if (r is NotFoundResult)
            {
                var max = range.Max.AddMonths(-1);
                return await GetMonthlyRanking(max.Year, max.Month, m);
            }
            return r;
        }
        [Polly("RankingCircuitBreaker")]
        [HttpGet("{year:int:range(1000,9999)}/{month:int:range(1,12)}/{day:int:range(1,31)}")]
        public async Task<IActionResult> GetDailyRanking(int year, int month, int day, [FromQuery] RankingQueryModel m)
        {
            object? r = true switch
            {
                _ when (m.GroupBy == "organization") => await _rankingService.GetOrganizedRanking(year, month, day, m.SortBy),
                _ when (m.DataType == "livehistory") => await _rankingService.GetLiveRanking(year, month, day, m.Organization, m.SortBy, m.Distinct),
                _ => await _rankingService.GetIndividualRanking(year, month, day, m.Organization, m.SortBy)
            };
            if (r != null)
            {
                return Ok(r);
            }
            return NotFound();
        }
        [Polly("RankingCircuitBreaker")]
        [HttpGet("{year:int:range(1000,9999)}/{month:int:range(1,12)}")]
        public async Task<IActionResult> GetMonthlyRanking(int year, int month, [FromQuery] RankingQueryModel m)
        {
            object? r = true switch
            {
                _ when (m.GroupBy == "organization") => await _rankingService.GetOrganizedRanking(year, month, m.SortBy),
                _ when (m.DataType == "livehistory") => await _rankingService.GetLiveRanking(year, month, m.Organization, m.SortBy, m.Distinct),
                //_ when (m.DataType == "dailytop") => await _rankingService.GetIndividualRankingWithTopDailyData(year, month, m.Organization, m.SortBy),
                _ => await _rankingService.GetIndividualRanking(year, month, m.Organization, m.SortBy)
            };
            if (r != null)
            {
                return Ok(r);
            }
            return NotFound();
        }
    }
}
