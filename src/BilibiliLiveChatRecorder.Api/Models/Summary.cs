using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Darkflame.BilibiliLiveChatRecorder.Options;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Models
{
    public class SummaryItem
    {
        public SummaryItem()
        {

        }



        public LiverModel Liver { get; set; } = new LiverModel();
        public Summary Data { get; set; } = default!;

    }
    public class Summary
    {
        public Summary()
        {
        }

        public Summary(AggregateData data)
        {
            HourOfLive = data.DurationOfLive.TotalHours;
            RealDanmaku = data.RealDanmaku;
            RealDanmakuUser = data.RealDanmakuUser.Count;
            GoldCoin = data.GoldCoin;
            GoldUser = data.GoldUser.Count;
            SilverUser = data.SilverUser.Count;
            Participants = data.Participants.Count;
            GoldUserGreaterThen9 = data.GoldUser.Count(a => a.Value > 9000);
        }
        public double HourOfLive { get; set; }
        public int RealDanmaku { get; set; }
        public long GoldCoin { get; set; }
        public int GoldUser { get; set; }
        public int GoldUserGreaterThen9 { get; set; }
        public int RealDanmakuUser { get; set; }
        public int SilverUser { get; set; }
        public int Participants { get; set; }

        public static IEnumerable<(string Key, bool IsOrganization, Summary Data)> Parse(IEnumerable<AggregateData> data, LiverExOptions options)
        {
            var tagDic = new Dictionary<string, (AggregateData Data, bool isOrganization)>();
            foreach (var item in data)
            {
                options.LiversDic.TryGetValue(item.RoomId!.Value, out var liver);
                if (liver?.Organization.Any() ?? false)
                {
                    foreach (var orgName in liver.Organization)
                    {
                        if (options.OrganizationsDic.ContainsKey(orgName))
                        {
                            if (!tagDic.TryGetValue(orgName, out var val))
                            {
                                val = (new AggregateData(), true);
                                tagDic.Add(orgName, val);
                            }
                            val.Data.Union(item);
                        }
                    }
                }
                else
                {
                    if (!tagDic.TryGetValue(item.RoomId!.ToString()!, out var val))
                    {
                        val = (new AggregateData(), false);
                        tagDic.Add(item.RoomId!.ToString()!, val);
                    }
                    val.Data.Union(item);
                }
            }
            return tagDic.Select(a => (a.Key, a.Value.isOrganization, new Summary(a.Value.Data)));
        }
        public static async Task<IEnumerable<(string Key, bool IsOrganization, Summary Data)>> Parse(IAsyncEnumerable<MonthlyData> data, LiverExOptions options)
        {
            var tagDic = new Dictionary<string, (AggregateData Data, bool isOrganization)>();
            await foreach (var item in data)
            {
                options.LiversDic.TryGetValue(item.RoomId, out var liver);
                if (liver?.Organization.Any() ?? false)
                {
                    foreach (var orgName in liver.Organization)
                    {
                        if (options.OrganizationsDic.ContainsKey(orgName))
                        {
                            if (!tagDic.TryGetValue(orgName, out var val) && options.OrganizationsDic.ContainsKey(orgName))
                            {
                                val = (new AggregateData(), true);
                                tagDic.Add(orgName, val);
                            }
                            val.Data.Union(item.Data);
                        }
                    }
                }
                else
                {
                    if (!tagDic.TryGetValue(item.RoomId!.ToString()!, out var val))
                    {
                        val = (new AggregateData(), false);
                        tagDic.Add(item.RoomId!.ToString()!, val);
                    }
                    val.Data.Union(item.Data);
                }
                item.Data = null!;

            }
            return tagDic.Select(a => (a.Key, a.Value.isOrganization, new Summary(a.Value.Data)));
        }
        public static Summary Parse(IEnumerable<AggregateData> data)
        {
            return new Summary(data.Aggregate((a, b) => a.Union(b)));
        }
        public static async Task<Summary> Parse(IAsyncEnumerable<MonthlyData> data)
        {
            var r = new AggregateData();
            await foreach (var item in data)
            {
                r.Union(item.Data);
                item.Data = null!;
            }
            return new Summary(r);
        }
    }
}
