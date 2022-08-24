using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Interceptors
{
    public class ConcurrencyLimit : AbstractInterceptorAttribute
    {
        static readonly ConcurrentDictionary<string, SemaphoreSlim> SemDic = new ConcurrentDictionary<string, SemaphoreSlim>();
        public string Scope { get; set; } = "";
        public async override Task Invoke(AspectContext context, AspectDelegate next)
        {
            var sem = SemDic.GetOrAdd(Scope, _ => new SemaphoreSlim(1, 1));
            try
            {
                await sem.WaitAsync();
                await next(context);
            }
            finally
            {
                sem.Release();
            }
        }
    }
}
