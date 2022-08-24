using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public class HourlyData
    {
        public static readonly int IntervalMins = 5;
        public int Id { get; set; }
        [Column(name: nameof(Time))]
        public DateTime Time { get; set; }
        //[NotMapped]
        //public int RoomId { get => Data.RoomId; set => Data.RoomId = value; }
        public RoomData Data { get; set; } = new RoomData();
    }
}
