using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Authentication
{
    public class BlcAuthenticationHandler : AuthenticationHandler<BlcAuthenticationOptions>
    {

        public BlcAuthenticationHandler(IOptionsMonitor<BlcAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }
        protected async override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var env = Request.HttpContext.RequestServices.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
            var token = "";
            string authorization = Request.Headers[HeaderNames.Authorization];
            if (env.IsDevelopment())
            {
                authorization = $"Blc dev:{Options.Token}";
            }
            if (string.IsNullOrEmpty(authorization))
            {
                return AuthenticateResult.NoResult();
            }
            if (authorization.StartsWith("Blc ", StringComparison.OrdinalIgnoreCase))
            {
                token = authorization.Substring("Blc ".Length).Trim();
                var split = token.Split(":");
                if (split.Length == 2 && split[1] == Options.Token)
                {
                    var claims = new[] {
                        new Claim(ClaimTypes.NameIdentifier, split[1]),
                        new Claim(ClaimTypes.Name, split[0])
                    };
                    var claimsIdentity = new ClaimsIdentity(claims, nameof(BlcAuthenticationHandler));
                    var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
                    return AuthenticateResult.Success(ticket);
                }
            }
            await Task.CompletedTask;
            return AuthenticateResult.NoResult();
        }
    }
}
