using System;
using System.Collections.Generic;
using System.Text;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public class LiveHistory
    {
        public int Id { get; set; }
        public RoomData Data { get; set; } = new RoomData();
    }
}
