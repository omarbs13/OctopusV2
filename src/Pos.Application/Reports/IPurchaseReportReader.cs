namespace Pos.Application.Reports;

/// <summary>Filtro ya validado del reporte de compras; fechas de factura inclusivas (research §13).</summary>
public sealed record PurchaseReportFilter(
    Guid? SupplierId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    long? MinTotalCents,
    long? MaxTotalCents,
    bool IncludeVoided,
    int Page);

public sealed record PurchaseReportRow(
    Guid PurchaseId,
    DateOnly InvoiceDate,
    string SupplierName,
    string InvoiceNumber,
    int LineCount,
    long SubtotalCents,
    long TaxCents,
    long TotalCents,
    string RegisteredByName,
    bool IsVoided);

/// <summary>
/// Una página del reporte y los acumulados de todo el filtro. <see cref="TotalCount"/> (para paginar) incluye
/// las anuladas listadas; <see cref="PurchaseCount"/> y las sumas, solo las vigentes (FR-021, FR-021a).
/// </summary>
public sealed record PurchaseReportPage(
    IReadOnlyList<PurchaseReportRow> Rows,
    long TotalCount,
    int Page,
    long PurchaseCount,
    long SubtotalSumCents,
    long TaxSumCents,
    long TotalSumCents)
{
    public const int PageSize = ReportPaging.ScreenPageSize;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

/// <summary>Lectura de "Reportes > Compras" (020, research §13).</summary>
public interface IPurchaseReportReader
{
    Task<PurchaseReportPage> SearchAsync(PurchaseReportFilter filter, CancellationToken cancellationToken);
}
