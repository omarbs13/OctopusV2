namespace Pos.Application.Printing;

public enum DeviceFailure
{
    NotConfigured,
    Unavailable,
    IoError,
}

/// <summary>Resultado de imprimir; nunca lleva excepciones hacia la interfaz.</summary>
/// <param name="Succeeded">Indica si se imprimió o guardó.</param>
/// <param name="Destination">Impresora o ruta del archivo de la impresora virtual.</param>
/// <param name="Failure">Motivo de la falla; nulo si tuvo éxito.</param>
public sealed record PrintOutcome(bool Succeeded, string? Destination, DeviceFailure? Failure)
{
    public static PrintOutcome Ok(string destination) => new(true, destination, null);

    public static PrintOutcome Fail(DeviceFailure failure, string? destination = null) => new(false, destination, failure);
}

/// <summary>Resultado de abrir el cajón; con impresora virtual el éxito es simulado.</summary>
public sealed record DrawerOutcome(bool Succeeded, string? Destination, DeviceFailure? Failure, bool Simulated = false)
{
    public static DrawerOutcome Ok(string destination, bool simulated = false) => new(true, destination, null, simulated);

    public static DrawerOutcome Fail(DeviceFailure failure, string? destination = null) => new(false, destination, failure);
}
