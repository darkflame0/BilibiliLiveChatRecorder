using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
    public class MonthlyData
    {

        public int Id { get; set; }
        public int RoomId { get; set; }
        [Column(TypeName = "date")]
        public DateTime Date { get; set; }
        public DateTime UpdateTime { get; set; }
        public DateTime LastLiveStartTime { get; set; }
        public DateTime LastLiveEndTime { get; set; }
        public AggregateData Data { get; set; } = new AggregateData();
        public void Union(IEnumerable<DailyData> data)
        {
            foreach (var item in data.OrderBy(a => a.Date))
            {
                if (item.RoomId != RoomId)
                {
                    throw new InvalidOperationException("RoomId must be the same");
                }
                if (UpdateTime < item.Date)
                {
                    if ((item.LastLiveStartTime - LastLiveEndTime) > RoomData.LiveInterval)
                    {
                        LastLiveStartTime = item.LastLiveStartTime;
                    }
                    if (item.LastLiveEndTime > LastLiveEndTime)
                    {
                        LastLiveEndTime = item.LastLiveEndTime;
                    }
                    UpdateTime = item.UpdateTime;
                    Data.Union(item.Data);
                }
            }
        }
    }
}
