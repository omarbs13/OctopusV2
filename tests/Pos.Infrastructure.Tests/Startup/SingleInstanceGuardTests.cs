using Pos.Infrastructure.Startup;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Startup;

public sealed class SingleInstanceGuardTests : IDisposable
{
    private readonly TempDataDirectory _dir = new();
    private readonly string _pipeName = $"pos-test-{Guid.NewGuid():N}";

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void PrimeraInstancia_ObtieneElBloqueo_YLaSegundaNo()
    {
        using var first = SingleInstanceGuard.TryAcquire(_dir.Paths.LockFile, _pipeName);
        using var second = SingleInstanceGuard.TryAcquire(_dir.Paths.LockFile, _pipeName);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task SegundaInstancia_PideActivarALaPrimera()
    {
        using var first = SingleInstanceGuard.TryAcquire(_dir.Paths.LockFile, _pipeName)!;
        var activated = new TaskCompletionSource();
        first.ActivationRequested += (_, _) => activated.TrySetResult();

        var signaled = await SingleInstanceGuard.TrySignalExistingAsync(_pipeName, TimeSpan.FromSeconds(5));

        Assert.True(signaled);
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void AlLiberar_OtraInstanciaPuedeObtenerElBloqueo()
    {
        var first = SingleInstanceGuard.TryAcquire(_dir.Paths.LockFile, _pipeName);
        Assert.NotNull(first);
        first.Dispose();

        using var again = SingleInstanceGuard.TryAcquire(_dir.Paths.LockFile, _pipeName);

        Assert.NotNull(again);
    }

    [Fact]
    public async Task SinInstanciaExistente_LaSenalFallaSinLanzar()
    {
        var signaled = await SingleInstanceGuard.TrySignalExistingAsync(_pipeName, TimeSpan.FromMilliseconds(300));

        Assert.False(signaled);
    }
}
