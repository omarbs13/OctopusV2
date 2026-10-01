using Pos.Application.Products;
using Pos.Domain.Discounts;

namespace Pos.Application.Discounts;

/// <summary>Persistencia del agregado Cupón (015). Comparte la unidad de trabajo con los demás repositorios.</summary>
public interface ICouponRepository
{
    /// <summary>Cupón con seguimiento de cambios, o nulo.</summary>
    Task<Coupon?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Cupón con seguimiento de cambios cuyo código (ya normalizado) coincide exactamente, o nulo.</summary>
    Task<Coupon?> FindByCodeAsync(string normalizedCode, CancellationToken cancellationToken);

    /// <summary>Página de cupones ordenada por código; el estado se calcula con <paramref name="today"/>.</summary>
    Task<CouponPage> SearchAsync(CouponSearch search, DateOnly today, CancellationToken cancellationToken);

    void Add(Coupon coupon);

    /// <summary>
    /// Guarda la unidad de trabajo. <see cref="SaveStatus.Conflict"/> por concurrencia y
    /// <see cref="SaveStatus.Duplicate"/> con <see cref="DiscountFields.Code"/> si el código ya existe.
    /// </summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
