using Pos.Application.Reports.GetCashCountReport;

namespace Pos.Application.Reports;

/// <summary>Lector de solo lectura de turnos para el arqueo (research §1); un turno se asigna al período por su apertura.</summary>
public interface ICashCountReportReader
{
    /// <summary>Turnos abiertos entre <c>FromUtc</c> y <c>ToUtcExclusive</c>, del más antiguo al más reciente.</summary>
    Task<IReadOnlyList<CashCountRawRow>> GetAsync(ReportWindow window, Guid? cashierId, CancellationToken cancellationToken);
}
