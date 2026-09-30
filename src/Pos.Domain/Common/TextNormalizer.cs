using System.Globalization;
using System.Text;

namespace Pos.Domain.Common;

public static class TextNormalizer
{
    /// <summary>
    /// Forma de búsqueda de un texto: sin acentos ni diacríticos y en minúsculas invariantes.
    /// Conserva el resto de caracteres.
    /// </summary>
    public static string ForSearch(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
