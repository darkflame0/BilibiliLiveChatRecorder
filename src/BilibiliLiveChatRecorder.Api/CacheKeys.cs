using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.Api.Models;

namespace Darkflame.BilibiliLiveChatRecorder.Api
{
    public static class CacheKeys
    {
        public static string GetRanking(int year, int month, int? day = null, string groupBy = "individual", RankingSortType? sortBy = null, string? organization = null)
        {
            return $"ranking:{year}-{month}{(day == null ? "" : $"-{day}")}.groupby:{groupBy}{(string.IsNullOrEmpty(organization) ? "" : $".organization:{organization}")}{(!sortBy.HasValue||!Enum.IsDefined(sortBy.Value) ? "" : $".sortby:{sortBy.Value.ToString().ToLower()}")}";
        }
        public static string GetSummary(int year, int month, int? day = null)
        {
            return $"summary:{year}-{month}{(day == null ? "" : $"-{day}")}";
        }
    }
}
