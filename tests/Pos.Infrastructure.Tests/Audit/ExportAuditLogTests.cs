using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Audit.ConfirmAuditExport;
using Pos.Application.Audit.ExportAuditLog;
using Pos.Application.Reports.Export;
using Pos.Application.Users.Session;
using Pos.Domain.Audit;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Business;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.TestSupport;
using Pos.Infrastructure.Users;

namespace Pos.Infrastructure.Tests.Audit;

/// <summary>
/// 018 (FR-023 a FR-025) sobre SQLite real: la exportación contiene todas las entradas filtradas, no se
/// registra al generarse y sí al confirmarse; sin rango se rechaza.
/// </summary>
public sealed class ExportAuditLogTests : IAsyncLifetime
{
    private const int EntryCount = 150;
    private static readonly DateTime From = new(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc);

    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AuditFilter Range => new(From, From.AddDays(1), null, null, null, null);

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _db.Clock.UtcNow = From.AddHours(1);
        await using var context = _db.CreateDbContext();
        for (var i = 0; i < EntryCount; i++)
        {
            // Las pares, con dos cambios de campo; las impares, sin cambios (como las anteriores a 0.13.0).
            IEnumerable<AuditFieldChange>? changes = i % 2 == 0 ? [new("Precio", "$1.00", "$2.00"), new("Nombre", "A", "B")] : null;
            context.AuditEntries.Add(AuditEntry.Create(AuditActions.ProductUpdated, AuditActions.ProductEntity, Guid.CreateVersion7(), $"entrada {i}", entityName: $"Producto {i}", changes: changes));
        }

        await context.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Xlsx_UnaFilaPorCambioOPorEntradaSinCambios_YNoRegistraNada()
    {
        var result = await ExportAsync(new ExportAuditLogCommand(Range, ExportFormat.Xlsx));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(EntryCount, result.Value.Receipt.EntryCount);
        using var workbook = new XLWorkbook(new MemoryStream(result.Value.File.Bytes));
        Assert.DoesNotContain(workbook.Worksheets, w => w.Name == "Gráficas");

        // Fila 1 título, fila 2 encabezados: 75 entradas × 2 cambios + 75 sin cambios.
        var detail = workbook.Worksheet("Detalle");
        Assert.Equal((EntryCount / 2 * 2) + (EntryCount / 2), detail.LastRowUsed()!.RowNumber() - 2);
        Assert.True(detail.Cell(3, 1).Value.IsDateTime);
        Assert.Equal(EntryCount, await CountAsync());
    }

    [Fact]
    public async Task Pdf_SeGeneraCompletoSinRegistrarNada()
    {
        var result = await ExportAsync(new ExportAuditLogCommand(Range, ExportFormat.Pdf));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(EntryCount, result.Value.Receipt.EntryCount);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(result.Value.File.Bytes, 0, 4), StringComparison.Ordinal);
        Assert.EndsWith(".pdf", result.Value.File.FileName, StringComparison.Ordinal);
        Assert.Equal(EntryCount, await CountAsync());
    }

    [Fact]
    public async Task Confirmar_RegistraLaExportacionConFormatoRangoFiltrosYEntradas()
    {
        var export = await ExportAsync(new ExportAuditLogCommand(Range with { Entity = AuditEntityGroup.Product }, ExportFormat.Xlsx));

        await using (var context = _db.CreateDbContext())
        {
            var confirmed = await new ConfirmAuditExportHandler(new AllowAllAccessControl(), new AuditLog(context), Builder(context))
                .HandleAsync(new ConfirmAuditExportCommand(export.Value.Receipt), Ct);
            Assert.True(confirmed.IsSuccess, confirmed.Error?.ToString());
        }

        await using var read = _db.CreateDbContext();
        var entry = await read.AuditEntries.AsNoTracking().SingleAsync(e => e.Action == AuditActions.AuditExported, Ct);
        Assert.Equal(AuditActions.AuditLogEntity, entry.EntityType);
        Assert.Contains("Formato: XLSX", entry.Details, StringComparison.Ordinal);
        Assert.Contains("Entidad: Producto", entry.Details, StringComparison.Ordinal);
        Assert.Contains($"Entradas: {EntryCount}", entry.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SinRango_SeRechaza()
    {
        var result = await ExportAsync(new ExportAuditLogCommand(Range with { ToUtcExclusive = null }, ExportFormat.Pdf));

        var validation = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(ExportAuditLogValidator.RangeRequired, validation.Errors[0].Message);
    }

    private async Task<Result<AuditExport>> ExportAsync(ExportAuditLogCommand command)
    {
        await using var context = _db.CreateDbContext();
        return await new ExportAuditLogHandler(
                new AllowAllAccessControl(),
                new AuditLogReader(context),
                Builder(context),
                new PdfReportWriter(),
                new XlsxReportWriter(new ChartRenderer()),
                new ExportAuditLogValidator(),
                NullLogger<ExportAuditLogHandler>.Instance)
            .HandleAsync(command, Ct);
    }

    private AuditLogDocumentBuilder Builder(PosDbContext context) =>
        new(new BusinessProfileRepository(context), new UserRepository(context), new UserSession(), _db.Clock);

    private async Task<int> CountAsync()
    {
        await using var context = _db.CreateDbContext();
        return await context.AuditEntries.CountAsync(Ct);
    }
}
