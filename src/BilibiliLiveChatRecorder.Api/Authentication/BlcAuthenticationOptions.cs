using Microsoft.AspNetCore.Authentication;

namespace Darkflame.BilibiliLiveChatRecorder.Api.Authentication
{
    public class BlcAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string Token { get; set; } = "";
    }
}
