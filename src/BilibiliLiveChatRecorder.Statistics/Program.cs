using Darkflame.BilibiliLiveApi;
using Darkflame.BilibiliLiveChatRecorder.DbModel;
using Darkflame.BilibiliLiveChatRecorder.Statistics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;
var env = builder.Environment;
// Add services to the container.


services.AddControllers().AddNewtonsoftJson(op => op.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore);
services.AddSignalR();
services.AddMemoryCache();
services.AddHostedService<StatisticsBackgroundService>();
services.AddSingleton(s => s.GetRequiredService<IEnumerable<IHostedService>>().OfType<StatisticsBackgroundService>().Single());
services.RegisterEasyNetQ(configuration.GetConnectionString("RabbitMQ"));
services
    .AddDbContextPool<LiveChatDbContext>(
        op =>
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(configuration.GetConnectionString("LiveChatDb"))
            {
                MaxPoolSize = 2
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
        }, poolSize: 128);
services.AddMemoryCache();
services.AddStackExchangeRedisCache(op =>
{
    op.Configuration = configuration.GetConnectionString("Redis");
    op.InstanceName = "livechatcache:";
});
services.Configure<BilbiliApiOptions>(configuration.GetSection("BiliApi"));
services.AddCachedBiliApiClient();







var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});



app.UseCors(policy =>
{
    policy.SetIsOriginAllowed(origin => new UriBuilder(origin).Host == "localhost")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
});

app.UseAuthorization();

app.MapHub<RoomHub>("/roomHub");

app.MapGet("api/statistic/participants/10m", ([FromServices] StatisticsBackgroundService service, bool? all) =>
{
    return Results.Ok(service.GetParticipantDuring10MinDic());
});

app.MapGet("api/statistic/{roomId:int}", ([FromServices] StatisticsBackgroundService service, int roomId) =>
{
    if (!service.ContextDic.TryGetValue(roomId, out var c))
    {
        return Results.NotFound();
    }
    return Results.Ok(c.Statistics);
});

app.MapGet("api/statistic/{roomId:int}/income", ([FromServices] StatisticsBackgroundService service, int roomId) =>
{
    if (!service.ContextDic.TryGetValue(roomId, out var c))
    {
        return Results.NotFound();
    }
    return Results.Ok(c.IncomeDic);
});
app.Run();
