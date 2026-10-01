namespace Pos.Domain.Discounts;

/// <summary>Estado de un cupón, derivado de la fecha local de hoy (015, data-model).</summary>
public enum CouponStatus
{
    /// <summary>Vigente: se puede aplicar.</summary>
    Active,

    /// <summary>Su vigencia aún no empieza.</summary>
    NotStarted,

    /// <summary>Su vigencia ya terminó.</summary>
    Expired,

    /// <summary>Alcanzó su límite de usos.</summary>
    Exhausted,

    /// <summary>Desactivado por el Administrador.</summary>
    Inactive,
}
