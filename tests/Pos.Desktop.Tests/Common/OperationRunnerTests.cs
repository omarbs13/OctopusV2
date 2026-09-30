using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Tests.TestSupport;
using Serilog.Events;

namespace Pos.Desktop.Tests.Common;

public class OperationRunnerTests
{
    private readonly CollectingSink _sink = new();
    private readonly FakeDialogService _dialogs = new();

    private OperationRunner CreateRunner() => new(_sink.CreateLogger(), _dialogs, new FixedCurrentUser());

    [Fact]
    public async Task RunAsync_OperacionExitosa_DevuelveVerdaderoSinMensajes()
    {
        var executed = false;

        var ok = await CreateRunner().RunAsync("Prueba", () =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        Assert.True(ok);
        Assert.True(executed);
        Assert.Empty(_dialogs.Messages);
        Assert.DoesNotContain(_sink.Events, e => e.Level >= LogEventLevel.Error);
    }

    [Fact]
    public async Task RunAsync_Excepcion_SeRegistraConOperacionUsuarioEIdentificadores()
    {
        var productId = Guid.NewGuid();

        var ok = await CreateRunner().RunAsync(
            "GuardarProducto",
            () => throw new InvalidOperationException("falla simulada"),
            new Dictionary<string, object?> { ["ProductId"] = productId });

        Assert.False(ok);
        var error = Assert.Single(_sink.Events, e => e.Level == LogEventLevel.Error);
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Equal("\"GuardarProducto\"", error.Properties["Operation"].ToString());
        Assert.Equal(FixedCurrentUser.Id.ToString(), error.Properties["UserId"].ToString());
        Assert.Equal(productId.ToString(), error.Properties["ProductId"].ToString());
    }

    [Fact]
    public async Task RunAsync_Excepcion_MuestraMensajeGenericoSinDetallesTecnicosYNoPropaga()
    {
        var ok = await CreateRunner().RunAsync("Operacion", () => throw new InvalidOperationException("SQLITE_IOERR detalle interno"));

        Assert.False(ok);
        var message = Assert.Single(_dialogs.Messages);
        Assert.Equal(Strings.Common_UnexpectedError, message.Message);
        Assert.DoesNotContain("SQLITE", message.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ConResultado_DevuelveValorOPredeterminado()
    {
        var runner = CreateRunner();

        var value = await runner.RunAsync("Valor", () => Task.FromResult(42));
        var failed = await runner.RunAsync<int>("Falla", () => throw new InvalidOperationException());

        Assert.Equal((true, 42), value);
        Assert.Equal((false, 0), failed);
    }
}
