using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Options
{
    public class LiverOptions
    {
        public IList<Liver> Livers { get; set; } = new List<Liver>();
        public IList<Organization> Organizations { get; set; } = new List<Organization>();
    }

    public class Liver
    {
        public long Uid { get; set; }
        public int RoomId { get; set; }
        public bool Keep { get; set; }
        public bool Retire { get; set; }
        public IList<string> Organization { get; set; } = new List<string>();
    }

    public class Organization
    {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public int? RoomId { get; set; }
        public long? Uid { get; set; }
        public string Face { get; set; } = "https://static.hdslb.com/images/member/noface.gif";
        public List<Liver> Livers { get; set; } = new();
    }
}



