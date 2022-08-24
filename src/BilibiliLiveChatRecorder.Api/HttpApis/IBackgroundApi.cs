using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.Api.Models;
using Refit;

namespace Darkflame.BilibiliLiveChatRecorder.Api.HttpApis
{
    public interface IBackgroundApi
    {
        [Get("/api/online")]
        Task<IEnumerable<FullRoomInfo>> GetRooms(bool all);
    }
}
