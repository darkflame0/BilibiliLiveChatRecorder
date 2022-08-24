using Darkflame.BilibiliLiveChatRecorder.Api.Models;
using Refit;

namespace Darkflame.BilibiliLiveChatRecorder.Api.HttpApis
{
    public interface IStatisticsApi
    {
        [Get("/api/statistic/participants/10m")]
        Task<Dictionary<long, int>> GetParticipantDuring10Min();
        [Get("/api/statistic/{roomId}/income")]
        Task<Dictionary<long, int>> GetIncome(int roomId);
        [Get("/api/statistic/{roomId}")]
        Task<RoomStatistic?> GetRoom(int roomId);
    }
}
