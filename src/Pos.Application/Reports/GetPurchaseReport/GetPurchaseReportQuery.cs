namespace Pos.Application.Reports.GetPurchaseReport;

/// <summary>Filtros capturados del reporte de compras; los importes son texto y se validan en el caso de uso.</summary>
public sealed record GetPurchaseReportQuery(
    Guid? SupplierId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string? MinTotalText,
    string? MaxTotalText,
    bool IncludeVoided,
    int Page = 1);
