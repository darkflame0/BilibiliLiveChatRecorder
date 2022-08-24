using System;

namespace Darkflame.BilibiliLiveApi
{
    public class LiveInfo
    {
        public string Title { get; set; } = "";
        public string Area { get; set; } = "";
        public string ParentArea { get; set; } = "";
        public DateTime? LiveTime { get; set; }
        public Uri? UserCover { get; set; }
        public string Keyframe { get; set; } = "";
        public bool Live { get; set; }
        public int Popularity { get; }
        public string? UName { get; set; }

        public LiveInfo(string title, string area, DateTime? liveTime, string userCover, string keyframe, bool live, int popularity, string? uname = null, string parentArea = "")
        {
            Area = area;
            Title = title;
            LiveTime = liveTime;
            UserCover = string.IsNullOrEmpty(userCover) ? null : new Uri(userCover);
            Keyframe = keyframe;
            Live = live;
            Popularity = popularity;
            UName = uname;
            ParentArea = parentArea;
        }

        public LiveInfo()
        {
        }

        public LiveInfo Clone()
        {
            return new LiveInfo(Title, Area, LiveTime, UserCover?.ToString() ?? "", Keyframe, Live, Popularity, UName, ParentArea);
        }
    }
}
