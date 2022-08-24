using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Background.Options
{
    public class RoomOptions
    {
        public RoomOptions()
        {
            SpecificRoom = new HashSet<int>();
            ExcludeRoom = new HashSet<int>();
        }

        public HashSet<int> SpecificRoom { get; set; }
        public HashSet<int> ExcludeRoom { get; set; }
        public int PullingKeyframeInterval { get; set; } = 60;
        public int PullingRoomsInterval { get; set; } = 5;
        public int QueueBatchSize { get; set; } = 512;
    }
}
