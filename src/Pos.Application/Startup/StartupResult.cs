namespace Pos.Application.Startup;

/// <summary>Resultado de la secuencia de arranque de la base.</summary>
public abstract record StartupResult
{
    private StartupResult()
    {
    }

    public sealed record Ready : StartupResult;

    public sealed record NewerDatabase : StartupResult;

    public sealed record MigrationFailed : StartupResult;

    public sealed record InsufficientSpace : StartupResult;

    public sealed record Inaccessible(DatabaseProblem Reason) : StartupResult;

    /// <summary>La base está dañada; incluye el respaldo automático más reciente, si existe.</summary>
    public sealed record Corrupted(BackupInfo? LatestBackup) : StartupResult;
}
