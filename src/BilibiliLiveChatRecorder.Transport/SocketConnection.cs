using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Transport
{
    public class SocketConnection : IConnection
    {
        private readonly TcpClient _client = new TcpClient
        {
            ReceiveTimeout = 60000,
            SendTimeout = 10000
        };

        NetworkStream _stream => _client.GetStream();

        public bool Connected
        {
            get
            {
                try
                {
                    return _client.Connected;
                }
                catch
                {

                    return false;
                }
            }
        }

        public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            return _client.ConnectAsync(host, port);
        }

        public async ValueTask<int> ReadAsync(Memory<byte> memory, CancellationToken cancellationToken = default)
        {
            var offset = 0;
            var length = memory.Length;
            while (offset != length)
            {
                var size = await _stream.ReadAsync(memory.Slice(offset, length - offset), cancellationToken);
                if (size == 0)
                {
                    return 0;
                }
                offset += size;
            }
            return offset;
        }

        public ValueTask WriteAsync(Memory<byte> memory, CancellationToken cancellationToken = default)
        {
            return _stream.WriteAsync(memory, cancellationToken);
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    _client.Dispose();
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~SocketConnection()
        // {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
