using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Api
{
    public class QueryOptions
    {
        public QueryOptions()
        {
            ExcludeRooms = new HashSet<int>();
        }

        public HashSet<int> ExcludeRooms { get; set; } 
    }
}
