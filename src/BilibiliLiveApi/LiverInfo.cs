using System;
using Newtonsoft.Json;

namespace Darkflame.BilibiliLiveApi
{
    public class LiverInfo
    {
        public LiverInfo()
        {
        }

        public LiverInfo(int roomId, long uid, string name, string face, int shortId = 0)
        {
            Uid = uid;
            RoomId = roomId;
            Name = name;
            Face = face;
            ShortId = shortId;
        }
        [JsonProperty(Required = Required.Always)]
        public int RoomId { get; set; }
        [JsonProperty(Required = Required.Always)]
        public int ShortId { get; set; }
        [JsonProperty(Required = Required.Always)]
        public long Uid { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; } = "";
        [JsonProperty(Required = Required.Always)]
        public string Face { get; set; } = "";

    }
}
