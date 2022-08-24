using System;
using System.Collections.Generic;
using System.Text;

namespace Darkflame.BilibiliLiveChatRecorder.Transport
{
    public enum Operation
    {
        None = 0,
        HeartBeat = 2,
        HeartBeatResponse = 3,
        Notification = 5,
        Join = 7,
        JoinResponse = 8,
    }
}
