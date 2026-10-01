using Pos.Domain.Discounts;

namespace Pos.Application.Discounts;

/// <summary>Aprobaciones de descuentos sobre el límite, ligadas a la venta en curso (015, research §7).</summary>
public interface IDiscountApprovalStore
{
    void Add(DiscountApproval approval);

    /// <summary>Aprobaciones de la venta en curso <paramref name="draftId"/> pedidas por <paramref name="requestedBy"/>, sin seguimiento.</summary>
    Task<IReadOnlyList<DiscountApproval>> ListForDraftAsync(Guid draftId, Guid requestedBy, CancellationToken cancellationToken);

    /// <summary>Guarda la unidad de trabajo (la aprobación y su entrada de bitácora).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
