using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Darkflame.BilibiliLiveApi
{
    public class UserInfo
    {
        public UserInfo(long uid, string name, string sex, int level, string face)
        {
            Uid = uid;
            Name = name;
            Sex = sex;
            Level = level;
            Face = face;
        }
        public long Uid { get; set; }
        public string Name { get; set; }
        public string Sex { get; set; }
        public int Level { get; set; }
        public string Face { get; set; }
    }
}
