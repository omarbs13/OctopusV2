using Pos.Domain.Reports;

namespace Pos.Application.Reports;

/// <summary>Ventana UTC <c>[FromUtc, ToUtcExclusive)</c> más el rango local que la originó.</summary>
public sealed record ReportWindow(ReportPeriod Period, DateTime FromUtc, DateTime ToUtcExclusive);
