using Darkflame.BilibiliLiveChatRecorder.DbModel;

namespace Darkflame.BilibiliLiveChatRecorder.Background
{
    public interface IPersistenceService
    {
        Task SaveAsync(ChatMessage chatMessage);
        Task StartAsync();
    }
}