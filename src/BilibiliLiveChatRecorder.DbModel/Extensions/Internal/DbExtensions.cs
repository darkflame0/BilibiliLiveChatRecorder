using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore
{
    public static class DbExtensions
    {
        public static TContext GetNewInstance<TContext>(this TContext dbContext)
            where TContext : DbContext
        {
            return (TContext)Activator.CreateInstance(dbContext.GetType(), dbContext.GetService<IDbContextServices>().ContextOptions)!;
        }

        public static TContext GetNewInstance<TContext>(this TContext _, IServiceProvider serviceProvider)
            where TContext : DbContext
        {
            return serviceProvider.CreateScope().ServiceProvider.GetRequiredService<TContext>();
        }

        public static string GetTableName<T>(this DbSet<T> dbSet)
            where T : class
        {
            return GetTableName(dbSet.GetContext(), typeof(T));
        }

        public static string GetTableName(this DbContext dbContext, Type type)
        {
            return dbContext.Model.FindEntityType(type)!.GetTableName()!;
        }

        public static string GetTableName<TEntity>(this DbContext dbContext)
            where TEntity : class
        {
            return dbContext.GetTableName(typeof(TEntity));
        }

        public static async Task<bool> EnsureTable<TEntity>(this DbSet<TEntity> dbSet)
            where TEntity : class
        {
            var dbContext = dbSet.GetService<ICurrentDbContext>().Context;
            try
            {
                dbSet.Any();
                return false;
            }
            catch (DbException e) when (e.Message.ToLower().Contains("not exist"))
            {
                var tableName = dbContext.GetTableName<TEntity>();
                var migrationsSqlGenerator = ((IInfrastructure<IServiceProvider>)dbContext).GetService<IMigrationsSqlGenerator>();
                var migrationsModelDiffer = ((IInfrastructure<IServiceProvider>)dbContext).GetService<IMigrationsModelDiffer>();
                var opList = migrationsModelDiffer.GetDifferences(null, dbContext.Model.GetRelationalModel()).Where(a =>
                (a is CreateTableOperation ct && ct.Name.Contains(tableName)) || (a is CreateIndexOperation ci && ci.Name.Contains(tableName)))
                .ToList();
                var list = migrationsSqlGenerator.Generate(opList, dbContext.Model);
                foreach (var a in list)
                {
                    await dbContext.Database.ExecuteSqlRawAsync(a.CommandText);
                }

                return true;
            }
        }
    }
}
