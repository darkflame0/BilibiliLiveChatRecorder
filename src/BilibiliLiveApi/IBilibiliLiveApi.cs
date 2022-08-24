using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveApi
{
    public interface IBilibiliLiveApi
    {
        Task<bool> GetLiveStatus(int roomId);
        Task<IEnumerable<int>> GetLiveRoomId(IEnumerable<int> roomIds);
        ValueTask<int> GetRealRoomId(int shortId);
        Task<LiveInfo> GetLiveInfo(int roomId);
        Task<(IEnumerable<(string Host, int Port, int WsPort)> Servers, string Token)> GetRoomServerConf(int roomId);
        ValueTask<int> GetShortId(int roomId);
        Task<LiverInfo?> GetLiverInfo(int roomId);
        Task<IEnumerable<LiverInfo>> GetLiverInfo(IEnumerable<int> roomIds);
        Task<List<(LiverInfo Liver, LiveInfo LiveInfo)>> GetVLiverRoomList();
        Task<UserInfo?> GetUserInfo(long uid);
        Task<IEnumerable<UserInfo>> GetUserInfo(IEnumerable<long> uids);
        Task<string?> GetLiverName(int roomId);
        Task<IEnumerable<int>> GetRealRoomId(IEnumerable<int> list);
        Task<IEnumerable<(int RoomId ,LiveInfo LiveInfo)>> GetLiveInfo(IEnumerable<int> roomIds);
        Task<IEnumerable<(long Uid, int RoomId)>> GetRoomIdByUid(IEnumerable<long> uids);
        Task<int> GetRoomIdByUid(long uid);
        Task<IEnumerable<(LiverInfo Liver, LiveInfo LiveInfo)>> GetLiveRoomByUid(IEnumerable<long> uids);
        Task<IEnumerable<LiverInfo>> GetLiverInfoByUid(IEnumerable<long> uids);
        Task<LiverInfo?> GetLiverInfoByUid(long uid);
        Task<List<(LiverInfo Liver, LiveInfo LiveInfo)>> GetRecentlyVLiverRoomList();
    }
}
