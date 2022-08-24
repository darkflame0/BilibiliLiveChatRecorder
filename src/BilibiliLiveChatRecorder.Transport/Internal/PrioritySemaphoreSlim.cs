using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveChatRecorder.Transport.Internal
{
    internal class PrioritySemaphoreSlim<TPriority> : IDisposable
    {
        readonly SemaphoreSlim _sem;
        readonly PriorityQueue<TaskCompletionSource, TPriority?> _queue = new();
        TaskCompletionSource _tcs = new();
        readonly ConcurrentBag<(TaskCompletionSource Tcs, TPriority Priority)> _bags = new();

        public PrioritySemaphoreSlim(int initialCount)
        {
            _sem = new SemaphoreSlim(initialCount);
            Task.Run(Loop);
        }
        public PrioritySemaphoreSlim(int initialCount, int maxCount)
        {
            _sem = new SemaphoreSlim(initialCount, maxCount);
            Task.Run(Loop);
        }
        public int CurrentCount => _sem.CurrentCount;
        public int Release()
        {
            return _sem.Release();
        }
        public async Task WaitAsync(TPriority priority, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tcs = new TaskCompletionSource();
            using var _ = cancellationToken.Register(
                () => tcs.TrySetCanceled()
                );
            _bags.Add((tcs, priority));
            _tcs.TrySetResult();
            await tcs.Task;
        }

        async Task Loop()
        {
            await _tcs.Task;
            while (true)
            {
                await _sem.WaitAsync();
                _tcs = new TaskCompletionSource();
                var delay = Task.Delay(1000);
                while (_bags.TryTake(out var item))
                {
                    _queue.Enqueue(item.Tcs, item.Priority);
                    if(delay.IsCompleted)
                    {
                        break;
                    }    
                }
                do
                {
deq: 
                    if (!_queue.TryDequeue(out var t, out _))
                    {
                        _sem.Release();
                        await _tcs.Task;
                        break;
                    }
                    else
                    {
                        if (t.Task.IsCanceled)
                        {
                            goto deq;
                        }
                        t.TrySetResult();
                    }
                } while (await _sem.WaitAsync(0));
            }
        }
        public void Dispose()
        {
            _sem.Dispose();
            _queue.Enqueue(new TaskCompletionSource(), default);
        }
    }
}
