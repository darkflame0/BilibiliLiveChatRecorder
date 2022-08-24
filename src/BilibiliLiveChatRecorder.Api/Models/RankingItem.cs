using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Models
{
    public class RankingItem
    {
        public RankingItem()
        {

        }



        public LiverModel Liver { get; set; } = new LiverModel();
        public RankingItemData Data { get; set; } = default!;

    }
    public class RankingItemData
    {
        public RankingItemData()
        {
        }

        public RankingItemData(AggregateData data)
        {
            HourOfLive = data.DurationOfLive.TotalHours;
            RealDanmaku = data.RealDanmaku;
            GoldCoin = data.GoldCoin;
            MaxPopularity = data.MaxPopularity;
            GoldUser = data.GoldUser.Count;
            GoldUserGreaterThen9 = data.GoldUser.Count(a => a.Value > 9000);
            GoldUserGreaterThen99 = data.GoldUser.Count(a => a.Value > 99000);
            RealDanmakuUser = data.RealDanmakuUser.Count;
            SilverUser = data.SilverUser.Count;
            Participants = data.Participants.Count;
            LastLiveTime = data.DailyData?.LastLiveStartTime ?? data.MonthlyData?.LastLiveStartTime ?? default;
        }

        public RankingItemData(double hourOfLive, DateTime lastLiveTime, int realDanmaku, long goldCoin, int maxPopularity, int goldUser, int realDanmakuUser, int silverUser, int participants)
        {
            HourOfLive = hourOfLive;
            RealDanmaku = realDanmaku;
            GoldCoin = goldCoin;
            MaxPopularity = maxPopularity;
            GoldUser = goldUser;
            RealDanmakuUser = realDanmakuUser;
            SilverUser = silverUser;
            Participants = participants;
            LastLiveTime = lastLiveTime;
        }
        public double HourOfLive { get; set; }
        public DateTime LastLiveTime { get; set; }
        public int RealDanmaku { get; set; }
        public long GoldCoin { get; set; }
        public int MaxPopularity { get; set; }
        public int GoldUser { get; set; }
        public int GoldUserGreaterThen9 { get; set; }
        public int GoldUserGreaterThen99 { get; set; }
        public int RealDanmakuUser { get; set; }
        public int SilverUser { get; set; }
        public int Participants { get; set; }
    }
}
