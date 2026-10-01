using Pos.Domain.Returns;

namespace Pos.Application.Returns.PreviewReturn;

/// <summary>Vista previa de una devolución: <c>Lines</c> nulo = toda la venta.</summary>
public sealed record PreviewReturnCommand(Guid SaleId, IReadOnlyList<ReturnLineRequest>? Lines);
