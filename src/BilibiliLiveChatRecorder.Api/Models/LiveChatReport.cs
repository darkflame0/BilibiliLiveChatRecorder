using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Newtonsoft.Json;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Models
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class LiveChatReport
    {
        public LiveChatReport()
        {
        }

        public LiveChatReport(DateTime startTime, DateTime endTime, int? popularity, int? maxPopularity, int viewer, int participants, int goldUser, int danmakuUser, int silverUser, int giftDanmakuUser, int realDanmaku, int giftDanmaku, long goldCoin, long silverCoin, int fansIncrement, string? title = default, string? cover = default, string? area = default)
        {
            StartTime = startTime;
            EndTime = endTime;
            Popularity = popularity;
            MaxPopularity = maxPopularity;
            GoldUser = goldUser;
            DanmakuUser = danmakuUser;
            SilverUser = silverUser;
            GiftDanmakuUser = giftDanmakuUser;
            RealDanmaku = realDanmaku;
            GiftDanmaku = giftDanmaku;
            GoldCoin = goldCoin;
            Title = title;
            Cover = cover;
            FansIncrement = fansIncrement;
            SilverCoin = silverCoin;
            Participants = participants;
            //Viewer = viewer;
            Area = area;
        }
        public string? Title { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime StartTime { get; set; }
        public int? Popularity { get; set; }
        public int? MaxPopularity { get; set; }
        //public int Viewer { get; set; }
        public int Participants { get; set; }
        public int GoldUser { get; set; }
        public int DanmakuUser { get; set; }
        public int SilverUser { get; }
        public int GiftDanmakuUser { get; }

        public int RealDanmaku { get; set; }
        public int GiftDanmaku { get; set; }
        public long GoldCoin { get; set; }
        public long SilverCoin { get; set; }
        public int? FansIncrement { get; set; }
        public string? Cover { get; set; }
        public string? Area { get; set; }
        public static LiveChatReport Parse(RoomData data)
        {
            data.GiftDanmakuUser.ExceptWith(data.Participants);
            data.Viewer.UnionWith(data.Participants);
            var r = new LiveChatReport(data.StartTime, data.EndTime, data.Popularity, data.MaxPopularity, data.Viewer.Count, data.Participants.Count, data.GoldUser.Count, data.RealDanmakuUser.Count, data.SilverUser.Count, data.GiftDanmakuUser.Count, data.RealDanmaku, data.GiftDanmaku, data.GoldCoin, data.SilverCoin, data.FansIncrement, data.Title, data.Cover, data.Area);
            if (data.RoomId == default)
            {
                r.FansIncrement = null;
                r.Popularity = null;
                r.MaxPopularity = null;
            }
            return r;
        }
    }
}
