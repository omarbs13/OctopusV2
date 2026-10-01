namespace Pos.Domain.Reports;

/// <summary>Períodos predefinidos que el usuario elige en los reportes (FR-003).</summary>
public enum ReportPreset
{
    Today,
    Yesterday,
    Last7Days,
    ThisMonth,
    PreviousMonth,
    Custom,
}
