using Pos.Desktop.Diagnostics;

namespace Pos.Desktop.Tests.Diagnostics;

/// <summary>FR-017: un error que se repite se muestra una vez y se resume con su conteo.</summary>
public sealed class ErrorEpisodeGateTests
{
    private DateTimeOffset _now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private readonly List<ErrorSummary> _summaries = [];

    private ErrorEpisodeGate CreateGate() => new(() => _now, TimeSpan.FromSeconds(5), _summaries.Add);

    private static InvalidOperationException Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    [Fact]
    public void ErrorDistinto_EsNuevoYSeMuestra()
    {
        var gate = CreateGate();

        Assert.True(gate.Observe(Thrown("a")).IsNew);
        Assert.True(gate.Observe(new TimeoutException("otro tipo")).IsNew);
    }

    [Fact]
    public void DiezRepeticionesEnCincoSegundos_UnSoloAvisoYUnResumenConConteo()
    {
        var gate = CreateGate();
        var error = Thrown("falla");

        var observations = Enumerable.Range(0, 10).Select(_ => gate.Observe(error)).ToList();
        _now = _now.AddSeconds(6);
        gate.CloseExpired();

        Assert.Single(observations, o => o.IsNew);
        Assert.Equal(2, observations[1].Count);
        var summary = Assert.Single(_summaries);
        Assert.Equal(10, summary.Repetitions);
        Assert.Equal(typeof(InvalidOperationException).FullName, summary.ExceptionType);
    }

    [Fact]
    public void FueraDeLaVentana_EmpiezaUnEpisodioNuevo()
    {
        var gate = CreateGate();
        var error = Thrown("falla");
        gate.Observe(error);
        _now = _now.AddSeconds(6);

        Assert.True(gate.Observe(error).IsNew);
    }

    [Fact]
    public void SoloHayUnAvisoAbiertoALaVez()
    {
        var gate = CreateGate();

        Assert.True(gate.TryBeginNotification());
        Assert.False(gate.TryBeginNotification());
        gate.EndNotification();
        Assert.True(gate.TryBeginNotification());
    }
}
