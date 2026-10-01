using Pos.Application.Users;
using Pos.Application.Users.Session;
using Pos.Desktop.Diagnostics;
using Pos.Desktop.Tests.TestSupport;
using Serilog;

namespace Pos.Desktop.Tests.Diagnostics;

/// <summary>FR-014: una falla del contexto de diagnóstico nunca afecta a quien registra.</summary>
public sealed class LoggingResilienceTests
{
    [Fact]
    public void ContextoQueFalla_NoImpideEscribirLaEntrada()
    {
        var context = new DiagnosticContext();
        context.BindSession(new ThrowingSession());
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new DiagnosticContextEnricher(context))
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Error("Se registra igual");

        var logEvent = Assert.Single(sink.Events);
        Assert.False(logEvent.Properties.ContainsKey("UserId"));
    }

    [Fact]
    public void ContextoConVentaYUsuario_AgregaPantallaYLineas()
    {
        var context = new DiagnosticContext();
        var userId = Guid.NewGuid();
        context.BindSession(new FixedSession(new SessionUser(userId, "Ana Pérez", "ana", Pos.Domain.Users.UserRole.Cashier, "AP")));
        context.SetScreen("sales.pos");
        context.SetSale(Guid.NewGuid(), 3);
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().Enrich.With(new DiagnosticContextEnricher(context)).WriteTo.Sink(sink).CreateLogger();

        logger.Error("Con contexto");

        var properties = sink.Events.Single().Properties;
        Assert.Equal("\"sales.pos\"", properties["Screen"].ToString());
        Assert.Equal("3", properties["SaleLines"].ToString());
        Assert.Contains(userId.ToString(), properties["UserId"].ToString(), StringComparison.Ordinal);
    }

    private sealed class ThrowingSession : IUserSession
    {
        public SessionUser? User => throw new InvalidOperationException("sesión no disponible");

        public Guid UserId => throw new InvalidOperationException("sesión no disponible");

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }

    private sealed class FixedSession(SessionUser user) : IUserSession
    {
        public SessionUser? User => user;

        public Guid UserId => user.Id;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }
}
