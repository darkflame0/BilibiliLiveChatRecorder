using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveApi
{
    public class AreaInfo
    {
        public int AreaId { get; set; }
        public int ParentAreaId { get; set; }
        public string AreaName { get; set; } = "";
        public string ParetAreaName { get; set; } = "";
    }
}
