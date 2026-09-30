using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Startup;

namespace Pos.Infrastructure.Startup;

/// <summary>
/// Respaldos con la API de backup en línea de SQLite: son consistentes aunque la base esté en
/// modo WAL o tenga conexiones abiertas. Se escriben como .tmp y se renombran al terminar.
/// </summary>
public sealed partial class SqliteBackupService : IBackupService
{
    public const int AutomaticRetention = 7;
    public const int PreMigrationRetention = 5;

    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    private readonly IAppPaths _paths;
    private readonly IClock _clock;
    private readonly ILogger<SqliteBackupService> _logger;

    public SqliteBackupService(IAppPaths paths, IClock clock, ILogger<SqliteBackupService> logger)
    {
        _paths = paths;
        _clock = clock;
        _logger = logger;
    }

    public Task<BackupInfo> CreateAsync(BackupKind kind, CancellationToken cancellationToken) =>
        Task.Run(
            () =>
            {
                var directory = DirectoryFor(kind);
                var createdAt = TruncateToSeconds(_clock.UtcNow);
                var target = UniqueBackupPath(directory, createdAt);

                Translate(() =>
                {
                    Directory.CreateDirectory(directory);
                    CopyDatabase(_paths.DatabaseFile, target);
                });

                Prune(kind);
                LogBackupCreated(kind, target);
                return new BackupInfo(target, kind, createdAt);
            },
            cancellationToken);

    public Task<BackupInfo?> GetLatestAsync(BackupKind kind, CancellationToken cancellationToken) =>
        Task.FromResult(List(kind).MaxBy(b => (b.CreatedAtUtc, b.Path)));

    public Task RestoreAsync(BackupInfo backup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backup);
        return Task.Run(
            () => Translate(() =>
            {
                SqliteConnection.ClearAllPools();
                Directory.CreateDirectory(Path.GetDirectoryName(_paths.DatabaseFile)!);

                using (var source = Open(backup.Path, SqliteOpenMode.ReadOnly))
                using (var destination = Open(_paths.DatabaseFile, SqliteOpenMode.ReadWriteCreate))
                {
                    source.BackupDatabase(destination);
                }

                SqliteConnection.ClearAllPools();
                DeleteIfExists(_paths.DatabaseFile + "-wal");
                DeleteIfExists(_paths.DatabaseFile + "-shm");
                LogRestored(backup.Path);
            }),
            cancellationToken);
    }

    public Task QuarantineCurrentDatabaseAsync(CancellationToken cancellationToken) =>
        Task.Run(
            () => Translate(() =>
            {
                SqliteConnection.ClearAllPools();
                var stamp = TruncateToSeconds(_clock.UtcNow).ToString(TimestampFormat, CultureInfo.InvariantCulture) + "Z";
                var target = Path.Combine(_paths.CorruptDirectory, stamp);
                for (var i = 1; Directory.Exists(target); i++)
                {
                    target = Path.Combine(_paths.CorruptDirectory, $"{stamp}-{i}");
                }

                Directory.CreateDirectory(target);
                foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
                {
                    var file = _paths.DatabaseFile + suffix;
                    if (File.Exists(file))
                    {
                        File.Move(file, Path.Combine(target, Path.GetFileName(file)));
                    }
                }

                LogQuarantined(target);
            }),
            cancellationToken);

    public Task<string> CreateTemporaryCopyAsync(CancellationToken cancellationToken) =>
        Task.Run(
            () =>
            {
                var target = Path.Combine(Path.GetTempPath(), $"pos-copia-{Guid.NewGuid():N}.db");
                Translate(() => CopyDatabase(_paths.DatabaseFile, target));
                return target;
            },
            cancellationToken);

    private static void CopyDatabase(string sourceFile, string targetFile)
    {
        var temporary = targetFile + ".tmp";
        try
        {
            using (var source = Open(sourceFile, SqliteOpenMode.ReadOnly))
            using (var destination = Open(temporary, SqliteOpenMode.ReadWriteCreate))
            {
                source.BackupDatabase(destination);
            }

            File.Move(temporary, targetFile);
        }
        catch
        {
            DeleteIfExists(temporary);
            throw;
        }
    }

    private static SqliteConnection Open(string file, SqliteOpenMode mode)
    {
        // Sin pool: el archivo debe quedar libre al terminar (para renombrarlo o reemplazarlo).
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = file,
            Mode = mode,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void Translate(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (SqliteErrors.Translate(ex) is { } translated)
        {
            throw translated;
        }
    }

    private static DateTime TruncateToSeconds(DateTime utc) =>
        new(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

    private static string UniqueBackupPath(string directory, DateTime createdAt)
    {
        var stamp = createdAt.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"pos-{stamp}Z.db");
        for (var i = 1; File.Exists(path); i++)
        {
            path = Path.Combine(directory, $"pos-{stamp}Z-{i}.db");
        }

        return path;
    }

    private static void DeleteIfExists(string file)
    {
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    private string DirectoryFor(BackupKind kind) => kind switch
    {
        BackupKind.Automatic => _paths.AutoBackupsDirectory,
        BackupKind.PreMigration => _paths.PreMigrationBackupsDirectory,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private List<BackupInfo> List(BackupKind kind)
    {
        var directory = DirectoryFor(kind);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var backups = new List<BackupInfo>();
        foreach (var file in Directory.EnumerateFiles(directory, "pos-*.db"))
        {
            var match = BackupFileName().Match(Path.GetFileName(file));
            if (match.Success && DateTime.TryParseExact(
                    match.Groups["stamp"].Value,
                    TimestampFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var createdAt))
            {
                backups.Add(new BackupInfo(file, kind, createdAt));
            }
        }

        return backups;
    }

    private void Prune(BackupKind kind)
    {
        var retention = kind == BackupKind.Automatic ? AutomaticRetention : PreMigrationRetention;
        var obsolete = List(kind)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ThenByDescending(b => b.Path, StringComparer.Ordinal)
            .Skip(retention);

        foreach (var backup in obsolete)
        {
            try
            {
                File.Delete(backup.Path);
            }
            catch (IOException ex)
            {
                LogPruneFailed(ex, backup.Path);
            }
        }
    }

    [GeneratedRegex(@"^pos-(?<stamp>[0-9]{8}-[0-9]{6})Z(-[0-9]+)?\.db$", RegexOptions.CultureInvariant)]
    private static partial Regex BackupFileName();

    [LoggerMessage(Level = LogLevel.Information, Message = "Respaldo {Kind} creado en {Path}")]
    private partial void LogBackupCreated(BackupKind kind, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Base restaurada desde {Path}")]
    private partial void LogRestored(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Base dañada apartada en {Path}")]
    private partial void LogQuarantined(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo eliminar el respaldo antiguo {Path}")]
    private partial void LogPruneFailed(Exception exception, string path);
}
