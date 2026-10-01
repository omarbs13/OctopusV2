namespace Pos.Application.Reports;

/// <summary>Almacén local de <see cref="ReportSettings"/> (preferencias de la máquina, research §9).</summary>
public interface IReportSettingsStore
{
    ReportSettings Load();

    void Save(ReportSettings settings);
}
