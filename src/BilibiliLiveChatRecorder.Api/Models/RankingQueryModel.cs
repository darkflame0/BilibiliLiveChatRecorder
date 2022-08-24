using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Models
{
    public class RankingQueryModel
    {
        public string? GroupBy { get; set; }
        public string? Organization { get; set; }
        public string? DataType { get; set; }
        public RankingSortType? SortBy { get; set; } = RankingSortType.Participant;
        public bool Distinct { get; set; }
    }
}
