using System;
using System.Collections.Generic;
using System.Linq;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public static class RoomDataExtensions
    {
        public static RoomData? Aggregate(this IEnumerable<RoomData> data, bool singleRoom = false)
        {
            if (data.Count() > 1)
            {
                var r = new RoomData
                {
                    StartTime = data.First().StartTime,
                };
                if (singleRoom)
                {
                    r.RoomId = data.First().RoomId;
                }

                foreach (var item in data)
                {
                    r.Union(item, singleRoom);
                }
                return r;
            }
            return data.FirstOrDefault();
        }
        public static IEnumerable<RoomData> AggregateByPerLive(this IEnumerable<RoomData> data, bool includeNotLive = false)
        {
            if (!data.Any())
            {
                return Enumerable.Empty<RoomData>();
            }

            var list = data.ToList();
            var tempList = new List<RoomData>();
            var r = new List<RoomData>();
            var i = 0;
            DateTime lastTime = default;
            while (i < list.Count)
            {
                if (!tempList.Any())
                {
                    if (!string.IsNullOrEmpty(list[i].Title) || list[i].MaxPopularity != 0)
                    {
                        tempList.Add(list[i]);
                        lastTime = list[i].EndTime;
                    }
                    else if (includeNotLive)
                    {
                        r.Add(list[i]);
                    }
                }
                else
                {
                    if ((list[i].StartTime - lastTime) > RoomData.LiveInterval)
                    {
                        var t = tempList.Aggregate(true);
                        if (t!.Title == null && r.Any() && r.Last().MaxPopularity != 0 && (t.StartTime - r.Last().EndTime).TotalHours < 1)
                        {
                            r.Last().Union(t, true);
                        }
                        else
                        {
                            r.Add(t);
                        }
                        tempList.Clear();
                        continue;
                    }
                    else
                    {
                        tempList.Add(list[i]);
                    }
                    if (list[i].MaxPopularity != 0)
                    {
                        lastTime = list[i].EndTime;
                    }
                }
                ++i;
            }
            if (tempList.Any())
            {
                var t = tempList.Aggregate(true);
                if (t!.Title == null && r.Any() && (t.StartTime - r.Last().EndTime).TotalHours < 1)
                {
                    r.Last().Union(t, true);
                }
                else
                {
                    r.Add(t);
                }
            }
            return r;
        }
    }
}
