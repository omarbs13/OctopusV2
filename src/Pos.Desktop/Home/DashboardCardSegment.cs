namespace Pos.Desktop.Home;

/// <summary>Color de una cifra de tarjeta: neutro, advertencia o peligro (022).</summary>
public enum DashboardSegmentTone
{
    Neutral,
    Warning,
    Danger,
}

/// <summary>
/// Cifra navegable dentro de una tarjeta de indicadores con varias cifras (022, research §11): número
/// grande, etiqueta y color; pulsarla navega a <c>NavigateTo</c> con <c>Argument</c>.
/// </summary>
public sealed record DashboardCardSegment(
    string Label,
    string Value,
    DashboardSegmentTone Tone,
    string NavigateTo,
    object? Argument);
