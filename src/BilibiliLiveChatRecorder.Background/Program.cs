using Darkflame.BilibiliLiveApi;
using Darkflame.BilibiliLiveChatRecorder.Background;
using Darkflame.BilibiliLiveChatRecorder.Background.Options;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Darkflame.BilibiliLiveChatRecorder.Options;
using Darkflame.BilibiliLiveChatRecorder.Transport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;
var env = builder.Environment;
// Add services to the container.

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




services.AddControllers().AddNewtonsoftJson(op => op.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore);
services.AddMemoryCache();
services.AddStackExchangeRedisCache(op =>
{
    op.Configuration = configuration.GetConnectionString("Redis");
    op.InstanceName = "livechatcache:";
});
services.RegisterEasyNetQ(configuration.GetConnectionString("RabbitMQ"));
services.Configure<RoomOptions>(configuration);
services.AddOptions<RoomOptions>().PostConfigure<IBilibiliLiveApi>((op, api) =>
{
    op.ExcludeRoom = api.GetRealRoomId(op.ExcludeRoom).GetAwaiter().GetResult().ToHashSet();
    op.SpecificRoom = api.GetRealRoomId(op.SpecificRoom).GetAwaiter().GetResult().ToHashSet();
});
services.Configure<AutoKeepOptions>(configuration.GetSection("AutoKeep"));
services.Configure<LiveOptions>(configuration);
services.AddOptions<AutoKeepOptions>().PostConfigure<IBilibiliLiveApi>((op, api) =>
{
    op.Exclude = api.GetRealRoomId(op.Exclude).GetAwaiter().GetResult().ToHashSet();
});
services.Configure<LiverOptions>(configuration);
services.AddOptions<LiverOptions>();
services
.AddDbContextPool<LiveChatDbContext>(
    op =>
    {
        op.ConfigureWarnings(a => a.Ignore(new EventId(10620)));
        op.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        op.UseNpgsql(configuration.GetConnectionString("LiveChatDb"));
    }
    , poolSize: 128)
.AddHostedService<LiveChatBackgroundService>();
services.AddSingleton(s => s.GetRequiredService<IEnumerable<IHostedService>>().OfType<LiveChatBackgroundService>().Single());
services.Configure<BilbiliApiOptions>(configuration.GetSection("BiliApi"));
services.AddCachedBiliApiClient();







var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapGet("api/online", ([FromServices] LiveChatBackgroundService service, bool? all) =>
{
    var q = service.RoomDic.Select(a => a.Value);
    if (!all.GetValueOrDefault())
    {
        q = q.Where(a => a.Live);
    }
    return Results.Ok(q.Select(a => new { RoomId = a.RealRoomId, Uname = a.UName ?? "", Title = a.Title ?? "", Area = a.Area ?? "", Popularity = a.Popularity, UserCover = a.UserCover?.ToString() ?? "", Keyframe = a.Keyframe ?? "", Uid = a.UId, ShortId = a.ShortId, LiveTime = a.LiveTime.GetValueOrDefault().ToString("yyyy-MM-dd HH:mm:ss.fff"), Connected = a.Connected, ParentArea = a.ParentArea ?? "", Host = a.Host }));
});

app.Run();
