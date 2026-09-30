using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.Diagnostics;
using Pos.Application.Inventory;
using Pos.Application.Printing;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Application.Audit;
using Pos.Application.Security;
using Pos.Application.Startup;
using Pos.Application.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Business;
using Pos.Infrastructure.Diagnostics;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Printing;
using Pos.Infrastructure.Printing.Linux;
using Pos.Infrastructure.Printing.Windows;
using Pos.Infrastructure.Platform;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Security;
using Pos.Infrastructure.Users;
using Pos.Infrastructure.Sales;
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
        services.AddSingleton<IAppInfo, AssemblyAppInfo>();
        services.AddSingleton<IPreferencesStore, JsonFilePreferencesStore>();
        services.AddSingleton<IPrintingSettingsStore, PreferencesPrintingSettingsStore>();
        services.AddSingleton<ISecuritySettingsStore, PreferencesSecuritySettingsStore>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<AuditingInterceptor>();

        // Impresión: el transporte se elige por sistema operativo; el resto no conoce la plataforma.
        services.AddSingleton<PrintGate>();
        services.AddSingleton<FileTicketPrinter>();
        services.AddSingleton<IRawPrinterTransport>(sp =>
            OperatingSystem.IsWindows() ? ActivatorUtilities.CreateInstance<WinSpoolTransport>(sp)
            : OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? ActivatorUtilities.CreateInstance<CupsTransport>(sp)
            : new UnsupportedTransport());
        services.AddSingleton<ITicketPrinter, PlatformTicketPrinter>();
        services.AddSingleton<ICashDrawer, PlatformCashDrawer>();
        services.AddSingleton<IPrinterCatalog, PlatformPrinterCatalog>();

        services.AddDbContextFactory<PosDbContext>((sp, options) =>
            options
                .UseSqlite(SqliteConnectionStrings.For(paths.DatabaseFile))
                .AddInterceptors(sp.GetRequiredService<AuditingInterceptor>()));

        // Un DbContext por operación: la raíz de composición crea un ámbito por caso de uso.
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<PosDbContext>>().CreateDbContext());

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IBusinessProfileRepository, BusinessProfileRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ISaleDraftStore, SqliteSaleDraftStore>();
        services.AddScoped<IWriteTransactions, WriteTransactions>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
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
