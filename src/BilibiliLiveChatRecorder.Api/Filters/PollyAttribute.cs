using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Filters
{
    public class PollyAttribute : ActionFilterAttribute
    {
        public PollyAttribute(string policyKey)
        {
            PolicyKey = policyKey;
        }

        public string PolicyKey { get; set; }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var registry = context.HttpContext.RequestServices.GetRequiredService<IReadOnlyPolicyRegistry<string>>();
            ActionExecutedContext? executed = default;
            var r = await registry.Get<IAsyncPolicy>(PolicyKey).ExecuteAndCaptureAsync(async c =>
            {
                executed = await next();
                if (executed.Exception != null && !executed.ExceptionHandled)
                {
                    throw executed.Exception;
                }
            }
            , new Context(context.HttpContext.Request.Path));
            if (executed == default)
            {
                throw r.FinalException;
            }

        }
        public override Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            return base.OnResultExecutionAsync(context, next);
        }
    }
}
