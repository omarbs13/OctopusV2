namespace Pos.Application.CashShifts.CountShiftCash;

/// <summary>Conteo de efectivo del arqueo ciego; <c>DiscardHeldSale</c> confirma el descarte de la venta conservada del dueño.</summary>
public sealed record CountShiftCashCommand(Guid ShiftId, long CountedCents, bool DiscardHeldSale = false);
