using System.Diagnostics;
using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Reports.Export;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Audit.ExportAuditLog;

/// <summary>
/// Genera el PDF o XLSX con todas las entradas filtradas, solo para el Administrador (018, FR-023). No toca
/// el sistema de archivos ni registra nada en la bitácora: la exportación se registra con
/// <c>ConfirmAuditExport</c> cuando la interfaz guardó el archivo (research §12).
/// </summary>
public sealed partial class ExportAuditLogHandler
{
    private const string PdfContentType = "application/pdf";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IAccessControl _access;
    private readonly IAuditLogReader _reader;
    private readonly AuditLogDocumentBuilder _builder;
    private readonly IPdfReportWriter _pdf;
    private readonly IXlsxReportWriter _xlsx;
    private readonly IValidator<ExportAuditLogCommand> _validator;
    private readonly ILogger<ExportAuditLogHandler> _logger;

    public ExportAuditLogHandler(
        IAccessControl access,
        IAuditLogReader reader,
        AuditLogDocumentBuilder builder,
        IPdfReportWriter pdf,
        IXlsxReportWriter xlsx,
        IValidator<ExportAuditLogCommand> validator,
        ILogger<ExportAuditLogHandler> logger)
    {
        _access = access;
        _reader = reader;
        _builder = builder;
        _pdf = pdf;
        _xlsx = xlsx;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<AuditExport>> HandleAsync(ExportAuditLogCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ViewAuditLog, cancellationToken);
        if (!access.Allowed)
        {
            LogRejected(command.Format, "sin permiso");
            return Result.Failure<AuditExport>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            LogRejected(command.Format, validation.Errors[0].ErrorMessage);
            return Result.Failure<AuditExport>(ProductRules.ToError(validation));
        }

        var watch = Stopwatch.StartNew();
        var rows = await _reader.ListAsync(command.Filter, cancellationToken);
        var document = await _builder.BuildAsync(command.Filter, command.Format, rows, cancellationToken);
        var name = FileBaseName(command.Filter);
        var file = command.Format == ExportFormat.Pdf
            ? new ExportedFile($"{name}.pdf", PdfContentType, _pdf.Write(document), document.Business is null)
            : new ExportedFile($"{name}.xlsx", XlsxContentType, _xlsx.Write(document), document.Business is null);

        LogExported(command.Format, rows.Count, watch.ElapsedMilliseconds);
        return Result.Success(new AuditExport(file, new AuditExportReceipt(command.Format, command.Filter, rows.Count)));
    }

    /// <summary>"bitacora_20261001-20261031", con las fechas locales del rango (contracts/ui.md).</summary>
    public static string FileBaseName(AuditFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var from = filter.FromUtc?.ToLocalTime() ?? DateTime.Today;
        var to = filter.ToUtcExclusive?.ToLocalTime().AddDays(-1) ?? from;
        return string.Create(CultureInfo.InvariantCulture, $"bitacora_{from:yyyyMMdd}-{to:yyyyMMdd}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Bitácora exportada. Formato={Format} Entradas={Entries} DuracionMs={ElapsedMs}")]
    private partial void LogExported(ExportFormat format, int entries, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exportación de la bitácora rechazada. Formato={Format} Motivo={Reason}")]
    private partial void LogRejected(ExportFormat format, string reason);
}
