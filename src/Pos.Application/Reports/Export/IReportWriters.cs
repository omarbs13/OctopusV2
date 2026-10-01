namespace Pos.Application.Reports.Export;

/// <summary>Escribe un <see cref="ReportDocument"/> como PDF A4 horizontal; lo implementa Infrastructure.</summary>
public interface IPdfReportWriter
{
    byte[] Write(ReportDocument document);
}

/// <summary>Escribe un <see cref="ReportDocument"/> como XLSX con hojas de resumen, detalle y gráficas.</summary>
public interface IXlsxReportWriter
{
    byte[] Write(ReportDocument document);
}
