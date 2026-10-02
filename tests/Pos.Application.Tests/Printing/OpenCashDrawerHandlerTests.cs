using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Printing;
using Pos.Application.Printing.OpenCashDrawer;
using Pos.Application.Tests.TestSupport;

namespace Pos.Application.Tests.Printing;

public sealed class OpenCashDrawerHandlerTests
{
    private readonly FakeDrawer _drawer = new();
    private readonly FakeAudit _audit = new();
    private readonly FakeSettings _settings = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OpenCashDrawerHandler Handler =>
        new(new AllowAllAccessControl(), _drawer, _settings, _audit, new OpenCashDrawerValidator(), NullLogger<OpenCashDrawerHandler>.Instance);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Manual_MotivoVacio_ValidationFailedSinAbrirNiAuditar(string? reason)
    {
        var result = await Handler.HandleAsync(OpenCashDrawerCommand.Manual(reason), Ct);

        var failed = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(PrintingFields.Reason, Assert.Single(failed.Errors).Field);
        Assert.Equal(0, _drawer.Calls);
        Assert.Empty(_audit.Entries);
        Assert.Equal(0, _audit.Saves);
    }

    [Fact]
    public async Task Manual_MotivoMayorA200_SeRechaza()
    {
        var result = await Handler.HandleAsync(OpenCashDrawerCommand.Manual(new string('x', 201)), Ct);

        Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(0, _drawer.Calls);
    }

    [Fact]
    public async Task Manual_Exito_AudtaOkYGuarda()
    {
        _drawer.Next = DrawerOutcome.Ok("Caja");

        var result = await Handler.HandleAsync(OpenCashDrawerCommand.Manual("  Cambio de billetes  "), Ct);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(OpenCashDrawerHandler.AuditAction, entry.Action);
        Assert.Equal("CashDrawer", entry.EntityType);
        Assert.Equal("Cambio de billetes", entry.Reason);
        Assert.Equal("Resultado: OK", entry.Details);
        Assert.Equal(1, _audit.Saves);
    }

    [Fact]
    public async Task Manual_Falla_AuditaFalloYDevuelveDrawerFailed()
    {
        _drawer.Next = DrawerOutcome.Fail(DeviceFailure.IoError);

        var result = await Handler.HandleAsync(OpenCashDrawerCommand.Manual("Revisión"), Ct);

        Assert.IsType<DrawerFailed>(result.Error);
        var failed = Assert.Single(_audit.Entries);
        Assert.Equal("Revisión", failed.Reason);
        Assert.Equal("Resultado: FALLO", failed.Details);
        Assert.Equal(1, _audit.Saves);
    }

    [Fact]
    public async Task Manual_ExcepcionDelDispositivo_NoLanzaYAuditaFallo()
    {
        _drawer.Throw = new InvalidOperationException("boom");

        var result = await Handler.HandleAsync(OpenCashDrawerCommand.Manual("Revisión"), Ct);

        Assert.IsType<DrawerFailed>(result.Error);
        Assert.EndsWith("Resultado: FALLO", Assert.Single(_audit.Entries).Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cobro_Falla_DevuelveErrorSinLanzarYSinAuditar()
    {
        _drawer.Next = DrawerOutcome.Fail(DeviceFailure.Unavailable);

        var result = await Handler.HandleAsync(OpenCashDrawerCommand.ForSale(Guid.NewGuid()), Ct);

        Assert.IsType<DrawerFailed>(result.Error);
        Assert.Empty(_audit.Entries);
        Assert.Equal(0, _audit.Saves);
    }

    [Fact]
    public async Task Cobro_Exito_NoAudita()
    {
        var result = await Handler.HandleAsync(OpenCashDrawerCommand.ForSale(Guid.NewGuid()), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _drawer.Calls);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task SinImpresoraConfigurada_DevuelveNotConfigured()
    {
        _settings.Current = new PrintingSettings();

        var result = await Handler.HandleAsync(OpenCashDrawerCommand.ForSale(Guid.NewGuid()), Ct);

        Assert.IsType<NotConfigured>(result.Error);
        Assert.Equal(0, _drawer.Calls);
    }

    private sealed class FakeSettings : IPrintingSettingsStore
    {
        public PrintingSettings Current { get; set; } = new(UseVirtualPrinter: true);

        public PrintingSettings Load() => Current;

        public void Save(PrintingSettings settings) => Current = settings;
    }

    private sealed class FakeDrawer : ICashDrawer
    {
        public DrawerOutcome Next { get; set; } = DrawerOutcome.Ok("x");

        public Exception? Throw { get; set; }

        public int Calls { get; private set; }

        public Task<DrawerOutcome> OpenAsync(PrintingSettings settings, CancellationToken cancellationToken)
        {
            Calls++;
            return Throw is not null ? throw Throw : Task.FromResult(Next);
        }
    }

    private sealed class FakeAudit : IAuditLog
    {
        public List<AuditRecord> Entries { get; } = [];

        public int Saves { get; private set; }

        public void Add(string action, string entityType, Guid entityId, string? details, Guid? authorizedBy = null) =>
            Entries.Add(new AuditRecord(action, entityType, entityId, Details: details, AuthorizedBy: authorizedBy));

        public void Add(AuditRecord record) => Entries.Add(record);

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }
}
