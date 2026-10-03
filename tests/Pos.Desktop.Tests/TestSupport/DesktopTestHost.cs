using Microsoft.Extensions.DependencyInjection;
using Pos.Application;
using Pos.Application.Abstractions;
using Pos.Application.Categories;
using Pos.Application.Diagnostics;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.Access;
using Pos.Application.Users.Session;
using Pos.Desktop.Common;

namespace Pos.Desktop.Tests.TestSupport;

/// <summary>
/// Contenedor para probar ViewModels con los casos de uso reales de Application y un repositorio
/// en memoria.
/// </summary>
public sealed class DesktopTestHost : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public DesktopTestHost(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddLogging();
        services.AddSingleton<IProductRepository>(Repository);
        services.AddSingleton<IInventoryRepository>(Inventory);
        services.AddSingleton<ICategoryRepository>(Categories);
        services.AddSingleton<ISaleRepository>(Sales);
        services.AddSingleton<IWriteTransactions>(new FakeWriteTransactions());
        services.AddSingleton<IAuditLog>(new RecordingAuditLog());
        services.AddSingleton<ICurrentUser, FixedCurrentUser>();
        services.AddScoped<IAccessControl, AllowAllAccessControl>();
        services.AddSingleton<IUserSession>(Session);
        services.AddSingleton<ICurrentPermissions, SessionPermissions>();
        services.AddSingleton<Pos.Desktop.Shell.ISessionNavigation, FakeSessionNavigation>();
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IAppInfo, FakeAppInfo>();
        services.AddSingleton<Pos.Application.Licensing.IMachineIdProvider, FakeMachineId>();
        services.AddSingleton<IAppPaths, FakeAppPaths>();
        services.AddSingleton<IDiagnosticsExporter>(Exporter);
        services.AddSingleton<IClipboardService>(Clipboard);
        services.AddSingleton<IDialogService>(Dialogs);
        services.AddSingleton(Sink.CreateLogger());
        services.AddSingleton<OperationRunner>();
        services.AddSingleton<UseCases>();
        configure?.Invoke(services);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        // Las pantallas son de la sesión (scoped): los ViewModels y el menú se resuelven de un ámbito de sesión.
        _scope = _provider.CreateScope();
    }

    /// <summary>Sesión de pruebas: un Administrador, salvo que la prueba fije otro rol.</summary>
    public FakeUserSession Session { get; } = new();

    public InMemoryProductRepository Repository { get; } = new();

    public InMemoryInventoryRepository Inventory { get; } = new();

    public InMemoryCategoryRepository Categories { get; } = new();

    public FakeSaleRepository Sales { get; } = new();

    public FakeDialogService Dialogs { get; } = new();

    public FakeClock Clock { get; } = new();

    public FakeDiagnosticsExporter Exporter { get; } = new();

    public FakeClipboard Clipboard { get; } = new();

    public CollectingSink Sink { get; } = new();

    public UseCases UseCases => _provider.GetRequiredService<UseCases>();

    public OperationRunner Runner => _provider.GetRequiredService<OperationRunner>();

    public T Get<T>()
        where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
