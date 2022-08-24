using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Transport
{
    public interface IConnection : IDisposable
    {
        bool Connected { get; }

        Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
        ValueTask<int> ReadAsync(Memory<byte> memory, CancellationToken cancellationToken = default);
        ValueTask WriteAsync(Memory<byte> memory, CancellationToken cancellationToken = default);
    }
}
