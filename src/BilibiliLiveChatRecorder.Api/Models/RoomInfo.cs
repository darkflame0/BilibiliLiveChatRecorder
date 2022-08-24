using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Models
{
    public class RoomInfo
    {
        public int RoomId { get; set; }
        public int ShortId { get; set; }
        public long Uid { get; set; }
        public string Uname { get; set; } = "";
        public int Popularity { get; set; }
        public string Title { get; set; } = "";
        public string Area { get; set; } = "";
        public string ParentArea { get; set; } = "";
        public string UserCover { get; set; } = "";
        public string Keyframe { get; set; } = "";
        public DateTime LiveTime { get; set; }
        public int ParticipantDuring10Min { get; set; }
    }
    public class RoomInfoWithHost : RoomInfo
    {
        public string? Host { get; set; }
    }
    public class FullRoomInfo
    {
        public int RoomId { get; set; }
        public int ShortId { get; set; }
        public long Uid { get; set; }
        public string Uname { get; set; } = "";
        public int Popularity { get; set; }
        public string Title { get; set; } = "";
        public string Area { get; set; } = "";
        public string ParentArea { get; set; } = "";
        public string UserCover { get; set; } = "";
        public string Keyframe { get; set; } = "";
        public DateTime LiveTime { get; set; }
        public string Host { get; set; } = "";
        public bool Connected { get; set; }
    }
}
