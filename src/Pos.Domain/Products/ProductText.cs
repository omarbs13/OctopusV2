using System.Text;
using System.Text.RegularExpressions;
using Pos.Domain.Common;

namespace Pos.Domain.Products;

/// <summary>
/// Normalización de los textos del proveedor (026): sirve para comparar modelos y medidas sin importar
/// mayúsculas, acentos, espacios ni la forma de escribir la "por" de las medidas. Los valores originales
/// nunca se modifican; solo se calculan claves.
/// </summary>
public static partial class ProductText
{
    private const string SkuPrefix = "P-";

    /// <summary>Mayúsculas, sin acentos y con los espacios múltiples reducidos a uno.</summary>
    public static string NormalizeModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return string.Empty;
        }

        return CollapseSpaces(TextNormalizer.ForSearch(model)).ToUpperInvariant();
    }

    /// <summary>
    /// Sin espacios, en mayúsculas y con "x", "X", "×" y "*" equivalentes a "X". Los dígitos y puntos
    /// ("1.22") no se tocan. Vacío se convierte en nulo.
    /// </summary>
    public static string? NormalizeMeasures(string? measures)
    {
        if (string.IsNullOrWhiteSpace(measures))
        {
            return null;
        }

        var builder = new StringBuilder(measures.Length);
        foreach (var c in TextNormalizer.ForSearch(measures))
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            builder.Append(c is '*' or '×' ? 'X' : char.ToUpperInvariant(c));
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>Texto con forma de medida: números separados por "x", "X", "×" o "*" ("60 x 60", "1.22*2.44").</summary>
    public static bool LooksLikeMeasures(string? text) => text is not null && MeasuresShape().IsMatch(text);

    /// <summary>Forma normalizada de la marca para buscar.</summary>
    public static string? NormalizeBrand(string? brand) =>
        string.IsNullOrWhiteSpace(brand) ? null : NormalizeModel(brand);

    /// <summary>Clave de identidad: <c>{proveedor|-}|{modelo}|{medidas}</c>.</summary>
    public static string BuildIdentityKey(Guid? supplierId, string model, string? measures) =>
        $"{(supplierId is { } id ? id.ToString("N") : "-")}|{NormalizeModel(model)}|{NormalizeMeasures(measures)}";

    /// <summary>Nombre derivado: modelo y, si hay, medidas; se trunca al máximo del nombre.</summary>
    public static string BuildName(string model, string? measures)
    {
        var name = string.IsNullOrEmpty(measures) ? model : $"{model} {measures}";
        return name.Length > Product.NameMaxLength ? name[..Product.NameMaxLength] : name;
    }

    /// <summary>Código automático cuando no se captura uno (único con alta probabilidad; el caso de uso lo verifica).</summary>
    public static string GenerateSku() => SkuPrefix + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    private static string CollapseSpaces(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previousSpace = true;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousSpace)
                {
                    builder.Append(' ');
                }

                previousSpace = true;
                continue;
            }

            builder.Append(c);
            previousSpace = false;
        }

        return builder.ToString().TrimEnd();
    }

    [GeneratedRegex(@"^\s*\d*\.?\d+(\s*[xX×*]\s*\d*\.?\d+)+\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex MeasuresShape();
}
