using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Transport
{
    public class WebSocketConnection : IConnection
    {
        private readonly ClientWebSocket _webSocket = new ClientWebSocket();
        private bool _disposedValue;

        public bool Connected
        {
            get
            {
                try
                {
                    return _webSocket.State == WebSocketState.Open;
                }
                catch
                {

                    return false;
                }
            }
        }
        public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            var b = new UriBuilder(host)
            {
                Port = port
            };
            _webSocket.Options.KeepAliveInterval = Timeout.InfiniteTimeSpan;
            return _webSocket.ConnectAsync(b.Uri, cancellationToken);
        }

        public async ValueTask<int> ReadAsync(Memory<byte> memory, CancellationToken cancellationToken = default)
        {
            var offset = 0;
            var length = memory.Length;
            while (offset != length)
            {
                var r = await _webSocket.ReceiveAsync(memory.Slice(offset, length - offset), cancellationToken);
                offset += r.Count;
                if (r.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken);
                    return 0;
                }
            }
            return offset;
        }

        public async ValueTask WriteAsync(Memory<byte> memory, CancellationToken cancellationToken = default)
        {
            await _webSocket.SendAsync(memory, WebSocketMessageType.Binary, true, cancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)
                                        if (_webSocket?.State == WebSocketState.Open)
                    {
                        _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).GetAwaiter().GetResult();
                    }
                    _webSocket?.Abort();
                }

                // TODO: 释放未托管的资源(未托管的对象)并替代终结器
                // TODO: 将大型字段设置为 null
                _disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~WebSocketConnection()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
