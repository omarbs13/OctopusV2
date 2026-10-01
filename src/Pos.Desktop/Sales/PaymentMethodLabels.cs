using Pos.Desktop.Resources;
using Pos.Domain.Sales;

namespace Pos.Desktop.Sales;

/// <summary>Etiqueta de una forma de pago en la interfaz.</summary>
internal static class PaymentMethodLabels
{
    public static string Of(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => Strings.Payment_Cash,
        PaymentMethod.Card => Strings.Payment_Card,
        PaymentMethod.CreditNote => Strings.Payment_CreditNote,
        _ => Strings.Payment_Transfer,
    };
}

/// <summary>Opción del selector de forma de pago no en efectivo.</summary>
public sealed record PaymentMethodOption(PaymentMethod Method, string Label);
