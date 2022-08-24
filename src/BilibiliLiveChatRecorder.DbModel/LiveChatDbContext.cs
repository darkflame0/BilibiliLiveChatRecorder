using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Darkflame.BilibiliLiveChatRecorder.DbModel.QueryEntities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace Darkflame.BilibiliLiveChatRecorder.DbModel
{
#nullable disable warnings
    public class LiveChatDbContext : DbContext
    {
        static LiveChatDbContext()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }
        public LiveChatDbContext(DbContextOptions<LiveChatDbContext> options)
            : base(options)
        {
        }

        public DbSet<ChatMessage> ChatMessage { get; set; }

        public DbSet<HourlyData> HourlyData { get; set; }
        public DbSet<DailyData> DailyData { get; set; }
        public DbSet<MonthlyData> MonthlyData { get; set; }
        public DbSet<LiveHistory> LiveHistory { get; set; }

        static readonly Regex Regex = new("[\u0000]", RegexOptions.Compiled);
        static JToken EscapeUnsupportedUnicode(JToken j)
        {
            if (Regex.IsMatch(j["info"]?[1]?.ToString() ?? string.Empty))
            {
                j["info"][1] = Regex.Replace(j["info"]?[1].ToString(), "");
            }
            return j;
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDbFunction(DateTruncMethod).HasName("date_trunc");
            modelBuilder.Entity<RoomLiveTime>().HasNoKey();
            modelBuilder.Entity<ChatMessage>(a =>
            {
                a.HasIndex(a => new { a.RoomId, a.Time, });
                a.HasIndex(a => new { a.Time, });
                a.Property(a => a.Raw).HasConversion(s => EscapeUnsupportedUnicode(s).ToString(), d => JToken.Parse(d));
                a.Property<DateTime>("InsertTime").ValueGeneratedOnAdd().HasDefaultValueSql("now()");
                if (Database.IsNpgsql())
                {
                    a.Property(a => a.Raw).HasColumnType("jsonb");
                }
            });

            modelBuilder.Entity<HourlyData>(a =>
            {
                a.HasIndex(a => new { a.Time });
                a.Property(a => a.Time).HasColumnName("Time");
                a.OwnsOne(e => e.Data, e =>
                {
                    e.Property(a => a.RealDanmakuUser);
                    e.Property(a => a.GoldUser);
                    //e.Property<DateTime>("Time").HasColumnName("Time");
                    //e.HasIndex("RoomId", "Time");
                    e.Ignore(a => a.Participants);
                    e.Ignore(a => a.Viewer);
                });
            });
            modelBuilder.Entity<DailyData>(a =>
            {
                a.OwnsOne(e => e.Data, e =>
                {
                    e.Property(a => a.RealDanmakuUser);
                    e.Property(a => a.GoldUser);
                    e.WithOwner(e => e.DailyData);
                });
                a.HasIndex(a => new { a.Date, a.RoomId }).IsUnique();
                a.HasIndex(a => new { a.UpdateTime });
            });
            modelBuilder.Entity<MonthlyData>(a =>
            {
                a.OwnsOne(e => e.Data, e =>
                {
                    e.Property(a => a.RealDanmakuUser);
                    e.Property(a => a.GoldUser);
                    e.WithOwner(e => e.MonthlyData);
                });
                a.HasIndex(a => new { a.Date, a.RoomId }).IsUnique();
                a.HasIndex(a => new { a.UpdateTime });
            });
            modelBuilder.Entity<LiveHistory>(a =>
            {
                a.OwnsOne(e => e.Data, e =>
                {
                    e.Property(a => a.Participants).IsRequired(false).UsePropertyAccessMode(PropertyAccessMode.PreferProperty);
                    e.Property(a => a.RealDanmakuUser);
                    e.Property(a => a.GoldUser);
                    e.HasIndex(a => new { a.EndTime, a.StartTime }).IsUnique();
                    e.HasIndex(a => new { a.StartTime, a.RoomId }).IsUnique();
                    e.Ignore(a => a.Viewer);
                });
            });
        }
        public static DateTime DateTrunc(string field, DateTime source) => throw new NotSupportedException();
        private static readonly MethodInfo DateTruncMethod
    = typeof(LiveChatDbContext).GetMethod(nameof(DateTrunc));
    }
}
