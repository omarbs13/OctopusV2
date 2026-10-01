namespace Pos.Application.Returns;

/// <summary>
/// Configuración de devoluciones por instalación: plazo máximo en días para cancelar o devolver una
/// venta (research §9). Sin archivo o dañado devuelve el valor predeterminado.
/// </summary>
public sealed record ReturnsSettings
{
    public const int DefaultReturnWindowDays = 30;
    public const int MinReturnWindowDays = 1;
    public const int MaxReturnWindowDays = 3650;

    public int ReturnWindowDays { get; init; } = DefaultReturnWindowDays;
}
