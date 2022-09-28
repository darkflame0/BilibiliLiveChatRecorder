using System.Threading.Channels;
using Darkflame.BilibiliLiveChatRecorder.Background.Options;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using EasyNetQ;
using Microsoft.Extensions.Options;

namespace Darkflame.BilibiliLiveChatRecorder.Background
{
    public class PersistenceService : IPersistenceService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IBus _bus;
        private readonly IOptionsMonitor<RoomOptions> _roomOptions;
        private readonly ILogger<PersistenceService> _logger;
        private readonly Channel<ChatMessage> _messageBuffer = Channel.CreateUnbounded<ChatMessage>(new UnboundedChannelOptions() { SingleReader = true });
        private Channel<ChatMessage> _fallbackMessageBuffer = Channel.CreateUnbounded<ChatMessage>(new UnboundedChannelOptions() { SingleReader = true });
        private IPullingConsumer<PullResult<ChatMessage>>? _pullingConsumer;
        static readonly string PersistentQName = $"{nameof(ChatMessage)}.persistent";

        RoomOptions RoomOptions => _roomOptions.CurrentValue;

        public PersistenceService(IServiceProvider serviceProvider, IHostApplicationLifetime lifetime, IBus bus, IOptionsMonitor<RoomOptions> roomOptions, ILogger<PersistenceService> logger)
        {
            _serviceProvider = serviceProvider;
            _lifetime = lifetime;
            _bus = bus;
            _roomOptions = roomOptions;
            _logger = logger;
        }
        public async Task StartAsync()
        {
            _ = Loop();
            await Task.CompletedTask;
        }
        public async Task SaveAsync(ChatMessage chatMessage)
        {
            await _messageBuffer.Writer.WriteAsync(chatMessage);
        }
        private async Task InitQ()
        {
            await _bus.Advanced.ConnectAsync();
            var queue = await _bus.Advanced.QueueDeclareAsync(PersistentQName);
            _pullingConsumer = _bus.Advanced.CreatePullingConsumer<ChatMessage>(queue, false);
        }
        private async Task Loop()
        {
            _ = InitQ();
            while (!_lifetime.ApplicationStopping.IsCancellationRequested)
            {
                try
                {
                    var msgs = await GetMessages();
                    try
                    {
                        await SaveToDb(msgs.messages);
                        await msgs.OnSucess();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "入库失败");
                        await msgs.OnFailed();
                        await FallbackToQ();
                    }
                    if (msgs.messages.Count >= _roomOptions.CurrentValue.QueueBatchSize && _messageBuffer.Reader.TryReadAll(out var messages) != 0)
                    {
                        await SaveToQ(messages);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "");
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            var list = Enumerable.Empty<ChatMessage>();
            if (_bus.Advanced.IsConnected)
            {
                if (_fallbackMessageBuffer.Reader.TryReadAll(out list) != 0)
                {
                    await SaveToQ(list);
                }
                if (_messageBuffer.Reader.TryReadAll(out list) != 0)
                {
                    await SaveToQ(list);
                }
            }
            else
            {
                if (_fallbackMessageBuffer.Reader.TryReadAll(out list) != 0)
                {
                    await SaveToDb(list);
                }
                if (_messageBuffer.Reader.TryReadAll(out list) != 0)
                {
                    await SaveToDb(list);
                }
            }
        }
        private async Task<(IReadOnlyList<ChatMessage> messages, Func<ValueTask> OnSucess, Func<ValueTask> OnFailed)> GetMessages()
        {
            var queueBatchSize = _roomOptions.CurrentValue.QueueBatchSize;
            if (_fallbackMessageBuffer.Reader.TryPeek(out _))
            {
                _fallbackMessageBuffer.Reader.TryReadAll(out var msgs, queueBatchSize);
                return (msgs.ToList(), () => ValueTask.CompletedTask, () => SaveToQ(msgs));
            }
            if (await CheckQueueAsync())
            {
                var msgs = new List<ChatMessage>(queueBatchSize);
                ulong deliveryTag = 0;
                try
                {
                    while (msgs.Count < queueBatchSize && await _pullingConsumer!.PullAsync() is { } pulled && pulled.IsAvailable)
                    {
                        msgs.Add(pulled.Message.Body);
                        deliveryTag = pulled.ReceivedInfo.DeliveryTag;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "从队列获取消息失败");
                    _pullingConsumer?.Dispose();
                    _pullingConsumer = null;
                    throw;
                }
                return (msgs, async () => await _pullingConsumer.AckBatchAsync(deliveryTag), async () =>
                {
                    await _pullingConsumer.RejectBatchAsync(deliveryTag, true);
                }
                );
            }
            _messageBuffer.Reader.TryReadAll(out var messages, queueBatchSize);
            return (messages.ToList(), () => ValueTask.CompletedTask, async () =>
            {
                await SaveToQ(messages);
            }
            );
        }
        private async Task SaveToDb(IEnumerable<ChatMessage> messages)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LiveChatDbContext>();
            db.AddRange(messages);
            await db.SaveChangesAsync();
        }
        private async ValueTask SaveToQ(IEnumerable<ChatMessage> messages)
        {
            foreach (var (a, i) in messages.Select((a, i) => (a, i)))
            {
                try
                {
                    await _bus.SendReceive.SendAsync(PersistentQName, a);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "入队失败");
                    foreach (var item in messages.Skip(i))
                    {
                        await _fallbackMessageBuffer.Writer.WriteAsync(item);
                    }
                }
            }
        }
        async Task FallbackToQ()
        {
            if (_messageBuffer.Reader.TryReadAll(out var messages) != 0)
            {
                await SaveToQ(messages);
            }
        }
        private async Task<bool> CheckQueueAsync()
        {
            try
            {
                if (!_bus.Advanced.IsConnected)
                {
                    return false;
                }
                if (_pullingConsumer == null)
                {
                    await InitQ();
                }
                if (await _pullingConsumer!.PullAsync() is { } pulled && pulled.IsAvailable)
                {
                    await _pullingConsumer.RejectBatchAsync(pulled.ReceivedInfo.DeliveryTag, true);
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "队列状态获取失败");
                _pullingConsumer?.Dispose();
                _pullingConsumer = null;
                return false;
            }
        }
    }
}
