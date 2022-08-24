using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel.QueryEntities
{
    internal class RoomLiveTime
    {
        public int RoomId { get; set; }
        public DateTime Time { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime LatestEndTime { get; set; }
    }
}
