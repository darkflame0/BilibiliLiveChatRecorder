

using System;
using Darkflame.BilibiliLiveApi;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Models
{
    public class LiverModel
    {
        public LiverModel()
        {
        }

        public long? Uid { get; set; }
        public int? RoomId { get; set; }
        public string Name { get; set; } = "";
        public string Face { get; set; } = "";

        public static implicit operator LiverModel(LiverInfo v)
        {
            return new LiverModel() { Uid = v.Uid, RoomId = v.RoomId, Name = v.Name, Face = v.Face };
        }
    }
}
