using System;
using System.Collections.Generic;
using System.Text;
using Darkflame.BilibiliLiveChatRecorder.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class StreamingAuthenticationSExtensions
    {
        public static AuthenticationBuilder AddBlc(this AuthenticationBuilder builder)
        {
            return builder.AddScheme<BlcAuthenticationOptions, BlcAuthenticationHandler>("Blc",op=> { });
        }
        public static AuthenticationBuilder AddBlc(this AuthenticationBuilder builder,Action<BlcAuthenticationOptions> op)
        {
            return builder.AddScheme<BlcAuthenticationOptions, BlcAuthenticationHandler>("Blc", op);
        }
    }
}
