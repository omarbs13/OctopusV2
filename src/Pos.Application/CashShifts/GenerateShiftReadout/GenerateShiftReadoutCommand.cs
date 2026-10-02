namespace Pos.Application.CashShifts.GenerateShiftReadout;

/// <summary>Corte X del turno abierto de la caja; <c>AuthorizationGrantId</c> es la concesión de un administrador para el Cajero.</summary>
public sealed record GenerateShiftReadoutCommand(Guid? AuthorizationGrantId = null);

/// <summary>Corte generado: <c>Folio</c> es <c>X-000001</c> y <c>ShiftFolio</c>, <c>T-000123</c>.</summary>
public sealed record GeneratedShiftCut(Guid CutId, string Folio, string ShiftFolio);
