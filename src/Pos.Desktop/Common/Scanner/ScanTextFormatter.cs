using System.Globalization;
using System.Text;

namespace Pos.Desktop.Common.Scanner;

/// <summary>
/// Texto de una lectura con lo invisible a la vista (021, contracts/ui.md): espacio como <c>·</c>,
/// tabulador como <c>[TAB]</c> y cualquier carácter de control o fuera de ASCII imprimible como
/// <c>[U+XXXX]</c> (por ejemplo <c>PROD·0042</c> o <c>750[U+00A0]1</c>).
/// </summary>
public static class ScanTextFormatter
{
    public static string ToVisible(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var visible = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            _ = c switch
            {
                ' ' => visible.Append('·'),
                '\t' => visible.Append("[TAB]"),
                > ' ' and < (char)0x7F => visible.Append(c),
                _ => visible.Append(CultureInfo.InvariantCulture, $"[U+{(int)c:X4}]"),
            };
        }

        return visible.ToString();
    }
}
