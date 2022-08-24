using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveApi;
using Darkflame.BilibiliLiveChatRecorder.Api.Services;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "*" })]
    public class SummaryController : ControllerBase
    {
        private readonly IMemoryCache _memory;
        private readonly IRankingService _rankingService;

        public SummaryController(IMemoryCache memory, IRankingService rankingService)
        {
            _memory = memory;
            _rankingService = rankingService;
        }

        [HttpGet("day")]
        public async Task<IActionResult> GetDailySummary()
        {
            var range = await _rankingService.GetRange();
            if (range == default)
            {
                return NotFound();
            }
            var r = await GetDailySummary(range.Max.Year, range.Max.Month, range.Max.Day);
            if (r is NotFoundResult)
            {
                var max = range.Max.AddDays(-1);
                return await GetDailySummary(max.Year, max.Month, max.Day);
            }
            return r;
        }
        [HttpGet("month")]
        public async Task<IActionResult> GetMonthlySummary()
        {
            var range = await _rankingService.GetRange();
            if (range == default)
            {
                return NotFound();
            }
            var r = await GetMonthlySummary(range.Max.AddDays(-1).Year, range.Max.AddDays(-1).Month);
            if (r is NotFoundResult)
            {
                var max = range.Max.AddMonths(-1);
                return await GetMonthlySummary(max.Year, max.Month);
            }
            return r;
        }
        [HttpGet("{year:int:range(1000,9999)}/{month:int:range(1,12)}/{day:int:range(1,31)}")]
        public async Task<IActionResult> GetDailySummary(int year, int month, int day)
        {
            var r = await _rankingService.GetSummary(year, month, day);
            if (r != null)
            {
                return Ok(new { UpdateTime = r.Value.UpdateTime, Data = r.Value.Data });
            }
            return NotFound();

        }
        [HttpGet("{year:int:range(1000,9999)}/{month:int:range(1,12)}")]
        public async Task<IActionResult> GetMonthlySummary(int year, int month)
        {
            var r = await _rankingService.GetSummary(year, month);
            if (r != null)
            {
                return Ok(new { UpdateTime = r.Value.UpdateTime, Data = r.Value.Data });
            }
            return NotFound();
        }
    }
}
