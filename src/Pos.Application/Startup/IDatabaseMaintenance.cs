namespace Pos.Application.Startup;

/// <summary>Estado, verificación y migración de la base local.</summary>
public interface IDatabaseMaintenance
{
    bool DatabaseExists();

    /// <summary>Verifica que la base se pueda abrir, esté íntegra y admita escritura.</summary>
    Task<IntegrityState> CheckIntegrityAsync(CancellationToken cancellationToken);

    Task<MigrationState> GetMigrationStateAsync(CancellationToken cancellationToken);

    /// <summary>Crea o actualiza el esquema. Los problemas de acceso se lanzan como <see cref="DatabaseAccessException"/>.</summary>
    Task MigrateAsync(CancellationToken cancellationToken);

    Task EnableWalAsync(CancellationToken cancellationToken);

    long DatabaseSizeBytes();

    long AvailableFreeSpaceBytes();
}

public enum IntegrityState
{
    Ok,
    Corrupted,
    Locked,
    PermissionDenied,
}

public enum MigrationStatus
{
    UpToDate,

    /// <summary>Hay migraciones conocidas que aún no se aplicaron.</summary>
    Pending,

    /// <summary>La base tiene migraciones que esta versión de la aplicación no conoce.</summary>
    Newer,
}

public sealed record MigrationState(MigrationStatus Status, IReadOnlyList<string> Migrations)
{
    public static MigrationState UpToDate { get; } = new(MigrationStatus.UpToDate, []);
}
