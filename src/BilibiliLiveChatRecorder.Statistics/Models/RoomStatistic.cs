using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel;

namespace Darkflame.BilibiliLiveChatRecorder.Statistics.Models
{
    public class RoomStatistic
    {
        public RoomStatistic(RoomData room)
        {
            this.Set(room);
        }
        public RoomStatistic()
        {
        }

        public string Title { get; private set; } = "";
        public string StartTime { get; private set; } = "";
        public string EndTime { get; private set; } = "";
        public int Popularity { get; private set; }
        public int MaxPopularity { get; private set; }
        public int GoldUser { get; private set; }
        public int DanmakuUser { get; private set; }
        public int SilverUser { get; private set; }
        public int GiftDanmakuUser { get; private set; }
        public int RealDanmaku { get; private set; }
        public int GiftDanmaku { get; private set; }
        public long GoldCoin { get; private set; }
        public long SilverCoin { get; private set; }
        public int FansIncrement { get; private set; }
        public int Participants { get; private set; }
        public string Cover { get; private set; } = "";
        public string Area { get; private set; } = "";
        public int Viewer { get; private set; }

        public RoomStatistic Set(RoomData room)
        {
            Title = room.Title ?? "";
            StartTime = room.StartTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
            EndTime = room.EndTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Popularity = room.Popularity.GetValueOrDefault();
            MaxPopularity = room.MaxPopularity;
            GoldUser = room.GoldUser.Count;
            DanmakuUser = room.RealDanmakuUser.Count;
            SilverUser = room.SilverUser.Count;
            GiftDanmakuUser = room.GiftDanmakuUser.Count;
            RealDanmaku = room.RealDanmaku;
            GiftDanmaku = room.GiftDanmaku;
            GoldCoin = room.GoldCoin;
            SilverCoin = room.SilverCoin;
            FansIncrement = room.FansIncrement;
            Participants = room.Participants.Count;
            Cover = room.Cover ?? "";
            Area = room.Area ?? "";
            Viewer = room.Viewer.Count;
            return this;
        }

    }
}
