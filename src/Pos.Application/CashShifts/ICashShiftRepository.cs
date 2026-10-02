using Pos.Application.Products;
using Pos.Domain.CashShifts;

namespace Pos.Application.CashShifts;

/// <summary>Persistencia del agregado Turno de caja. Específico del agregado; no hay repositorios genéricos.</summary>
public interface ICashShiftRepository
{
    /// <summary>Turno abierto de la caja, con movimientos y seguimiento de cambios; nulo si no hay.</summary>
    Task<CashShift?> GetOpenAsync(string registerCode, CancellationToken cancellationToken);

    /// <summary>Turno por id, con movimientos y seguimiento de cambios.</summary>
    Task<CashShift?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Datos del comprobante de un movimiento; nulo si no existe.</summary>
    Task<CashMovementReceiptDto?> FindMovementAsync(Guid movementId, CancellationToken cancellationToken);

    /// <summary>Datos del corte de un turno cerrado; nulo si no existe o sigue abierto.</summary>
    Task<ShiftReportDto?> GetReportAsync(Guid shiftId, CancellationToken cancellationToken);

    /// <summary><c>MAX(Number) + 1</c>; se llama dentro de la transacción de escritura.</summary>
    Task<long> NextNumberAsync(CancellationToken cancellationToken);

    void Add(CashShift shift);

    void AddMovement(CashMovement movement);

    Task<ShiftPage> SearchAsync(ShiftSearch search, CancellationToken cancellationToken);

    /// <summary>Detalle de solo lectura; el arqueo de un turno abierto usa <paramref name="openShiftTotals"/>.</summary>
    Task<ShiftDetailDto?> GetDetailAsync(Guid id, ShiftSalesTotals openShiftTotals, CancellationToken cancellationToken);

    /// <summary><c>MAX(Number) + 1</c> del tipo de corte; se llama dentro de la transacción de escritura (017, research §3).</summary>
    Task<long> NextCutNumberAsync(ShiftCutType type, CancellationToken cancellationToken);

    /// <summary>Agrega un corte; los cortes nunca se modifican ni se borran (FR-013).</summary>
    void AddCut(ShiftCut cut);

    /// <summary>Reporte fijo de un corte con los nombres resueltos; nulo si no existe.</summary>
    Task<ShiftCutReportDto?> GetCutReportAsync(Guid cutId, CancellationToken cancellationToken);

    /// <summary>Histórico de cortes del más reciente al más antiguo (<c>GeneratedAt DESC, Id DESC</c>).</summary>
    Task<ShiftCutPage> SearchCutsAsync(ShiftCutSearch search, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la unidad de trabajo. <see cref="SaveStatus.Duplicate"/> con
    /// <see cref="CashShiftFields.OpenPerRegister"/>, <see cref="CashShiftFields.Number"/> o
    /// <see cref="CashShiftFields.CutNumber"/>; <see cref="SaveStatus.Conflict"/> por versión.
    /// </summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
