using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    [Owned]
    public class RoomData
    {
        public static readonly TimeSpan LiveInterval = TimeSpan.FromMinutes(5);
        public static readonly HashSet<string> Cmds = new HashSet<string>() { Cmd.SendGift, Cmd.SUPER_CHAT_MESSAGE, Cmd.USER_TOAST_MSG, Cmd.Danmaku, Cmd.Popularity, Cmd.RoomRealTimeMessageUpdate, Cmd.RoomInfo, Cmd.LiveStart, Cmd.LiveEnd };
        private HashSet<long>? _participants;
        private HashSet<long> _viewer = new ();

        public RoomData()
        {
        }

        public RoomData(DateTime startTime, DateTime endTime, int? popularity, int maxPopularity, int realDanmaku, int giftDanmaku, long goldCoin, long silverCoin, IEnumerable<KeyValuePair<long, int>> goldUser, IEnumerable<KeyValuePair<long, int>> realDanmakuUser, IEnumerable<long> silverUser, IEnumerable<long> giftDanmakuUser, IEnumerable<long> viewer, int fansIncrement = 0)
        {
            StartTime = startTime;
            EndTime = endTime;
            Popularity = popularity;
            MaxPopularity = maxPopularity;
            RealDanmaku = realDanmaku;
            GiftDanmaku = giftDanmaku;
            GoldCoin = goldCoin;
            GoldUser = new Dictionary<long, int>(goldUser ?? Enumerable.Empty<KeyValuePair<long, int>>());
            RealDanmakuUser = new Dictionary<long, int>(realDanmakuUser ?? Enumerable.Empty<KeyValuePair<long, int>>());
            SilverUser.UnionWith(silverUser ?? Enumerable.Empty<long>());
            GiftDanmakuUser.UnionWith(giftDanmakuUser ?? Enumerable.Empty<long>());
            Viewer.UnionWith(viewer ?? Enumerable.Empty<long>());
            FansIncrement = fansIncrement;
            SilverCoin = silverCoin;
        }

        [Column(name: nameof(RoomId))]
        public int RoomId { get; set; }

        [Column(name: nameof(Title))]
        public string? Title { get; set; }
        [Column(name: nameof(Cover))]
        public string? Cover { get; set; }
        [Column(name: nameof(Area))]
        public string? Area { get; set; }

        [Column(name: nameof(StartTime))]
        public DateTime StartTime { get; set; }

        [Column(name: nameof(EndTime))]
        public DateTime EndTime { get; set; }

        [Column(name: nameof(Popularity))]
        [NotMapped]
        public int? Popularity { get; set; }

        [Column(name: nameof(MaxPopularity))]
        public int MaxPopularity { get; set; }

        [Column(name: nameof(GoldUser), TypeName = "jsonb")]
        public IDictionary<long, int> GoldUser { get; private set; } = new Dictionary<long, int>();

        [Column(name: nameof(RealDanmakuUser), TypeName = "jsonb")]
        public IDictionary<long, int> RealDanmakuUser { get; private set; } = new Dictionary<long, int>();

        [Column(name: nameof(SilverUser), TypeName = "jsonb")]
        public HashSet<long> SilverUser { get; private set; } = new ();

        [Column(name: nameof(GiftDanmakuUser), TypeName = "jsonb")]
        public HashSet<long> GiftDanmakuUser { get; private set; } = new ();
        [Column(name: nameof(Viewer), TypeName = "jsonb")]
        public HashSet<long> Viewer { get => _viewer ??= new HashSet<long>(); private set => _viewer = value; }
        [Column(name: nameof(Participants), TypeName = "jsonb")]
        public HashSet<long> Participants { get => _participants ??= ResetParticipants(); private set => _participants = value; }

        [Column(name: nameof(RealDanmaku))]
        public int RealDanmaku { get; set; }

        [Column(name: nameof(GiftDanmaku))]
        public int GiftDanmaku { get; set; }

        [Column(name: nameof(GoldCoin))]
        public long GoldCoin { get; set; }

        [Column(name: nameof(SilverCoin))]
        public long SilverCoin { get; set; }

        [Column(name: nameof(FansIncrement))]
        public int FansIncrement { get; set; }

        public HashSet<long> ResetParticipants()
        {
            _participants = new HashSet<long>(RealDanmakuUser.Keys);
            _participants.UnionWith(GoldUser.Keys);
            _participants.UnionWith(SilverUser);
            return _participants;
        }
        public void Union(RoomData data, bool singleRoom)
        {
            if (singleRoom)
            {
                if (MaxPopularity < data.MaxPopularity)
                {
                    MaxPopularity = data.MaxPopularity;
                }
                Popularity = data.Popularity;
                FansIncrement += data.FansIncrement;
                if (!string.IsNullOrEmpty(data.Title))
                {
                    Title = data.Title;
                }
                if (!string.IsNullOrEmpty(data.Cover))
                {
                    Cover = data.Cover;
                }
                if (!string.IsNullOrEmpty(data.Area))
                {
                    Area = data.Area;
                }
            }
            if (EndTime < data.EndTime)
            {
                EndTime = data.EndTime;
            }
            if (StartTime > data.StartTime)
            {
                StartTime = data.StartTime;
            }
            RealDanmaku += data.RealDanmaku;
            GiftDanmaku += data.GiftDanmaku;
            GoldCoin += data.GoldCoin;
            SilverCoin += data.SilverCoin;
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
            GiftDanmakuUser.UnionWith(data.GiftDanmakuUser);
            Participants.UnionWith(data.Participants);
            Viewer.UnionWith(data.Viewer);
        }
    }
}
