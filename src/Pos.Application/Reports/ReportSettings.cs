using Pos.Domain.Reports;

namespace Pos.Application.Reports;

/// <summary>
/// Configuración de reportes por instalación: umbral de alerta de arqueo en centésimas de por ciento
/// (500 = 5 %). Sin archivo o dañado devuelve el valor predeterminado (research §9).
/// </summary>
public sealed record ReportSettings
{
    public const long MinThresholdBasisPoints = 1;
    public const long MaxThresholdBasisPoints = 10_000;

    public long CashDifferenceAlertBasisPoints { get; init; } = CashDifferenceRule.DefaultThresholdBasisPoints;
}
