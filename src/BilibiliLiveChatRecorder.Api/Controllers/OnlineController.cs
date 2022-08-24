using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Darkflame.BilibiliLiveApi;
using Darkflame.BilibiliLiveChatRecorder.Api.Filters;
using Microsoft.Extensions.Options;
using Darkflame.BilibiliLiveChatRecorder.Api.HttpApis;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ResponseCache(Duration = 20, VaryByQueryKeys = new[] { "*" })]
    public class OnlineController : ControllerBase
    {
        private readonly IOptionsMonitor<QueryOptions> _queryOptions;
        private readonly IMemoryCache _cache;
        private readonly IMapper _mapper;
        private readonly IBilibiliLiveApi _bilibiliLiveApi;
        private readonly IBackgroundApi _backgroundApi;
        private readonly IStatisticsApi _statisticsApi;

        public OnlineController(IOptionsMonitor<QueryOptions> queryOptions, IMemoryCache cache, IMapper mapper, IBilibiliLiveApi bilibiliLiveApi, IBackgroundApi backgroundApi, IStatisticsApi statisticsApi)
        {
            _queryOptions = queryOptions;
            _cache = cache;
            _mapper = mapper;
            _bilibiliLiveApi = bilibiliLiveApi;
            _backgroundApi = backgroundApi;
            _statisticsApi = statisticsApi;
        }
        [Polly("OnlineCircuitBreaker")]
        [HttpGet]
        public async Task<IActionResult> GetRooms(string? sortby = "", bool all = false, bool? connected = null, string? area = "", [FromQuery(Name = "roomId")] int[]? roomIds = null, bool? host = false)
        {
            try
            {
                var r = await _cache.GetOrCreateAtomicAsync((nameof(GetRooms), Request.QueryString), async e =>
                {
                    var respT = _backgroundApi.GetRooms(all);
                    IReadOnlyDictionary<long, int>? p10MinMap = null;
                    try
                    {
                        p10MinMap = await _statisticsApi.GetParticipantDuring10Min();
                    }
                    catch
                    {
                    }
                    finally
                    {
                        p10MinMap ??= new Dictionary<long, int>(0);
                    }
                    var rooms = (await respT);
                    e.SetAbsoluteExpiration(TimeSpan.FromSeconds(20));
                    if (!all)
                    {
                        rooms = rooms.Where(a => !(_queryOptions.CurrentValue.ExcludeRooms.Contains(a.RoomId) && a.ParentArea != "虚拟主播"));
                    }
                    if (roomIds?.Any() ?? false)
                    {
                        e.SetAbsoluteExpiration(TimeSpan.FromSeconds(1));
                        rooms = rooms.Where(a => roomIds.Contains(a.RoomId));
                    }
                    if (connected.HasValue)
                    {
                        rooms = rooms.Where(a => a.Connected == connected);
                    }
                    if (area?.Length > 0)
                    {
                        if (area == "other")
                        {
                            rooms = rooms.Where(a => a.ParentArea != "虚拟主播");
                        }
                        else
                        {
                            rooms = rooms.Where(a => a.ParentArea == area || a.Area == area);
                        }
                    }
                    var mapFunc = (Models.FullRoomInfo a) => _mapper.Map<Models.RoomInfo>(a);
                    if (host == true)
                    {
                        mapFunc = (Models.FullRoomInfo a) => _mapper.Map<Models.RoomInfoWithHost>(a);
                    }
                    var list = rooms.Select(a =>
                    {
                        var d = mapFunc(a);
                        d.ParticipantDuring10Min = p10MinMap.GetValueOrDefault(a.RoomId);
                        return d;
                    });
                    list = sortby?.ToLower() switch
                    {
                        "roomid" => list.OrderBy(a => a.RoomId),
                        "livetime" => list.OrderByDescending(a => a.LiveTime),
                        _ => list.OrderByDescending(a => a.ParticipantDuring10Min).ThenByDescending(a => a.Popularity)
                    };
                    var r = list.ToList();
                    return new
                    {
                        CreateTicks = DateTime.UtcNow.Ticks,
                        Data = new
                        {
                            Count = r.Count,
                            List = r
                        }
                    };
                }, a => (DateTime.UtcNow.Ticks - a.CreateTicks) < 10 * TimeSpan.TicksPerSecond, waitTime: TimeSpan.FromMilliseconds(100));
                return Ok(r.Data);
            }
            catch (OperationCanceledException)
            {
                return Forbid();
            }
        }
        [HttpGet("count")]
        public async Task<IActionResult> GetRoomsCount(bool all = false, bool? connected = null, string? area = "")
        {
            try
            {
                var r = await _cache.GetOrCreateAtomicAsync((nameof(GetRooms), Request.QueryString), async e =>
                {
                    var respT = _backgroundApi.GetRooms(all);
                    var rooms = (await respT);
                    e.SetAbsoluteExpiration(TimeSpan.FromSeconds(5));
                    if (!all)
                    {
                        rooms = rooms.Where(a => !(_queryOptions.CurrentValue.ExcludeRooms.Contains(a.RoomId) && a.ParentArea != "虚拟主播"));
                    }
                    if (connected.HasValue)
                    {
                        rooms = rooms.Where(a => a.Connected == connected);
                    }
                    if (area?.Length > 0)
                    {
                        if (area == "other")
                        {
                            rooms = rooms.Where(a => a.ParentArea != "虚拟主播");
                        }
                        else
                        {
                            rooms = rooms.Where(a => a.ParentArea == area || a.Area == area);
                        }
                    }
                    var r = rooms.ToList();
                    return new
                    {
                        CreateTicks = DateTime.UtcNow.Ticks,
                        Data = new
                        {
                            Count = r.Count,
                        }
                    };
                }, a => (DateTime.UtcNow.Ticks - a.CreateTicks) < 10 * TimeSpan.TicksPerSecond, waitTime: TimeSpan.FromMilliseconds(100));
                return Ok(r.Data);
            }
            catch (OperationCanceledException)
            {
                return Forbid();
            }
        }
        [HttpGet("{roomId:int}")]
        public async Task<IActionResult> GetRoom(int roomId)
        {
            roomId = await _bilibiliLiveApi.GetRealRoomId(roomId);
            var data = await _statisticsApi.GetRoom(roomId);
            if (data == null)
            {
                return NotFound();
            }
            return Ok(new
            {
                Liver = await _bilibiliLiveApi.GetLiverName(roomId),
                data
            });
        }

        [HttpGet("{roomId:int}/income")]
        public async Task<IActionResult> GetIncome(int roomId, int top = 50)
        {
            roomId = await _bilibiliLiveApi.GetRealRoomId(roomId);
            var r = await _statisticsApi.GetIncome(roomId);
            var list = r.OrderByDescending(a => a.Value).Take(top).ToList();
            var users = (await _bilibiliLiveApi.GetUserInfo(list.Select(a => a.Key))).ToDictionary(a => a.Uid);
            return Ok(new
            {
                Liver = await _bilibiliLiveApi.GetLiverName(roomId),
                Income = new { Total = r.Sum(a => a.Value), Detail = new { Top = top, List = list.Select(a => new { GoldCoin = a.Value, User = users[a.Key] }) } }
            });
        }
    }
}
