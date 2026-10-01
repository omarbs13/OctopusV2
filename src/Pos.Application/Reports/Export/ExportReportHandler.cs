using Pos.Application.Abstractions;
using Pos.Application.Audit;

namespace Pos.Application.Reports.Export;

/// <summary>
/// Genera el PDF o XLSX de un reporte con los mismos parámetros que la pantalla. Verifica el permiso del
/// reporte (a través de su caso de uso), no toca el sistema de archivos y audita la exportación con
/// reporte, formato, período y filtros, nunca con los datos exportados (FR-027).
/// </summary>
public sealed class ExportReportHandler
{
    private const string PdfContentType = "application/pdf";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly ReportDocumentBuilder _builder;
    private readonly IPdfReportWriter _pdf;
    private readonly IXlsxReportWriter _xlsx;
    private readonly IAuditLog _audit;

    public ExportReportHandler(ReportDocumentBuilder builder, IPdfReportWriter pdf, IXlsxReportWriter xlsx, IAuditLog audit)
    {
        _builder = builder;
        _pdf = pdf;
        _xlsx = xlsx;
        _audit = audit;
    }

    public async Task<Result<ExportedFile>> HandleAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // "Mi turno" solo se exporta a PDF (contracts/application-ports.md).
        if (request.Format == ExportFormat.Xlsx && request.Kind == ReportKind.MyShift)
        {
            return Result.Failure<ExportedFile>(new InvalidState("Este reporte solo se exporta a PDF."));
        }

        var built = await _builder.BuildAsync(request, cancellationToken);
        if (!built.IsSuccess)
        {
            return Result.Failure<ExportedFile>(built.Error);
        }

        var report = built.Value;
        var file = request.Format == ExportFormat.Pdf
            ? new ExportedFile($"{report.FileBaseName}.pdf", PdfContentType, _pdf.Write(report.Document), report.Document.Business is null)
            : new ExportedFile($"{report.FileBaseName}.xlsx", XlsxContentType, _xlsx.Write(report.Document), report.Document.Business is null);

        _audit.Add(
            AuditActions.ReportExported,
            AuditActions.ReportEntity,
            Guid.CreateVersion7(),
            $"Formato: {request.Format}; {report.AuditDetails}");
        await _audit.SaveAsync(cancellationToken);
        return Result.Success(file);
    }
}
