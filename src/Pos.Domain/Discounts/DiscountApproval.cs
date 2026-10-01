using Pos.Domain.Common;

namespace Pos.Domain.Discounts;

/// <summary>
/// Aprobación de un Administrador para un descuento que supera el límite (015, research §7). Se crea al
/// aplicar el descuento, ligada a la venta en curso (<see cref="DraftId"/>), y se consulta al cobrar. Es
/// inmutable: sin <c>Version</c> ni borrado (plan, Complexity Tracking).
/// </summary>
public sealed class DiscountApproval
{
    private DiscountApproval()
    {
    }

    public Guid Id { get; private set; }

    public Guid DraftId { get; private set; }

    /// <summary>Usuario que aplicó el descuento.</summary>
    public Guid RequestedBy { get; private set; }

    /// <summary>Administrador que autorizó.</summary>
    public Guid AuthorizedBy { get; private set; }

    public DiscountScope Scope { get; private set; }

    /// <summary>Producto de la línea; obligatorio si <see cref="Scope"/> es <see cref="DiscountScope.Line"/>.</summary>
    public Guid? ProductId { get; private set; }

    /// <summary>Porcentaje equivalente aprobado, entre 1 y 10 000 puntos base.</summary>
    public long ApprovedBasisPoints { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static DiscountApproval Create(
        Guid draftId,
        Guid requestedBy,
        Guid authorizedBy,
        DiscountScope scope,
        Guid? productId,
        long approvedBasisPoints,
        DateTime utcNow)
    {
        if (draftId == Guid.Empty || requestedBy == Guid.Empty || authorizedBy == Guid.Empty)
        {
            throw new DomainException("La aprobación del descuento no es válida.");
        }

        if (scope == DiscountScope.Line ? productId is null || productId == Guid.Empty : productId is not null)
        {
            throw new DomainException("La aprobación de un descuento de línea requiere el producto.");
        }

        if (approvedBasisPoints is < 1 or > DiscountValue.MaxBasisPoints)
        {
            throw new DomainException("El porcentaje aprobado debe estar entre 0.01 % y 100 %.");
        }

        return new DiscountApproval
        {
            Id = Guid.CreateVersion7(),
            DraftId = draftId,
            RequestedBy = requestedBy,
            AuthorizedBy = authorizedBy,
            Scope = scope,
            ProductId = productId,
            ApprovedBasisPoints = approvedBasisPoints,
            CreatedAt = utcNow,
        };
    }

    /// <summary>Cubre el descuento si es del mismo alcance y producto y aprobó al menos el equivalente actual.</summary>
    public bool Covers(DiscountScope scope, Guid? productId, long equivalentBasisPoints) =>
        Scope == scope && ProductId == productId && ApprovedBasisPoints >= equivalentBasisPoints;
}
