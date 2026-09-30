namespace Pos.Application.Startup;

/// <summary>Secuencia de arranque y cierre de la base (contracts/startup.md).</summary>
public interface IDatabaseStartup
{
    Task<StartupResult> RunAsync(CancellationToken cancellationToken);

    /// <summary>Aparta la base dañada y restaura el respaldo indicado. Después hay que volver a ejecutar <see cref="RunAsync"/>.</summary>
    Task RecoverFromBackupAsync(BackupInfo backup, CancellationToken cancellationToken);

    /// <summary>Respaldo automático al cerrar, con límite de tiempo. Nunca lanza.</summary>
    Task BackupOnCloseAsync();
}
