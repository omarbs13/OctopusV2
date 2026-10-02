using Pos.Application.Reports.Export;

namespace Pos.Application.Audit.ExportAuditLog;

/// <summary>Exporta todas las entradas del filtro; el rango de fechas es obligatorio (018, FR-023).</summary>
public sealed record ExportAuditLogCommand(AuditFilter Filter, ExportFormat Format);
