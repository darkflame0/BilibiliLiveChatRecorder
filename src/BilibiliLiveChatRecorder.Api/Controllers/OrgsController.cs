using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.Api.Models;
using Darkflame.BilibiliLiveChatRecorder.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "*" })]
    public class OrgsController : ControllerBase
    {
        private readonly IOptionsMonitor<LiverExOptions> _liverOptions;

        public OrgsController(IOptionsMonitor<LiverExOptions> liverOptions)
        {
            _liverOptions = liverOptions;
        }

        [HttpGet]
        public Task<IEnumerable<Organization>> GetAll()
        {
            return Task.FromResult(_liverOptions.CurrentValue.Organizations.AsEnumerable());
        }
        [HttpGet("{organization}/ranking/day")]
        public async Task<IActionResult> GetDailyRanking([FromServices] RankingController rankingController, string organization, [FromQuery] RankingQueryModel m)
        {
            rankingController.ControllerContext = ControllerContext;
            m.Organization = organization;
            return await rankingController.GetDailyRanking(m);

        }
        [HttpGet("{organization}/ranking/month")]
        public async Task<IActionResult> GetMonthlyRanking([FromServices] RankingController rankingController, string organization, [FromQuery] RankingQueryModel m)
        {
            rankingController.ControllerContext = ControllerContext;
            m.Organization = organization;
            return await rankingController.GetMonthlyRanking(m);
        }
        [HttpGet("{organization}/ranking/{year:int:range(1000,9999)}/{month:int:range(1,12)}/{day:int:range(1,31)}")]
        public async Task<IActionResult> GetDailyRanking([FromServices] RankingController rankingController, int year, int month, int day, string organization, [FromQuery] RankingQueryModel m)
        {
            rankingController.ControllerContext = ControllerContext;
            m.Organization = organization;
            return await rankingController.GetDailyRanking(year, month, day, m);
        }
        [HttpGet("{organization}/ranking/{year:int:range(1000,9999)}/{month:int:range(1,12)}")]
        public async Task<IActionResult> GetMonthlyRanking([FromServices] RankingController rankingController, int year, int month, string organization, [FromQuery] RankingQueryModel m)
        {
            rankingController.ControllerContext = ControllerContext;
            m.Organization = organization;
            return await rankingController.GetMonthlyRanking(year, month, m);
        }

    }
}
