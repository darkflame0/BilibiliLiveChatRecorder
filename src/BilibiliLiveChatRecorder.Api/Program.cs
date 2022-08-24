using System.Reflection;
using AspectCore.Extensions.DependencyInjection;
using Darkflame.BilibiliLiveApi;
using Darkflame.BilibiliLiveChatRecorder.Api;
using Darkflame.BilibiliLiveChatRecorder.Api.HttpApis;
using Darkflame.BilibiliLiveChatRecorder.Api.Models;
using Darkflame.BilibiliLiveChatRecorder.Api.Services;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Darkflame.BilibiliLiveChatRecorder.Options;
using EasyCaching.Interceptor.AspectCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Registry;
using Refit;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;
var env = builder.Environment;

if (Environment.GetEnvironmentVariable("KUBERNETES_PORT") != null)
{
    (configuration as IConfigurationBuilder).Sources.Clear();
    configuration.AddEnvironmentVariables();
    configuration.AddCommandLine(args);
    configuration.AddJsonFile("appsettings/appsettings.json", optional: true, reloadOnChange: true);
    configuration.AddJsonFile("livers/livers.json", optional: true, reloadOnChange: true);
}
else
{
    configuration.AddJsonFile($"{env.ContentRootPath}/{(env.IsDevelopment() ? "../../" : "")}livers.json", optional: true, reloadOnChange: true);
}

// Add services to the container.
builder.Host.UseServiceProviderFactory(new DynamicProxyServiceProviderFactory());



services.AddControllers().AddControllersAsServices().AddNewtonsoftJson(op => op.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore);
services.AddResponseCaching();

var refitSettings = new RefitSettings(new NewtonsoftJsonContentSerializer());
var defaultExceptionFactory = refitSettings.ExceptionFactory;
refitSettings.ExceptionFactory = httpResponse =>
httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound ? Task.FromResult<Exception?>(null) : defaultExceptionFactory(httpResponse);
services
    .AddRefitClient<IBackgroundApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(configuration.GetConnectionString("Background")));
services
    .AddRefitClient<IStatisticsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(configuration.GetConnectionString("Statistics")));
;
;
services
    .AddDbContext<LiveChatDbContext>(
        op =>
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(configuration.GetConnectionString("LiveChatDb"))
            {
                MaxPoolSize = 4,
            };
            op.ConfigureWarnings(a => a.Ignore(new EventId(10620)));
            op.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            op.UseNpgsql(
                builder.ConnectionString,
                (npgop) =>
                {
                    npgop.UseJTokenTranslating();
                    npgop.UseRelationalNulls();
                });
        });
services.AddMemoryCache();
services.AddStackExchangeRedisCache(op =>
{
    op.Configuration = configuration.GetConnectionString("Redis");
    op.InstanceName = "livechatcache:";
});
services.AddAuthentication(defaultScheme: "Blc").AddBlc(op => configuration.GetSection("Blc").Bind(op));
services.Configure<BilbiliApiOptions>(configuration.GetSection("BiliApi"));
if (env.IsDevelopment())
{
    services.AddBiliApiClient();
}
else
{
    services.AddCachedBiliApiClient();
}
services.AddCors();
services.AddAutoMapper(
    op =>
    {
        op.CreateMap<FullRoomInfo, RoomInfo>();
        op.CreateMap<FullRoomInfo, RoomInfoWithHost>();
    },
    Assembly.GetExecutingAssembly());
services.AddOptions<QueryOptions>().PostConfigure<IBilibiliLiveApi>((op, api) =>
{
    op.ExcludeRooms = api.GetRealRoomId(op.ExcludeRooms).GetAwaiter().GetResult().ToHashSet();
});
services.Configure<QueryOptions>(configuration);
services.AddOptions<LiverExOptions>().PostConfigure((op) =>
{
    foreach (var item in op.Organizations)
    {
        item.Livers = op.Livers.Where(a => a.Organization.Contains(item.Name)).ToList();
    }
    op.LiversDic = op.Livers.Where(a => a.RoomId != 0).ToDictionary(a => a.RoomId);
    op.OrganizationsDic = op.Organizations.ToDictionary(a => a.Name);
});
services.Configure<LiverExOptions>(configuration);
services.AddScoped<IRankingService, RankingService>();
services.AddSingleton<IReadOnlyPolicyRegistry<string>>(GetPolicies());
services.AddEasyCaching(options =>
{
    options.UseInMemory(op =>
    {
        op.MaxRdSecond = 10;
        op.DBConfig.EnableReadDeepClone = false;
    }, "m1");
});
services.ConfigureDynamicProxy(op => op.ThrowAspectException = false);
services.ConfigureAspectCoreInterceptor(options => options.CacheProviderName = "m1");
services.AddResponseCompression();

static PolicyRegistry GetPolicies() => new PolicyRegistry
        {
            {"RankingCircuitBreaker", Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(2, TimeSpan.FromMinutes(1)) },
            {"OnlineCircuitBreaker",
                Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(2, TimeSpan.FromMinutes(1))
            }
        };

var app = builder.Build();



app.UseHttpLogging();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseResponseCompression();
app.UseRouting();

app.UseCors(policy =>
{
    policy.SetIsOriginAllowed(origin => new UriBuilder(origin).Host == "localhost")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
});
app.UseResponseCaching();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
