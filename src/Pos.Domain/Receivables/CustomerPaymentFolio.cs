using System.Globalization;

namespace Pos.Domain.Receivables;

/// <summary>Formato del folio de abono: <c>AB-000001</c>.</summary>
public static class CustomerPaymentFolio
{
    public static string Format(long number) =>
        string.Create(CultureInfo.InvariantCulture, $"AB-{number:000000}");
}
