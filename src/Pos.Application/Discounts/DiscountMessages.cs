using System.Globalization;
using Pos.Domain.Discounts;

namespace Pos.Application.Discounts;

/// <summary>Mensajes de descuentos y cupones, en español y sin detalles técnicos (contracts/ui.md).</summary>
public static class DiscountMessages
{
    public const string CodeRequired = "Capture el código del cupón.";
    public const string CodeFormat = "El código debe tener de 3 a 30 caracteres: letras, números o guiones.";
    public const string CodeDuplicated = "Ya existe un cupón con ese código.";
    public const string CodeCollidesWithProduct = "El código coincide con el código de barras o la clave de un producto.";
    public const string CouponHasUses = "El cupón ya se usó; solo puede cambiar la vigencia, el límite o desactivarlo.";
    public const string DatesInvalid = "La fecha de fin no puede ser anterior a la de inicio.";
    public const string UsageLimitInvalid = "El límite de usos debe ser mayor que 0 o quedar vacío.";
    public const string LimitRange = "El límite debe estar entre 0 y 100 %.";
    public const string ModeInvalid = "La modalidad del descuento no es válida.";
    public const string ApprovalNotNeeded = "El descuento no supera el límite; no requiere autorización.";
    public const string DiscountsDropped =
        "Los descuentos de la venta conservada se quitaron porque el módulo Descuentos y promociones no está activo.";

    public static string CouponNotFound(string code) => $"El código {code} no corresponde a ningún producto ni cupón.";

    public static string CouponAlreadyApplied(string code) => $"La venta ya tiene el cupón {code}; quítelo para aplicar otro.";

    public static string OrderDiscountRemoved(string amount) =>
        $"El descuento de {amount} se quitó porque supera el subtotal. Vuelva a aplicarlo si corresponde.";

    /// <summary>Causa por la que un cupón no se puede aplicar; nulo si está vigente.</summary>
    public static string? CouponRejected(string code, CouponStatus status, DateOnly startsOn, DateOnly endsOn) => status switch
    {
        CouponStatus.Inactive => $"El cupón {code} está desactivado.",
        CouponStatus.NotStarted => $"El cupón {code} es válido a partir del {FormatDate(startsOn)}.",
        CouponStatus.Expired => $"El cupón {code} venció el {FormatDate(endsOn)}.",
        CouponStatus.Exhausted => $"El cupón {code} ya alcanzó su límite de usos.",
        _ => null,
    };

    public static string CouponRejected(CouponLookupDto coupon)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        return CouponRejected(coupon.Code, coupon.Status, coupon.StartsOn, coupon.EndsOn) ?? string.Empty;
    }

    public static string StatusText(CouponStatus status) => status switch
    {
        CouponStatus.Active => "Vigente",
        CouponStatus.NotStarted => "Por iniciar",
        CouponStatus.Expired => "Vencido",
        CouponStatus.Exhausted => "Agotado",
        _ => "Inactivo",
    };

    public static string KindText(DiscountKind kind) => kind switch
    {
        DiscountKind.Line => "Línea",
        DiscountKind.Order => "Venta",
        _ => "Cupón",
    };

    public static string FormatDate(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
