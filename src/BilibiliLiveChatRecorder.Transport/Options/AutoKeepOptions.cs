using System;
using System.Collections.Generic;
using System.Text;

namespace Darkflame.BilibiliLiveChatRecorder.Transport
{
    public class AutoKeepOptions
    {
        public bool Enable { get; set; }
        public int LongThreshold { get; set; } = 50000;
        public int NormalThreshold { get; set; } = 10000;
        public int ShortThreshold { get; set; } = 5000;
        public int Reload { get; set; }
        public TimeSpan LongActiveTime { get; set; } = TimeSpan.FromDays(90);
        public TimeSpan LongLastLiveTime { get; set; } = TimeSpan.FromDays(90);
        public TimeSpan NormalActiveTime { get; set; } = TimeSpan.FromDays(60);
        public TimeSpan NormalLastLiveTime { get; set; } = TimeSpan.FromDays(60);
        public TimeSpan LongDelay { get; set; } = TimeSpan.FromDays(90);
        public TimeSpan NormalDelay { get; set; } = TimeSpan.FromDays(30);
        public TimeSpan ShortDelay { get; set; } = TimeSpan.FromDays(3);
        public HashSet<int> Exclude { get; set; } = new HashSet<int>();
    }
}
