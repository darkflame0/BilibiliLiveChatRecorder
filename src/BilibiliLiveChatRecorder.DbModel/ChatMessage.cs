using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public class ChatMessage
    {
        public ChatMessage()
        {
        }

        public ChatMessage(int roomId, DateTime time, JToken raw)
        {
            RoomId = roomId;
            Time = time;
            Raw = raw;
        }

        public long Id { get; set; }
        public int RoomId { get; set; }
        public DateTime Time { get; set; }
        public JToken Raw { get; set; } = default!;
    }
}
