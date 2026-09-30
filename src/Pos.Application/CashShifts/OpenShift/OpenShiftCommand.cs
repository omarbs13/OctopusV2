namespace Pos.Application.CashShifts.OpenShift;

/// <summary>Abre el turno con el fondo inicial; un fondo de 0 requiere <c>ConfirmZeroFloat</c> (FR-003).</summary>
public sealed record OpenShiftCommand(long OpeningFloatCents, bool ConfirmZeroFloat = false);
