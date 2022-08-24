using System;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.Api.Interceptors;
using Darkflame.BilibiliLiveChatRecorder.Api.Models;
using EasyCaching.Core.Interceptor;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Services
{
    public interface IRankingService
    {
        Task<RankingList<RankingItem>?> GetIndividualRanking(int year, int month, int day, string? organization, RankingSortType? sortBy);
        Task<RankingList<RankingItem>?> GetIndividualRanking(int year, int month, string? organization, RankingSortType? sortBy);
        Task<RankingList<RankingItem>?> GetIndividualRankingWithTopDailyData(int year, int month, string? organization, RankingSortType? sortBy);
        Task<RankingList<LiveRankingItem>?> GetLiveRanking(int year, int month, int day, string? organization, RankingSortType? sortBy, bool distinct = false);
        Task<RankingList<LiveRankingItem>?> GetLiveRanking(int year, int month, string? organization, RankingSortType? sortBy, bool distinct = false);
        Task<RankingList<SummaryItem>?> GetOrganizedRanking(int year, int month, RankingSortType? sortBy);
        Task<RankingList<SummaryItem>?> GetOrganizedRanking(int year, int month, int day, RankingSortType? sortBy);
        Task<(DateTime UpdateTime, Summary Data)?> GetSummary(int year, int month);
        Task<(DateTime UpdateTime, Summary Data)?> GetSummary(int year, int month, int day);
        [EasyCachingAble(Expiration = 20)]
        Task<(DateTime Min, DateTime Max)> GetRange(bool fresh = false);
    }
}
