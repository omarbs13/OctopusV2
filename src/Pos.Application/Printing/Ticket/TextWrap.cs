using System.Text;

namespace Pos.Application.Printing.Ticket;

/// <summary>Ajuste de texto a columnas fijas para el ticket: por palabras, cortando las muy largas.</summary>
public static class TextWrap
{
    /// <summary>
    /// Parte el texto en renglones de hasta <paramref name="width"/> caracteres. Respeta los saltos de
    /// línea, parte por palabras y corta una palabra más larga que el ancho sin perder caracteres.
    /// </summary>
    public static IReadOnlyList<string> Wrap(string? text, int width)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);

        var lines = new List<string>();
        foreach (var paragraph in (text ?? string.Empty).ReplaceLineEndings("\n").Split('\n'))
        {
            WrapParagraph(paragraph, width, lines);
        }

        return lines;
    }

    /// <summary>Acomoda el texto en el ancho indicado según la alineación (rellena con espacios).</summary>
    public static string Align(string text, int width, TicketAlignment alignment)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length >= width)
        {
            return text;
        }

        var free = width - text.Length;
        return alignment switch
        {
            TicketAlignment.Right => new string(' ', free) + text,
            TicketAlignment.Center => new string(' ', free / 2) + text,
            _ => text,
        };
    }

    /// <summary>Una línea con texto a la izquierda y otro a la derecha; el de la derecha nunca se corta.</summary>
    public static string TwoColumns(string left, string right, int width)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var room = width - right.Length - 1;
        var shown = room <= 0 ? string.Empty : left.Length > room ? left[..room] : left;
        return shown.PadRight(Math.Max(width - right.Length, shown.Length)) + right;
    }

    private static void WrapParagraph(string paragraph, int width, List<string> lines)
    {
        var current = new StringBuilder();
        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var remaining = word;
            if (current.Length > 0 && current.Length + 1 + remaining.Length <= width)
            {
                current.Append(' ').Append(remaining);
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            while (remaining.Length > width)
            {
                lines.Add(remaining[..width]);
                remaining = remaining[width..];
            }

            current.Append(remaining);
        }

        lines.Add(current.ToString());
    }
}
