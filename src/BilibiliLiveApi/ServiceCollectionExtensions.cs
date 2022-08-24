using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using Darkflame.BilibiliLiveApi;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCachedBiliApiClient(this IServiceCollection services)
        {
            services.AddOptions<BilbiliApiOptions>();
            services.AddSingleton<IBilibiliLiveApi, CachedBilibiliLiveHttpClient>();
            services.AddHttpClient<BilibiliLiveHttpClient>(c =>
            {
                c.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue()
                {
                    NoCache = true,
                    NoStore = true
                };
                c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            })
            .ConfigurePrimaryHttpMessageHandler(h => GetSocketsHttpHandler(h));
            return services;
        }
        static SocketsHttpHandler GetSocketsHttpHandler(IServiceProvider serviceProvider) => new SocketsHttpHandler()
        {
            //Proxy = new WebProxy("127.0.0.1:1080"),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(60),
            ResponseDrainTimeout = TimeSpan.FromSeconds(5),
            ConnectTimeout = TimeSpan.FromSeconds(5),
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true
        };
        public static IServiceCollection AddBiliApiClient(this IServiceCollection services)
        {
            services.AddOptions<BilbiliApiOptions>();
            services.AddHttpClient<IBilibiliLiveApi, BilibiliLiveHttpClient>(c =>
            {
                c.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue()
                {
                    NoCache = true,
                    NoStore = true
                };
                c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            })
            .ConfigurePrimaryHttpMessageHandler(h => GetSocketsHttpHandler(h));
            return services;
        }
    }
}
