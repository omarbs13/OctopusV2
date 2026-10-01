using Pos.Desktop.Diagnostics;
using Pos.Desktop.Tests.TestSupport;
using Serilog;

namespace Pos.Desktop.Tests.Diagnostics;

/// <summary>FR-012: ninguna contraseña ni dato de tarjeta llega al log; los identificadores se conservan.</summary>
public sealed class SensitiveDataRedactorTests
{
    private static (ILogger Logger, CollectingSink Sink) Create()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SensitiveDataRedactor())
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    [Fact]
    public void PropiedadesSensibles_SalenEnmascaradasYLosIdentificadoresSeConservan()
    {
        var (logger, sink) = Create();
        var productId = Guid.NewGuid();

        logger
            .ForContext("Password", "secreto123")
            .ForContext("CardNumber", "4111111111111111")
            .ForContext("ProductId", productId)
            .ForContext("DiscardedLines", 3)
            .Error("Falla de prueba");

        var properties = sink.Events.Single().Properties;
        Assert.Equal("\"***\"", properties["Password"].ToString());
        Assert.Equal("\"***\"", properties["CardNumber"].ToString());
        Assert.Equal(productId.ToString(), ((Serilog.Events.ScalarValue)properties["ProductId"]).Value!.ToString());
        Assert.Equal("3", properties["DiscardedLines"].ToString());
    }

    [Fact]
    public void ObjetoConCampoSensible_SeEnmascaraDentro()
    {
        var (logger, sink) = Create();

        logger.Information("Entrada {@Request}", new { UserName = "ana", Pin = "1234" });

        var text = sink.Events.Single().Properties["Request"].ToString();
        Assert.Contains("\"ana\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1234", text, StringComparison.Ordinal);
    }
}
