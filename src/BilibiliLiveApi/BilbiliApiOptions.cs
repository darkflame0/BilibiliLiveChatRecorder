using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Darkflame.BilibiliLiveApi
{
    public class BilbiliApiOptions
    {

        public int Timeout { get; set; } = 5;
        public BilbiliApiUrlOptions Urls { get; set; } = new ();
    }
}
