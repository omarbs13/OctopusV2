using Microsoft.Extensions.DependencyInjection;
using Pos.Application;
using Pos.Application.Abstractions;
using Pos.Application.Diagnostics;
using Pos.Application.Products;
using Pos.Application.Tests.TestSupport;
using Pos.Desktop.Common;

namespace Pos.Desktop.Tests.TestSupport;

/// <summary>
/// Contenedor para probar ViewModels con los casos de uso reales de Application y un repositorio
/// en memoria.
/// </summary>
public sealed class DesktopTestHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public DesktopTestHost()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<IProductRepository>(Repository);
        services.AddSingleton<ICurrentUser, FixedCurrentUser>();
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IAppInfo, FakeAppInfo>();
        services.AddSingleton<IAppPaths, FakeAppPaths>();
        services.AddSingleton<IDiagnosticsExporter>(Exporter);
        services.AddSingleton<IClipboardService>(Clipboard);
        services.AddSingleton<IDialogService>(Dialogs);
        services.AddSingleton(Sink.CreateLogger());
        services.AddSingleton<OperationRunner>();
        services.AddSingleton<UseCases>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public InMemoryProductRepository Repository { get; } = new();

    public FakeDialogService Dialogs { get; } = new();

    public FakeClock Clock { get; } = new();

    public FakeDiagnosticsExporter Exporter { get; } = new();

    public FakeClipboard Clipboard { get; } = new();

    public CollectingSink Sink { get; } = new();

    public UseCases UseCases => _provider.GetRequiredService<UseCases>();

    public OperationRunner Runner => _provider.GetRequiredService<OperationRunner>();

    public T Get<T>()
        where T : notnull => _provider.GetRequiredService<T>();

    public void Dispose() => _provider.Dispose();
}
