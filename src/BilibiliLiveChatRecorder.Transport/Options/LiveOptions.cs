using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Transport
{
    public class LiveOptions
    {
        public HashSet<string> IgnoreCmds { get; set; } = new HashSet<string>();
        public HashSet<string> IgnoreWhenOffline { get; set; } = new HashSet<string>();
        public TimeSpan OfflineTimeount { get; set; } = TimeSpan.FromMinutes(5);
        public bool Wss { get; set; } = false;
        public int HeartbeatInterval { get; set; } = 30;
        public int HeartbeatTimeout { get; set; } = 5;
        public int HeartbeatRetry { get; set; } = 1;
        public int StayTimeout { get; set; } = 90;
        public string[] ServerLocation { get; set; } = Array.Empty<string>();
    }
}
