using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public class AggregateData
    {
        [Column(name: nameof(DurationOfLive), TypeName = "interval")]
        public TimeSpan DurationOfLive { get; set; }

        [Column(name: nameof(RealDanmaku))]
        public int RealDanmaku { get; set; }

        [Column(name: nameof(GoldCoin))]
        public long GoldCoin { get; set; }

        [Column(name: nameof(MaxPopularity))]
        public int MaxPopularity { get; set; }

        [Column(name: nameof(GoldUser), TypeName = "jsonb")]
        public IDictionary<long, int> GoldUser { get; private set; } = new Dictionary<long, int>();

        [Column(name: nameof(RealDanmakuUser), TypeName = "jsonb")]
        public IDictionary<long, int> RealDanmakuUser { get; private set; } = new Dictionary<long, int>();

        [Column(name: nameof(SilverUser), TypeName = "jsonb")]
        public HashSet<long> SilverUser { get; private set; } = new ();

        [Column(name: nameof(Participants), TypeName = "jsonb")]
        public HashSet<long> Participants { get; private set; } = new ();

        [NotMapped]
        public int? RoomId => DailyData?.RoomId ?? MonthlyData?.RoomId;
        [NotMapped]
        public DailyData? DailyData { get; set; }
        [NotMapped]
        public MonthlyData? MonthlyData { get; set; }
        public void Clear()
        {
            GoldUser.Clear();
            GoldUser = null!;
            RealDanmakuUser.Clear();
            RealDanmakuUser = null!;
            SilverUser.Clear();
            SilverUser = null!;
            Participants.Clear();
            Participants = null!;
        }
        public AggregateData Union(AggregateData data)
        {
            DurationOfLive += data.DurationOfLive;
            RealDanmaku += data.RealDanmaku;
            GoldCoin += data.GoldCoin;
            if (MaxPopularity < data.MaxPopularity)
            {
                MaxPopularity = data.MaxPopularity;
            }
            foreach (var kv in data.GoldUser)
            {
                if (GoldUser.TryGetValue(kv.Key, out var _))
                {
                    GoldUser[kv.Key] += kv.Value;
                }
                else
                {
                    GoldUser[kv.Key] = kv.Value;
                }
            }
            foreach (var kv in data.RealDanmakuUser)
            {
                if (RealDanmakuUser.TryGetValue(kv.Key, out var _))
                {
                    RealDanmakuUser[kv.Key] += kv.Value;
                }
                else
                {
                    RealDanmakuUser[kv.Key] = kv.Value;
                }
            }
            SilverUser.UnionWith(data.SilverUser);
            Participants.UnionWith(data.Participants);
            return this;
        }

        public AggregateData Union(IEnumerable<AggregateData> data)
        {
            foreach (var item in data)
            {
                Union(item);
            }
            return this;
        }
    }
}
