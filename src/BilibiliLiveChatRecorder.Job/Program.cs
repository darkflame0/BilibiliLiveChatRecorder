using Darkflame.BilibiliLiveApi;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Darkflame.BilibiliLiveChatRecorder.Job;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;
var env = builder.Environment;

services.AddControllers().AddNewtonsoftJson(op => op.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore);
services.Configure<JobOptions>(configuration);
services
.AddDbContext<LiveChatDbContext>(
op =>
{
    op.ConfigureWarnings(a => a.Ignore(new EventId(10620)));
    op.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    op.UseNpgsql(
    configuration.GetConnectionString("LiveChatDb"),
    (npgop) =>
    {
        npgop.CommandTimeout(300);
        npgop.UseJTokenTranslating();
        npgop.UseRelationalNulls();
    });
});
services.AddHostedService<TimedHostedService>();
services.AddMemoryCache();
services.AddStackExchangeRedisCache(op =>
{
    op.Configuration = configuration.GetConnectionString("Redis");
    op.InstanceName = "livechatcache:";
});
services.Configure<BilbiliApiOptions>(configuration.GetSection("BiliApi"));
services.AddCachedBiliApiClient();


var app = builder.Build();

app.UseAuthorization();

app.Run();

