namespace Pos.Application.Printing;

public interface ICashDrawer
{
    /// <summary>Envía el pulso de apertura; no lanza por fallas del dispositivo.</summary>
    Task<DrawerOutcome> OpenAsync(PrintingSettings settings, CancellationToken cancellationToken);
}
