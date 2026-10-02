using Avalonia;
using Avalonia.Controls;

namespace Pos.Desktop.Common.Scanner;

/// <summary>Campo de texto enfocado y su instantánea, para restaurarlo tras una lectura ignorada (021, research §7).</summary>
internal static class ScanFocus
{
    public static TextBox? FocusedTextBox(Visual visual) =>
        TopLevel.GetTopLevel(visual)?.FocusManager?.GetFocusedElement() as TextBox;

    public static TextBoxSnapshot? Snapshot(TextBox? box) =>
        box is null ? null : new TextBoxSnapshot(box.Text ?? string.Empty, box.CaretIndex);

    /// <summary>Devuelve al campo enfocado el texto y el cursor que tenía al empezar la lectura.</summary>
    public static void Restore(Visual visual, TextBoxSnapshot? snapshot)
    {
        if (snapshot is null || FocusedTextBox(visual) is not { } box)
        {
            return;
        }

        box.Text = snapshot.Text;
        box.CaretIndex = Math.Min(snapshot.CaretIndex, snapshot.Text.Length);
    }
}
