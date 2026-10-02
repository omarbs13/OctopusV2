using System.Diagnostics;
using System.Text;

namespace Pos.Desktop.Common.Scanner;

/// <summary>
/// Distingue la ráfaga de un lector en modo teclado de la escritura manual (021, research §6): una
/// lectura es escaneo si tiene al menos <see cref="MinBurstLength"/> caracteres y ningún intervalo entre
/// teclas (incluido el terminador) supera <see cref="MaxGap"/>. Las marcas de tiempo son de
/// <see cref="Stopwatch.GetTimestamp"/>.
/// </summary>
public sealed class ScanBurstDetector
{
    public static readonly TimeSpan MaxGap = TimeSpan.FromMilliseconds(50);

    public const int MinBurstLength = 3;

    /// <summary>Silencio que cierra una lectura sin terminador (pantalla de prueba).</summary>
    public static readonly TimeSpan IdleEnd = TimeSpan.FromMilliseconds(300);

    /// <summary>Espera del atajo <c>*</c> con el campo de captura vacío (research §9).</summary>
    public static readonly TimeSpan StarDeferral = TimeSpan.FromMilliseconds(60);

    private readonly StringBuilder _text = new();
    private long _lastTimestamp;
    private bool _slow;
    private TextBoxSnapshot? _snapshot;

    /// <summary>Indica si hay caracteres de una lectura sin cerrar.</summary>
    public bool HasPending => _text.Length > 0;

    /// <summary>
    /// Agrega texto recibido. La instantánea del campo enfocado se guarda solo en el primer carácter de
    /// la secuencia. Tras <see cref="IdleEnd"/> sin teclas empieza una secuencia nueva: lo escrito antes a
    /// mano sin terminador no convierte en manual la ráfaga siguiente.
    /// </summary>
    public void OnText(string text, long timestamp, TextBoxSnapshot? focused)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_text.Length > 0 && Stopwatch.GetElapsedTime(_lastTimestamp, timestamp) >= IdleEnd)
        {
            Reset();
        }

        if (_text.Length == 0)
        {
            _snapshot = focused;
            _slow = false;
        }
        else if (IsGapTooLong(timestamp))
        {
            _slow = true;
        }

        _text.Append(text);
        _lastTimestamp = timestamp;
    }

    /// <summary>Cierra la lectura con un terminador; nulo si no había caracteres.</summary>
    public ScanReading? OnTerminator(Terminator terminator, long timestamp)
    {
        if (_text.Length == 0)
        {
            return null;
        }

        if (IsGapTooLong(timestamp))
        {
            _slow = true;
        }

        return Complete(terminator);
    }

    /// <summary>Cierra la lectura con <see cref="Terminator.None"/> tras <see cref="IdleEnd"/> sin teclas.</summary>
    public ScanReading? CheckIdle(long timestamp)
    {
        if (_text.Length == 0 || Stopwatch.GetElapsedTime(_lastTimestamp, timestamp) < IdleEnd)
        {
            return null;
        }

        return Complete(Terminator.None);
    }

    public void Reset()
    {
        _text.Clear();
        _snapshot = null;
        _slow = false;
    }

    private bool IsGapTooLong(long timestamp) => Stopwatch.GetElapsedTime(_lastTimestamp, timestamp) > MaxGap;

    private ScanReading Complete(Terminator terminator)
    {
        var text = _text.ToString();
        var reading = new ScanReading(text, terminator, !_slow && text.Length >= MinBurstLength, _snapshot);
        Reset();
        return reading;
    }
}
