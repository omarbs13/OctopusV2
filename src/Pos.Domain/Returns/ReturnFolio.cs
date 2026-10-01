using System.Globalization;

namespace Pos.Domain.Returns;

/// <summary>Formato del folio de devolución: <c>D-000001</c>.</summary>
public static class ReturnFolio
{
    public static string Format(long number) =>
        string.Create(CultureInfo.InvariantCulture, $"D-{number:000000}");
}
