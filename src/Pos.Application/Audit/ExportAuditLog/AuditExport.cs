using Pos.Application.Reports.Export;

namespace Pos.Application.Audit.ExportAuditLog;

/// <summary>Archivo de la bitácora exportada y el comprobante para registrar la exportación (018, research §12).</summary>
public sealed record AuditExport(ExportedFile File, AuditExportReceipt Receipt);

/// <summary>Lo que se exportó: la interfaz lo envía a <c>ConfirmAuditExport</c> solo si el archivo se guardó.</summary>
public sealed record AuditExportReceipt(ExportFormat Format, AuditFilter Filter, int EntryCount);
