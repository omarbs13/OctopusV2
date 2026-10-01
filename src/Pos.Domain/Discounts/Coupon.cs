using System.Text.RegularExpressions;
using Pos.Domain.Common;

namespace Pos.Domain.Discounts;

/// <summary>
/// Cupón de descuento global (agregado, 015 research §9). No se borra: se desactiva. La vigencia es un día
/// de calendario local (inclusivo en ambos extremos). Los campos de auditoría los asigna la persistencia.
/// </summary>
public sealed partial class Coupon
{
    public const int CodeMinLength = 3;
    public const int CodeMaxLength = 30;

    private Coupon()
    {
        Code = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Recortado y en mayúsculas, <c>[A-Z0-9-]{3,30}</c>; único.</summary>
    public string Code { get; private set; }

    public DiscountMode Mode { get; private set; }

    /// <summary>Puntos base o centavos, según <see cref="Mode"/>.</summary>
    public long Value { get; private set; }

    public DateOnly StartsOn { get; private set; }

    public DateOnly EndsOn { get; private set; }

    /// <summary>Usos permitidos; nulo = sin límite.</summary>
    public int? UsageLimit { get; private set; }

    public int UsesCount { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    /// <summary>Estándar del Principio IV; siempre nulo: el cupón se desactiva, no se borra.</summary>
    public DateTime? DeletedAt { get; private set; }

    public int Version { get; private set; }

    public DiscountValue Discount => DiscountValue.Create(Mode, Value);

    /// <summary>Usos restantes; nulo si no hay límite.</summary>
    public int? RemainingUses => UsageLimit is { } limit ? Math.Max(0, limit - UsesCount) : null;

    public static Coupon Create(string code, DiscountValue value, DateOnly startsOn, DateOnly endsOn, int? usageLimit)
    {
        var coupon = new Coupon
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            Version = 1,
        };
        coupon.SetCode(code);
        coupon.SetValue(value);
        coupon.Update(startsOn, endsOn, usageLimit);
        return coupon;
    }

    /// <summary>Recorta y pasa a mayúsculas; los códigos se comparan sin distinguir mayúsculas ni espacios de los extremos.</summary>
    public static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Indica si el código normalizado tiene el formato de un cupón.</summary>
    public static bool IsValidCode(string? code) => CodePattern().IsMatch(NormalizeCode(code));

    /// <summary>Cambia la vigencia y el límite de usos; el límite no puede quedar debajo de los usos realizados (FR-010).</summary>
    public void Update(DateOnly startsOn, DateOnly endsOn, int? usageLimit)
    {
        if (endsOn < startsOn)
        {
            throw new DomainException("La fecha de fin no puede ser anterior a la de inicio.");
        }

        if (usageLimit is <= 0)
        {
            throw new DomainException("El límite de usos debe ser mayor que 0 o quedar vacío.");
        }

        if (usageLimit is { } limit && limit < UsesCount)
        {
            throw new DomainException($"El límite de usos no puede ser menor que los usos realizados ({UsesCount}).");
        }

        StartsOn = startsOn;
        EndsOn = endsOn;
        UsageLimit = usageLimit;
    }

    /// <summary>Cambia código y valor; con usos registrados no se permite (FR-010).</summary>
    public void Edit(string code, DiscountValue value)
    {
        var normalized = NormalizeCode(code);
        if (normalized == Code && value == Discount)
        {
            return;
        }

        if (UsesCount > 0)
        {
            throw new DomainException("El cupón ya se usó; solo puede cambiar la vigencia, el límite o desactivarlo.");
        }

        SetCode(normalized);
        SetValue(value);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Estado con la fecha local de hoy, en orden: inactivo, agotado, vencido, por iniciar, vigente.</summary>
    public CouponStatus StatusOn(DateOnly today)
    {
        if (!IsActive)
        {
            return CouponStatus.Inactive;
        }

        if (UsageLimit is { } limit && UsesCount >= limit)
        {
            return CouponStatus.Exhausted;
        }

        if (today > EndsOn)
        {
            return CouponStatus.Expired;
        }

        return today < StartsOn ? CouponStatus.NotStarted : CouponStatus.Active;
    }

    /// <summary>Cuenta un uso al cobrar (FR-014); el cupón debe estar vigente.</summary>
    public void ConsumeUse(DateOnly today)
    {
        if (StatusOn(today) != CouponStatus.Active)
        {
            throw new DomainException("El cupón no está vigente.");
        }

        UsesCount++;
    }

    /// <summary>Devuelve un uso por la cancelación completa de la venta (013); no baja de 0.</summary>
    public void ReleaseUse()
    {
        if (UsesCount > 0)
        {
            UsesCount--;
        }
    }

    private void SetCode(string code)
    {
        var normalized = NormalizeCode(code);
        if (!CodePattern().IsMatch(normalized))
        {
            throw new DomainException(
                $"El código debe tener de {CodeMinLength} a {CodeMaxLength} caracteres: letras, números o guiones.");
        }

        Code = normalized;
    }

    private void SetValue(DiscountValue value)
    {
        // Revalida: un default(DiscountValue) no pasó por Create.
        var checkedValue = DiscountValue.Create(value.Mode, value.Raw);
        Mode = checkedValue.Mode;
        Value = checkedValue.Raw;
    }

    [GeneratedRegex("^[A-Z0-9-]{3,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
