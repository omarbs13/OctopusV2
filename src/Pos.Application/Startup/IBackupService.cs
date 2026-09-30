namespace Pos.Application.Startup;

/// <summary>Respaldos consistentes de la base local.</summary>
public interface IBackupService
{
    /// <summary>Crea un respaldo y aplica la retención de su tipo. Nunca deja un respaldo a medias.</summary>
    Task<BackupInfo> CreateAsync(BackupKind kind, CancellationToken cancellationToken);

    Task<BackupInfo?> GetLatestAsync(BackupKind kind, CancellationToken cancellationToken);

    /// <summary>Reemplaza la base actual por el contenido del respaldo.</summary>
    Task RestoreAsync(BackupInfo backup, CancellationToken cancellationToken);

    /// <summary>Mueve la base actual (y sus archivos -wal y -shm) a la carpeta de bases dañadas.</summary>
    Task QuarantineCurrentDatabaseAsync(CancellationToken cancellationToken);

    /// <summary>Copia consistente de la base en un archivo temporal; el llamador lo elimina.</summary>
    Task<string> CreateTemporaryCopyAsync(CancellationToken cancellationToken);
}

public enum BackupKind
{
    /// <summary>Al arrancar y al cerrar, si el último tiene más de 24 h. Se conservan 7.</summary>
    Automatic,

    /// <summary>Antes de aplicar migraciones. Se conservan 5.</summary>
    PreMigration,
}

public sealed record BackupInfo(string Path, BackupKind Kind, DateTime CreatedAtUtc);
