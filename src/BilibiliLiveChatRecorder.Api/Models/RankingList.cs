using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Models
{
    public abstract class RankingList
    {
        public static RankingList<T> Create<T>(DateTime updateTime, int top, IEnumerable<T> list)
        {
            return new RankingList<T>(updateTime, top, list);
        }
    }
    public class RankingList<T>
    {
        public RankingList(DateTime updateTime, int top, IEnumerable<T> list)
        {
            UpdateTime = updateTime;
            Count = top;
            List = list;
        }
        public DateTime UpdateTime { get; set; }
        public int Count { get; set; }
        public IEnumerable<T> List { get; set; } = Enumerable.Empty<T>();
    }
}
