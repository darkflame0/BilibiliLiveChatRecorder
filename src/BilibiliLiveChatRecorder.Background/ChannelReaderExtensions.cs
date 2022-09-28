using System.Threading.Channels;

namespace Darkflame.BilibiliLiveChatRecorder.Background
{
    public static class ChannelReaderExtensions
    {
        public static int TryReadAll<T>(this ChannelReader<T> reader, out IEnumerable<T> list, int maxCount = int.MaxValue)
        {
            list = Enumerable.Empty<T>();
            var i = 0;
            while (i < maxCount && reader.TryRead(out var item))
            {
                ++i;
                list = list.Append(item);
            }
            return i;
        }
    }
}
