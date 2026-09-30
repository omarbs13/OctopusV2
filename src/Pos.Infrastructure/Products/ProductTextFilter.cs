using Microsoft.EntityFrameworkCore;
using Pos.Domain.Products;

namespace Pos.Infrastructure.Products;

/// <summary>Filtro de texto de productos (nombre, SKU y código de barras), compartido por Productos y Existencias.</summary>
internal static class ProductTextFilter
{
    /// <summary>Carácter de escape para LIKE.</summary>
    public const string LikeEscape = @"\";

    public static IQueryable<Product> Apply(
        IQueryable<Product> query,
        string? nameText,
        string? skuText,
        string? barcodeText,
        bool barcodeExact)
    {
        if (nameText is null)
        {
            return query;
        }

        var namePattern = LikeContains(nameText);
        var skuPattern = LikeContains(skuText ?? nameText);
        var barcode = barcodeText ?? nameText;
        var barcodePattern = LikeContains(barcode);

        return barcodeExact
            ? query.Where(p =>
                EF.Functions.Like(p.NameSearch, namePattern, LikeEscape)
                || EF.Functions.Like(p.Sku, skuPattern, LikeEscape)
                || p.Barcode == barcode)
            : query.Where(p =>
                EF.Functions.Like(p.NameSearch, namePattern, LikeEscape)
                || EF.Functions.Like(p.Sku, skuPattern, LikeEscape)
                || EF.Functions.Like(p.Barcode, barcodePattern, LikeEscape));
    }

    /// <summary>Patrón LIKE de "contiene", escapando los comodines del texto buscado.</summary>
    private static string LikeContains(string text)
    {
        var escaped = text
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
