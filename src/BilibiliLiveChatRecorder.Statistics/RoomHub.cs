using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace Darkflame.BilibiliLiveChatRecorder.Statistics
{
    public class RoomHub : Hub<IRoomClient>
    {
        private readonly StatisticsBackgroundService _backgroundService;

        public RoomHub(IEnumerable<IHostedService> hostedServices)
        {
            _backgroundService = hostedServices.OfType<StatisticsBackgroundService>().Single();
        }

        public override async Task OnConnectedAsync()
        {
            var roomId = Convert.ToInt32(Context.GetHttpContext()!.Request.Query["roomId"]);
            if (_backgroundService.TryGetRoomContext(roomId, out var context))
            {
                await Clients.Caller.ReceiveRoomData(roomId, context!.Statistics);
            }
            if (context == default || (DateTime.Now - context.LastHeartBeat) > RoomData.LiveInterval * 2)
            {
                await Clients.Caller.Disconnect();
                Context.Abort();
                return;
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var roomId = Context.GetHttpContext()!.Request.Query["roomId"];
            if (!string.IsNullOrEmpty(roomId))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        }
    }
    public interface IRoomClient
    {
        Task ReceiveRoomData(int roomId, Models.RoomStatistic room);
        Task Disconnect();
    }
}
