using Pos.Application.Audit.ExportAuditLog;

namespace Pos.Application.Audit.ConfirmAuditExport;

/// <summary>Registra una exportación de la bitácora ya guardada en disco (018, FR-025).</summary>
public sealed record ConfirmAuditExportCommand(AuditExportReceipt Receipt);
