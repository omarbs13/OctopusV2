using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.CashShifts;
using Pos.Application.Categories;
using Pos.Application.CreditNotes;
using Pos.Application.Customers;
using Pos.Application.Purchases;
using Pos.Application.Suppliers;
using Pos.Application.Discounts;
using Pos.Application.Diagnostics;
using Pos.Application.Inventory;
using Pos.Application.Printing;
using Pos.Application.Receivables;
using Pos.Application.Reports;
using Pos.Application.Returns;
using Pos.Application.Reports.Export;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Application.Audit;
using Pos.Application.Security;
using Pos.Application.Startup;
using Pos.Application.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Business;
using Pos.Infrastructure.CashShifts;
using Pos.Infrastructure.Categories;
using Pos.Infrastructure.CreditNotes;
using Pos.Infrastructure.Customers;
using Pos.Infrastructure.Purchases;
using Pos.Infrastructure.Suppliers;
using Pos.Infrastructure.Discounts;
using Pos.Infrastructure.Diagnostics;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Printing;
using Pos.Infrastructure.Printing.Linux;
using Pos.Application.Licensing;
using Pos.Infrastructure.Licensing;
using Pos.Infrastructure.Printing.Windows;
using Pos.Infrastructure.Platform;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Receivables;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Returns;
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
        services.AddSingleton<IReportSettingsStore, PreferencesReportSettingsStore>();
        services.AddSingleton<IReturnsSettingsStore, PreferencesReturnsSettingsStore>();
        services.AddSingleton<IReceivablesSettingsStore, PreferencesReceivablesSettingsStore>();
        services.AddSingleton<IDiscountSettingsStore, PreferencesDiscountSettingsStore>();
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
        services.AddScoped<ICashShiftRepository, CashShiftRepository>();
        services.AddScoped<IReturnRepository, ReturnRepository>();
        services.AddScoped<ICreditNoteRepository, CreditNoteRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IReceivableRepository, ReceivableRepository>();
        services.AddScoped<ICustomerPaymentRepository, CustomerPaymentRepository>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<IDiscountApprovalStore, DiscountApprovalStore>();
        services.AddScoped<IDiscountReportReader, DiscountReportReader>();
        services.AddScoped<ISalesReportReader, SalesReportReader>();
        services.AddScoped<ICashCountReportReader, CashCountReportReader>();
        services.AddScoped<IInventoryReportReader, InventoryReportReader>();
        services.AddScoped<IReportAlertsReader, ReportAlertsReader>();
        services.AddScoped<IReceivablesReportReader, ReceivablesReportReader>();
        services.AddScoped<IPurchaseReportReader, PurchaseReportReader>();
        services.AddScoped<IWriteTransactions, WriteTransactions>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        services.AddSingleton<IChartRenderer, ChartRenderer>();
        services.AddSingleton<IPdfReportWriter, PdfReportWriter>();
        services.AddSingleton<IXlsxReportWriter, XlsxReportWriter>();
        services.AddSingleton<IDatabaseMaintenance, SqliteDatabaseMaintenance>();
        services.AddSingleton<IBackupService, SqliteBackupService>();
        services.AddSingleton<IMachineIdProvider, MachineIdProvider>();
        services.AddSingleton<ILicenseStore, LicenseFileStore>();
        services.AddSingleton<ILicenseSealStore, LicenseSealStore>();
        services.AddSingleton<IInstallationAgeReader, InstallationAgeReader>();
        services.AddSingleton<ILicenseVerifier, EcdsaLicenseVerifier>();
        services.AddSingleton<IDiagnosticsExporter>(sp => new ZipDiagnosticsExporter(
            sp.GetRequiredService<IAppPaths>(),
            sp.GetRequiredService<IAppInfo>(),
            sp.GetRequiredService<IBackupService>(),
            sp.GetRequiredService<IClock>()));

        return services;
    }
}
