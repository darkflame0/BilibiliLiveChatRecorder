using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Microsoft.EntityFrameworkCore
{
    public static class QueryableExtensions
    {
        private static readonly TypeInfo QueryCompilerTypeInfo = typeof(QueryCompiler).GetTypeInfo();
        private static readonly TypeInfo QueryContextFactoryTypeInfo = typeof(RelationalQueryContextFactory).GetTypeInfo();
        private static readonly FieldInfo QueryCompilerField = typeof(EntityQueryProvider).GetTypeInfo().DeclaredFields
            .First(x => x.Name == "_queryCompiler");
        private static readonly FieldInfo QueryContextFactoryField =
    QueryCompilerTypeInfo.DeclaredFields.Single(x => x.Name == "_queryContextFactory");
        private static readonly FieldInfo QueryContextDependenciesField =
            QueryContextFactoryTypeInfo.DeclaredFields.Single(x => x.Name == "_dependencies");
        public static DbContext GetContext(this IQueryable source)
        {
            if (source is IInfrastructure<IServiceProvider> service)
            {
                return service.GetService<ICurrentDbContext>().Context;
            }
            var queryCompiler = (QueryCompiler)QueryCompilerField.GetValue(source.Provider)!;
            var queryContextFactory = (IQueryContextFactory)QueryContextFactoryField.GetValue(queryCompiler)!;
            var queryContextDependencies = (QueryContextDependencies)QueryContextDependenciesField.GetValue(queryContextFactory)!;
            return queryContextDependencies.CurrentContext.Context;
        }
        public static TContext? GetContext<TContext>(this IQueryable source)
            where TContext : DbContext
        {
            return source.GetContext() as TContext;
        }
    }
}
