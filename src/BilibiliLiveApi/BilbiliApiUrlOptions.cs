using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Darkflame.BilibiliLiveApi
{
    public class BilbiliApiUrlOptions
    {
        private IList<string> _getRoomListUrl = new List<string>() { "https://api.live.bilibili.com/room/v3/area/getRoomList?platform=web&parent_area_id=9&cate_id=0&area_id=371&sort_type=live_time&page_size=99&tag_version=1&page={0}" };

        public string GetAnchorInRoom { get; set; } = "https://api.live.bilibili.com/live_user/v1/UserInfo/get_anchor_in_room?roomid=";

        public string GetConf { get; set; } = "https://api.live.bilibili.com/room/v1/Danmu/getConf?room_id=";
        public string GetDanmuInfo { get; set; } = "https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?type=0&id=";

        public string GetRoomInfoUrl { get; set; } = "https://api.live.bilibili.com/room/v1/Room/get_info?room_id=";

        public IList<string> GetRoomListUrl
        {
            get => _getRoomListUrl;
            set
            {
                if (value.Count > 1)
                {
                    _getRoomListUrl = value.Skip(1).ToList();
                }
                else
                {
                    _getRoomListUrl = value;
                }
            }
        }
        public string GetInfoByRoomUrl { get; set; } = "https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?room_id=";

        public string GetH5InfoByRoomUrl { get; set; } = "https://api.live.bilibili.com/xlive/web-room/v1/index/getH5InfoByRoom?room_id=";
        public string UserInfoUrl { get; set; } = "https://api.bilibili.com/x/space/acc/info?mid=";

        public string GetUserCardUrl { get; set; } = "https://api.bilibili.com/x/web-interface/card?mid=";

        public string GetMultipleUserUrl { get; set; } = "https://api.live.bilibili.com/user/v3/User/getMultiple?attributes%5B%5D=info";

        public string GetLiverInfoByIdsUrl { get; set; } = "https://api.live.bilibili.com/room/v2/Room/get_by_ids?need_uinfo=1";
        public string GetLiveInfoByIdsUrl { get; set; } = "https://api.live.bilibili.com/room/v2/Room/get_by_ids?";
        public string GetRoomInitByIdsUrl { get; set; } = "https://api.live.bilibili.com/room/v1/Room/get_info_by_id?";
        public string GetRoomIdsByIdsUrl { get; set; } = "https://api.live.bilibili.com/room/v2/Room/room_id_by_uid_multi?";

        public string GetRoomIdByIdUrl { get; set; } = "https://api.live.bilibili.com/room/v2/Room/room_id_by_uid?";

        public string GetLiverInfoByUidUrl { get; set; } = "https://api.live.bilibili.com/room/v1/Room/get_status_info_by_uids?show_hidden=1";

        public string GetLiveStatusByUidUrl { get; set; } = "https://api.live.bilibili.com/room/v1/Room/get_status_info_by_uids?show_hidden=1&filter_offline=1";
        public string GetAllArea { get; set; } = "https://api.live.bilibili.com/room/v1/Area/getList";
    }
}
