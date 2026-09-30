using Pos.Domain.CashShifts;

namespace Pos.Application.CashShifts.RegisterCashMovement;

/// <summary><c>AuthorizationGrantId</c>: concesión de un administrador para el retiro de un cajero (FR-011).</summary>
public sealed record RegisterCashMovementCommand(
    Guid ShiftId,
    CashMovementType Type,
    long AmountCents,
    string Reason,
    Guid? AuthorizationGrantId = null);
