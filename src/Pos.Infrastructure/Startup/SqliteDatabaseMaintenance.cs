using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Startup;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Startup;

public sealed class SqliteDatabaseMaintenance : IDatabaseMaintenance
{
    private readonly IDbContextFactory<PosDbContext> _contexts;
    private readonly IAppPaths _paths;

    public SqliteDatabaseMaintenance(IDbContextFactory<PosDbContext> contexts, IAppPaths paths)
    {
        _contexts = contexts;
        _paths = paths;
    }

    public bool DatabaseExists() => File.Exists(_paths.DatabaseFile);

    public Task<IntegrityState> CheckIntegrityAsync(CancellationToken cancellationToken) =>
        Task.Run(CheckIntegrity, cancellationToken);

    public async Task<MigrationState> GetMigrationStateAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var known = context.Database.GetMigrations().ToList();

        var unknown = applied.Except(known, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            return new MigrationState(MigrationStatus.Newer, unknown);
        }

        var pending = known.Except(applied, StringComparer.Ordinal).ToList();
        return pending.Count > 0
            ? new MigrationState(MigrationStatus.Pending, pending)
            : MigrationState.UpToDate;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_paths.DatabaseFile)!);
            await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex) when (SqliteErrors.Translate(ex) is { } translated
            && translated.Problem != DatabaseProblem.Corrupted)
        {
            throw translated;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    public async Task EnableWalAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(SqliteConnectionStrings.For(_paths.DatabaseFile));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteScalarAsync(cancellationToken);
    }

    public long DatabaseSizeBytes()
    {
        long size = 0;
        foreach (var file in new[] { _paths.DatabaseFile, _paths.DatabaseFile + "-wal" })
        {
            if (File.Exists(file))
            {
                size += new FileInfo(file).Length;
            }
        }

        return size;
    }

    public long AvailableFreeSpaceBytes()
    {
        var directory = Path.GetDirectoryName(_paths.DatabaseFile)!;
        try
        {
            return new DriveInfo(directory).AvailableFreeSpace;
        }
        catch (ArgumentException)
        {
            return new DriveInfo(Path.GetPathRoot(directory)!).AvailableFreeSpace;
        }
    }

    private IntegrityState CheckIntegrity()
    {
        try
        {
            if (!CanWriteToDataDirectory() || new FileInfo(_paths.DatabaseFile).IsReadOnly)
            {
                return IntegrityState.PermissionDenied;
            }

            using var connection = new SqliteConnection(
                SqliteConnectionStrings.For(_paths.DatabaseFile, SqliteOpenMode.ReadWrite));
            connection.Open();

            using (var check = connection.CreateCommand())
            {
                check.CommandText = "PRAGMA quick_check;";
                if (!string.Equals(check.ExecuteScalar() as string, "ok", StringComparison.Ordinal))
                {
                    return IntegrityState.Corrupted;
                }
            }

            // Confirma que se puede obtener el bloqueo de escritura (otro programa no retiene la base).
            using var write = connection.CreateCommand();
            write.CommandText = "BEGIN IMMEDIATE; ROLLBACK;";
            write.ExecuteNonQuery();
            return IntegrityState.Ok;
        }
        catch (Exception ex) when (SqliteErrors.Classify(ex) is { } problem)
        {
            return problem switch
            {
                DatabaseProblem.Corrupted => IntegrityState.Corrupted,
                DatabaseProblem.Locked => IntegrityState.Locked,
                _ => IntegrityState.PermissionDenied,
            };
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private bool CanWriteToDataDirectory()
    {
        var probe = Path.Combine(Path.GetDirectoryName(_paths.DatabaseFile)!, $".write-probe-{Guid.NewGuid():N}");
        try
        {
            using (File.Create(probe, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
