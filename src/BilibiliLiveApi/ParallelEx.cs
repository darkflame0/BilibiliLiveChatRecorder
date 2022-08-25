using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Darkflame.BilibiliLiveApi
{
    public class ParallelEx
    {
        public static async Task<TResult[]> WhenAll<TSource, TResult>(IEnumerable<TSource> source, ParallelOptions parallelOptions, Func<TSource, CancellationToken, Task<TResult>> body)
        {
            var r = new ConcurrentBag<TResult>();
            await Parallel.ForEachAsync(source, parallelOptions: parallelOptions, async (a, _) => r.Add(await body(a, _)));
            return r.ToArray();
        }
    }
}
