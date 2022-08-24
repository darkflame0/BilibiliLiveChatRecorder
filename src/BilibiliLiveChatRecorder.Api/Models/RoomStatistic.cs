using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Models
{
    public class RoomStatistic
    {
        public RoomStatistic()
        {
        }

        public string Title { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public int Popularity { get; set; }
        public int MaxPopularity { get; set; }
        public int GoldUser { get; set; }
        public int DanmakuUser { get; set; }
        public int SilverUser { get; set; }
        public int GiftDanmakuUser { get; set; }
        public int RealDanmaku { get; set; }
        public int GiftDanmaku { get; set; }
        public long GoldCoin { get; set; }
        public long SilverCoin { get; set; }
        public int FansIncrement { get; set; }
        public int Participants { get; set; }
        public string Cover { get; set; } = "";
        public string Area { get; set; } = "";
        public int Viewer { get; set; }
    }
}
