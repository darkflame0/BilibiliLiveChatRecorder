using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Models
{
    public class LiverWithReport
    {
        public string? Liver { get; set; }
        public IEnumerable<LiveChatReport> Data { get; set; } = Enumerable.Empty<LiveChatReport>();
    }
}
