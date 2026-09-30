using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Pos.Application.Diagnostics;
using Pos.Application.Products;
using Pos.Application.Startup;
using Pos.Infrastructure.Diagnostics;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Platform;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Startup;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(paths);
        services.AddSingleton<IAppPaths>(paths);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICurrentUser, SystemCurrentUser>();
        services.AddSingleton<IAppInfo, AssemblyAppInfo>();
        services.AddSingleton<AuditingInterceptor>();

        services.AddDbContextFactory<PosDbContext>((sp, options) =>
            options
                .UseSqlite(SqliteConnectionStrings.For(paths.DatabaseFile))
                .AddInterceptors(sp.GetRequiredService<AuditingInterceptor>()));

        // Un DbContext por operación: la raíz de composición crea un ámbito por caso de uso.
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<PosDbContext>>().CreateDbContext());

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddSingleton<IDatabaseMaintenance, SqliteDatabaseMaintenance>();
        services.AddSingleton<IBackupService, SqliteBackupService>();
        services.AddSingleton<IDiagnosticsExporter>(sp => new ZipDiagnosticsExporter(
            sp.GetRequiredService<IAppPaths>(),
            sp.GetRequiredService<IAppInfo>(),
            sp.GetRequiredService<IBackupService>(),
            sp.GetRequiredService<IClock>()));

        return services;
    }
}
