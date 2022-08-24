using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Darkflame.BilibiliLiveChatRecorder.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Darkflame.BilibiliLiveApi;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RoomsController : ControllerBase
    {
        private readonly LiveChatDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IBilibiliLiveApi _bilibiliLiveApi;
        private readonly IOptionsMonitor<QueryOptions> _queryOptions;

        public RoomsController(LiveChatDbContext db, IMemoryCache cache, IBilibiliLiveApi bilibiliLiveApi, IOptionsMonitor<QueryOptions> queryOptions)
        {
            _db = db;
            _cache = cache;
            _bilibiliLiveApi = bilibiliLiveApi;
            _queryOptions = queryOptions;
        }
        [HttpGet]
        public IActionResult GetRooms()
        {
            return RedirectPermanent("online");
        }
        [HttpGet("{roomId:int}/latest")]
        public async Task<IActionResult> GetLatest(int roomId, DateTime? since, DateTime? until, [Range(1, 100)] int limit = 1)
        {
            roomId = await _bilibiliLiveApi.GetRealRoomId(roomId);
            var r = await _cache.GetOrCreateAsync((nameof(GetLatest), roomId, since, until, limit), async (e) =>
             {
                 e.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));
                 var t = await _db.ChatMessage.GetLatest(roomId,since, until, limit);
                 var reports = t.Select(a => LiveChatReport.Parse(a));
                 return new LiverWithReport()
                 {
                     Liver = await _bilibiliLiveApi.GetLiverName(roomId),
                     Data = reports
                 };
             });
            return Ok(r);
        }

        [HttpGet("{roomId:int}/livehistory")]
        public async Task<IActionResult> GetLiveHistory(int roomId, DateTime start, DateTime end, int hour, [Range(1, 100)] int limit = 50)
        {
            roomId = await _bilibiliLiveApi.GetRealRoomId(roomId);
            var liver = await _bilibiliLiveApi.GetLiverName(roomId);
            if (liver == null)
            {
                return NotFound();
            }
            var now = DateTime.Now;
            if (end == default)
            {
                if (hour > 0 && start != default)
                {
                    end = start.AddHours(hour);
                }
                else
                {
                    end = new DateTime(now.Ticks - ((now.Ticks % 1000) * TimeSpan.TicksPerMillisecond));
                }
            }
            if (start == default)
            {
                if (hour < 0 && end != default)
                {
                    start = end.AddHours(hour);
                }
                else
                {
                    start = now.Date;
                }
            }
            var list = await _db.ChatMessage.GetRoomDataPerLive(roomId, start, end);
            var r = list.Take(limit).Select(a => LiveChatReport.Parse(a));
            return Ok(new LiverWithReport() { Liver = await _bilibiliLiveApi.GetLiverName(roomId), Data = r });
        }
        [HttpGet("{roomId:int}")]
        public async Task<IActionResult> Get(int roomId, DateTime start, DateTime end, int hour, [Range(1, 100)] int limit = 50)
        {
            roomId = await _bilibiliLiveApi.GetRealRoomId(roomId);
            var liver = await _bilibiliLiveApi.GetLiverName(roomId);
            if (liver == null)
            {
                return NotFound();
            }
            var now = DateTime.Now;
            if (end == default)
            {
                if (hour > 0 && start != default)
                {
                    end = start.AddHours(hour);
                }
                else
                {
                    end = new DateTime(now.Ticks - ((now.Ticks % 1000) * TimeSpan.TicksPerMillisecond));
                }
            }
            if (start == default)
            {
                if (hour < 0 && end != default)
                {
                    start = end.AddHours(hour);
                }
                else
                {
                    start = now.Date;
                }
            }
            var list = await _db.ChatMessage.GetRoomData(start, end, roomId);
            var d = list.Aggregate(true);
            var r = Enumerable.Empty<LiveChatReport>();
            if (d != null)
            {
                r = r.Append(LiveChatReport.Parse(d));
            }
            return Ok(new LiverWithReport() { Liver = await _bilibiliLiveApi.GetLiverName(roomId), Data = r });
        }
        [HttpGet("{roomId:int}/income")]
        public async Task<IActionResult> GetIncome(int roomId, DateTime start, DateTime end, int top = 50)
        {
            roomId = await _bilibiliLiveApi.GetRealRoomId(roomId);
            var liver = await _bilibiliLiveApi.GetLiverName(roomId);
            if (liver == null)
            {
                return NotFound();
            }
            var now = DateTime.Now;
            if (end == default)
            {
                end = new DateTime(now.Ticks - ((now.Ticks % 1000) * TimeSpan.TicksPerMillisecond));

            }
            if (start == default)
            {
                start = now.Date;
            }
            var r = await _db.ChatMessage.GetGoldCoinGroupByUid(start, end, roomId);
            var list = r.Take(top).ToList();
            var users = (await _bilibiliLiveApi.GetUserInfo(list.Select(a => a.Uid))).ToDictionary(a => a.Uid);
            return Ok(new
            {
                Liver = await _bilibiliLiveApi.GetLiverName(roomId),
                Income = new { Total = r.Sum(a => a.Gold), Detail = new { Top = top, List = list.Select(a => new { GoldCoin = a.Gold, User = users[a.Uid] }) } }
            });

        }
    }
}
