namespace Pos.Application.CashShifts.CloseShift;

/// <summary>
/// Cierra el turno con las cifras que vio el usuario: <c>ShownExpectedCents</c> y <c>ExpectedVersion</c>
/// vienen de <c>CountShiftCash</c>; si ya no coinciden se devuelve <c>ShiftChanged</c> o <c>Conflict</c>.
/// </summary>
public sealed record CloseShiftCommand(
    Guid ShiftId,
    int ExpectedVersion,
    long CountedCents,
    long ShownExpectedCents,
    string? Comment,
    bool DiscardHeldSale = false);
