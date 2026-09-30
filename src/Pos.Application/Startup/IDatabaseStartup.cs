namespace Pos.Application.Startup;

/// <summary>Secuencia de arranque y cierre de la base (contracts/startup.md).</summary>
public interface IDatabaseStartup
{
    /// <param name="cancellationToken">Cancelación.</param>
    /// <param name="progress">Recibe cada paso antes de ejecutarlo (pantalla de carga); opcional.</param>
    Task<StartupResult> RunAsync(CancellationToken cancellationToken, IProgress<StartupStep>? progress = null);

    /// <summary>Aparta la base dañada y restaura el respaldo indicado. Después hay que volver a ejecutar <see cref="RunAsync"/>.</summary>
    Task RecoverFromBackupAsync(BackupInfo backup, CancellationToken cancellationToken, IProgress<StartupStep>? progress = null);

    /// <summary>Respaldo automático al cerrar, con límite de tiempo. Nunca lanza.</summary>
    Task BackupOnCloseAsync();
}
