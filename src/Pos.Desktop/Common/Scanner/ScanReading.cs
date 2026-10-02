namespace Pos.Desktop.Common.Scanner;

/// <summary>Tecla que cerró una lectura; <see cref="None"/> = silencio de 300 ms.</summary>
public enum Terminator
{
    Enter,
    Tab,
    None,
}

/// <summary>Texto y posición del cursor del campo enfocado al empezar una lectura.</summary>
public sealed record TextBoxSnapshot(string Text, int CaretIndex);

/// <summary>
/// Lectura completa del teclado (021). <paramref name="RawText"/> no incluye el terminador;
/// <paramref name="IsBurst"/> indica que llegó a velocidad de escáner.
/// </summary>
public sealed record ScanReading(string RawText, Terminator Terminator, bool IsBurst, TextBoxSnapshot? FocusedSnapshot);
