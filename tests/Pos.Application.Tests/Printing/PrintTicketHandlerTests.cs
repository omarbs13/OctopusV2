using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.Printing;
using Pos.Application.Printing.PrintTicket;
using Pos.Application.Printing.Ticket;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Domain.Business;
using Pos.Domain.Sales;

namespace Pos.Application.Tests.Printing;

public sealed class PrintTicketHandlerTests
{
    private readonly FakeSettings _settings = new(new PrintingSettings(UseVirtualPrinter: true));
    private readonly FakePrinter _printer = new();
    private readonly FakeSales _sales = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PrintTicketHandler Handler =>
        new(_sales, new FakeProfiles(), _settings, _printer, NullLogger<PrintTicketHandler>.Instance);

    [Fact]
    public async Task Muestra_ImprimeElTicketDePruebaEnElAnchoConfigurado()
    {
        _settings.Current = _settings.Current with { PaperWidth = PaperWidth.Mm58 };
        _printer.Next = PrintOutcome.Ok("/tickets/prueba.txt");

        var result = await Handler.HandleAsync(new PrintTicketCommand(PrintSource.Sample), Ct);

        Assert.Equal("/tickets/prueba.txt", result.Value.Destination);
        Assert.Equal(32, _printer.Last!.Columns);
        Assert.Equal(0, _sales.Reads);
    }

    [Theory]
    [InlineData(DeviceFailure.Unavailable, typeof(PrinterUnavailable))]
    [InlineData(DeviceFailure.IoError, typeof(PrintFailed))]
    [InlineData(DeviceFailure.NotConfigured, typeof(NotConfigured))]
    public async Task FallaDelDispositivo_DevuelveErrorSinLanzar(DeviceFailure failure, Type expected)
    {
        _printer.Next = PrintOutcome.Fail(failure);

        var result = await Handler.HandleAsync(new PrintTicketCommand(PrintSource.Sample), Ct);

        Assert.IsType(expected, result.Error);
    }

    [Fact]
    public async Task ExcepcionDelDispositivo_DevuelvePrintFailedSinLanzar()
    {
        _printer.Throw = new InvalidOperationException("boom");

        var result = await Handler.HandleAsync(new PrintTicketCommand(PrintSource.Sample), Ct);

        Assert.IsType<PrintFailed>(result.Error);
    }

    [Fact]
    public async Task SinImpresoraConfigurada_DevuelveNotConfiguredSinImprimir()
    {
        _settings.Current = new PrintingSettings();

        var result = await Handler.HandleAsync(new PrintTicketCommand(PrintSource.Sample), Ct);

        Assert.IsType<NotConfigured>(result.Error);
        Assert.Null(_printer.Last);
    }

    [Fact]
    public async Task Venta_ImprimeConLaLeyendaDeReimpresionYSinInexistenteDevuelveNotFound()
    {
        var saleId = Guid.NewGuid();
        _sales.Detail = new SaleDetailDto(
            saleId, "V-000001", DateTime.UtcNow, "Ana", 100, SaleStatus.Completed, 1, null, null, null,
            [new SaleLineDto(1, Guid.NewGuid(), "Producto", "P", "H87", 0, 100, 1000, 100)],
            [new SalePaymentDto(PaymentMethod.Card, 100, null, null, null)]);
        _printer.Next = PrintOutcome.Ok("impresora");

        var ok = await Handler.HandleAsync(new PrintTicketCommand(PrintSource.Sale(saleId), IsReprint: true), Ct);

        Assert.True(ok.IsSuccess);
        Assert.Contains(_printer.Last!.Lines, l => l.Text == TicketBuilder.ReprintLegend);

        _sales.Detail = null;
        var missing = await Handler.HandleAsync(new PrintTicketCommand(PrintSource.Sale(Guid.NewGuid())), Ct);
        Assert.IsType<NotFound>(missing.Error);
    }

    private sealed class FakeSettings(PrintingSettings current) : IPrintingSettingsStore
    {
        public PrintingSettings Current { get; set; } = current;

        public PrintingSettings Load() => Current;

        public void Save(PrintingSettings settings) => Current = settings;
    }

    private sealed class FakePrinter : ITicketPrinter
    {
        public PrintOutcome Next { get; set; } = PrintOutcome.Ok("x");

        public Exception? Throw { get; set; }

        public TicketDocument? Last { get; private set; }

        public Task<PrintOutcome> PrintAsync(TicketDocument ticket, PrintingSettings settings, CancellationToken cancellationToken)
        {
            if (Throw is not null)
            {
                throw Throw;
            }

            Last = ticket;
            return Task.FromResult(Next);
        }
    }

    private sealed class FakeProfiles : IBusinessProfileRepository
    {
        public Task<BusinessProfile?> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult<BusinessProfile?>(BusinessProfile.Create("Tienda", "Calle 1", "555", null, null));

        public void Add(BusinessProfile profile)
        {
        }

        public Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(SaveOutcome.Saved);
    }

    private sealed class FakeSales : ISaleRepository
    {
        public SaleDetailDto? Detail { get; set; }

        public int Reads { get; private set; }

        public Task<SaleDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(Detail);
        }

        public Task<bool> ExistsForDraftAsync(Guid draftId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ConfirmedSale?> FindByDraftAsync(Guid draftId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long> NextFolioNumberAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Sale?> GetAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(Sale sale) => throw new NotSupportedException();

        public Task<SalePage> SearchAsync(SaleSearch search, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesDashboard> GetDashboardAsync(IReadOnlyList<DayWindow> days, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
