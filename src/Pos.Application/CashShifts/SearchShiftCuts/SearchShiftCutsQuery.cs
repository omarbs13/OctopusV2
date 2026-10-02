using Pos.Domain.CashShifts;

namespace Pos.Application.CashShifts.SearchShiftCuts;

/// <summary>Filtros del "Histórico de cortes"; las fechas son locales de generación, ambas inclusive.</summary>
public sealed record SearchShiftCutsQuery(
    ShiftCutType? Type,
    DateOnly? From,
    DateOnly? To,
    Guid? UserId,
    int Page = 1);
