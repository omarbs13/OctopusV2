namespace Pos.Application.Sales.SaveSaleDraft;

/// <summary>Guarda el borrador de la venta en curso; sin líneas equivale a descartarlo.</summary>
public sealed record SaveSaleDraftCommand(Guid DraftId, IReadOnlyList<DraftLineDto> Lines);
