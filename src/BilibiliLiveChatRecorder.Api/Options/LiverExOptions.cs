using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Options
{
    public class LiverExOptions :LiverOptions
    {
        public Dictionary<string, Organization> OrganizationsDic { get; set; } = new Dictionary<string, Organization>();
        public Dictionary<int, Liver> LiversDic { get; set; } = new Dictionary<int, Liver>();
    }
}
