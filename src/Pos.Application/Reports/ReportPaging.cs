namespace Pos.Application.Reports;

/// <summary>Tamaños de página de los reportes.</summary>
public static class ReportPaging
{
    /// <summary>Pantalla: 100 por página (FR-013).</summary>
    public const int ScreenPageSize = 100;

    /// <summary>Exportación: todos los registros del filtro, sin paginar (FR-018).</summary>
    public const int All = 1_000_000;
}
