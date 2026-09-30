using System.IO.Compression;
using System.Text.Json;
using Pos.Application.Abstractions;
using Pos.Application.Diagnostics;
using Pos.Application.Startup;

namespace Pos.Infrastructure.Diagnostics;

/// <summary>
/// Paquete de diagnóstico (research §18). Se arma en una carpeta temporal y solo al final se
/// copia al destino con un nombre temporal y se renombra, así nunca queda un zip incompleto.
/// </summary>
public sealed class ZipDiagnosticsExporter : IDiagnosticsExporter
{
    public static readonly TimeSpan LogWindow = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IAppPaths _paths;
    private readonly IAppInfo _appInfo;
    private readonly IBackupService _backups;
    private readonly IClock _clock;
    private readonly string _tempDirectory;

    public ZipDiagnosticsExporter(
        IAppPaths paths,
        IAppInfo appInfo,
        IBackupService backups,
        IClock clock,
        string? tempDirectory = null)
    {
        _paths = paths;
        _appInfo = appInfo;
        _backups = backups;
        _clock = clock;
        _tempDirectory = tempDirectory ?? Path.GetTempPath();
    }

    public async Task ExportAsync(string destinationFile, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFile);

        var workZip = Path.Combine(_tempDirectory, $"pos-diag-{Guid.NewGuid():N}.zip");
        var destinationTemp = destinationFile + ".tmp";
        string? databaseCopy = null;
        try
        {
            databaseCopy = await _backups.CreateTemporaryCopyAsync(cancellationToken);
            await Task.Run(() => BuildZip(workZip, databaseCopy), cancellationToken);

            File.Copy(workZip, destinationTemp, overwrite: true);
            File.Move(destinationTemp, destinationFile, overwrite: true);
        }
        catch
        {
            TryDelete(destinationTemp);
            throw;
        }
        finally
        {
            TryDelete(workZip);
            if (databaseCopy is not null)
            {
                TryDelete(databaseCopy);
            }
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Un temporal que no se pudo borrar no invalida la exportación.
        }
    }

    private void BuildZip(string zipFile, string databaseCopy)
    {
        using var zip = ZipFile.Open(zipFile, ZipArchiveMode.Create);

        var info = new
        {
            appVersion = _appInfo.Version,
            os = _appInfo.OperatingSystem,
            dataDirectory = _paths.DataDirectory,
            exportedAtUtc = _clock.UtcNow,
        };
        using (var entry = zip.CreateEntry("info.json").Open())
        {
            JsonSerializer.Serialize(entry, info, JsonOptions);
        }

        var since = _clock.UtcNow - LogWindow;
        if (Directory.Exists(_paths.LogsDirectory))
        {
            foreach (var log in Directory.EnumerateFiles(_paths.LogsDirectory, "*.log"))
            {
                if (File.GetLastWriteTimeUtc(log) < since)
                {
                    continue;
                }

                // Serilog mantiene abierto el log del día; se lee sin bloquearlo.
                using var source = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var target = zip.CreateEntry($"logs/{Path.GetFileName(log)}").Open();
                source.CopyTo(target);
            }
        }

        zip.CreateEntryFromFile(databaseCopy, "pos.db");
    }
}
