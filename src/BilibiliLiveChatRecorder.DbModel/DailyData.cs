using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public class DailyData
    {

        public int Id { get; set; }
        public int RoomId { get; set; }
        [Column(TypeName = "date")]
        public DateTime Date { get; set; }
        public DateTime UpdateTime { get; set; }
        public DateTime LastLiveStartTime { get; set; }
        public DateTime LastLiveEndTime { get; set; }

        public AggregateData Data { get; set; } = new AggregateData();
        public void Union(IEnumerable<HourlyData> data)
        {
            foreach (var item in data.OrderBy(a => a.Time).Select(a => a.Data).AggregateByPerLive(true))
            {
                if (item.RoomId != RoomId)
                {
                    throw new InvalidOperationException("RoomId must be the same");
                }
                if (UpdateTime < item.EndTime)
                {
                    UpdateTime = item.EndTime;
                    if (item.MaxPopularity != 0 || !string.IsNullOrEmpty(item.Title))
                    {
                        var lastTime = LastLiveEndTime;
                        if ((item.StartTime - lastTime) > RoomData.LiveInterval)
                        {
                            lastTime = item.StartTime;
                            LastLiveStartTime = item.StartTime;
                        }
                        Data.DurationOfLive += TimeSpan.FromMinutes(Math.Floor((item.EndTime - lastTime).TotalMinutes));
                        LastLiveEndTime = item.EndTime;
                    }

                    Data.RealDanmaku += item.RealDanmaku;
                    Data.GoldCoin += item.GoldCoin;
                    if (Data.MaxPopularity < item.MaxPopularity)
                    {
                        Data.MaxPopularity = item.MaxPopularity;
                    }
                    foreach (var kv in item.GoldUser)
                    {
                        if (Data.GoldUser.TryGetValue(kv.Key, out var _))
                        {
                            Data.GoldUser[kv.Key] += kv.Value;
                        }
                        else
                        {
                            Data.GoldUser[kv.Key] = kv.Value;
                        }
                    }
                    foreach (var kv in item.RealDanmakuUser)
                    {
                        if (Data.RealDanmakuUser.TryGetValue(kv.Key, out var _))
                        {
                            Data.RealDanmakuUser[kv.Key] += kv.Value;
                        }
                        else
                        {
                            Data.RealDanmakuUser[kv.Key] = kv.Value;
                        }
                    }
                    Data.SilverUser.UnionWith(item.SilverUser);
                    Data.Participants.UnionWith(item.Participants);
                }
            }
        }
    }
}
