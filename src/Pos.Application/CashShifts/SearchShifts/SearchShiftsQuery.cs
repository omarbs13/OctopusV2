using Pos.Domain.CashShifts;

namespace Pos.Application.CashShifts.SearchShifts;

/// <summary>Filtros de "Turnos"; el rango es de la fecha de apertura y el límite superior es exclusivo (UTC).</summary>
public sealed record SearchShiftsQuery(
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    Guid? UserId,
    CashShiftStatus? Status,
    int Page = 1);
