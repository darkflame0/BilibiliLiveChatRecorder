using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Models
{
    public class LiveRankingItem
    {
        public LiveRankingItem()
        {

        }



        public LiverModel Liver { get; set; } = new LiverModel();
        public LiveRankingItemData Data { get; set; } = default!;

    }
    public class LiveRankingItemData
    {
        public LiveRankingItemData()
        {
        }

        public LiveRankingItemData(RoomData data)
        {
            Title = data.Title;
            Cover = data.Cover;
            HourOfLive = (data.EndTime - data.StartTime).TotalHours;
            StartTime = data.StartTime;
            RealDanmaku = data.RealDanmaku;
            GoldCoin = data.GoldCoin;
            MaxPopularity = data.MaxPopularity;
            GoldUser = data.GoldUser.Count;
            GoldUserGreaterThen9 = data.GoldUser.Count(a => a.Value > 9000);
            GoldUserGreaterThen99 = data.GoldUser.Count(a => a.Value > 99000);
            RealDanmakuUser = data.RealDanmakuUser.Count;
            SilverUser = data.SilverUser.Count;
            Participants = data.Participants.Count;
            FansIncrement = data.FansIncrement;
        }
        public string? Title { get; set; }
        public string? Cover { get; set; }
        public DateTime StartTime { get; set; }
        public double HourOfLive { get; set; }
        public int RealDanmaku { get; set; }
        public long GoldCoin { get; set; }
        public int MaxPopularity { get; set; }
        public int GoldUser { get; set; }
        public int GoldUserGreaterThen9 { get; set; }
        public int GoldUserGreaterThen99 { get; set; }
        public int RealDanmakuUser { get; set; }
        public int SilverUser { get; set; }
        public int Participants { get; set; }
        public int? FansIncrement { get; set; }
    }
}
