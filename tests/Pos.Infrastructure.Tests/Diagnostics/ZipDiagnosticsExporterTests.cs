using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Infrastructure.Diagnostics;
using Pos.Infrastructure.Startup;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Diagnostics;

/// <summary>SC-009: el archivo de diagnóstico contiene los logs y un respaldo que se abre.</summary>
public sealed class ZipDiagnosticsExporterTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private string _work = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        await DatabaseTestHelpers.SeedProductsAsync(_db, 3);
        _work = Path.Combine(_db.Directory.Root, "work");
        Directory.CreateDirectory(Path.Combine(_work, "tmp"));
        Directory.CreateDirectory(Path.Combine(_work, "destino"));
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private ZipDiagnosticsExporter CreateExporter()
    {
        var paths = _db.Directory.Paths;
        var backups = new SqliteBackupService(paths, _db.Clock, NullLogger<SqliteBackupService>.Instance);
        return new ZipDiagnosticsExporter(paths, new FixedAppInfo(), backups, _db.Clock, Path.Combine(_work, "tmp"));
    }

    private string WriteLog(string name, TimeSpan age)
    {
        var file = Path.Combine(_db.Directory.Paths.LogsDirectory, name);
        File.WriteAllText(file, $"{{\"@mt\":\"{name}\"}}\n");
        File.SetLastWriteTimeUtc(file, _db.Clock.UtcNow - age);
        return file;
    }

    [Fact]
    public async Task Exportar_GeneraZipConInfoLogsRecientesYBaseAbrible()
    {
        WriteLog("pos-20260929.log", TimeSpan.FromHours(1));
        WriteLog("pos-20260925.log", TimeSpan.FromDays(4));
        WriteLog("pos-20260910.log", TimeSpan.FromDays(19));
        var destination = Path.Combine(_work, "destino", "diag.zip");

        await CreateExporter().ExportAsync(destination, Ct);

        using var zip = ZipFile.OpenRead(destination);
        var names = zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["info.json", "logs/pos-20260925.log", "logs/pos-20260929.log", "pos.db"], names);

        using (var info = JsonDocument.Parse(zip.GetEntry("info.json")!.Open()))
        {
            var root = info.RootElement;
            Assert.Equal("9.9.9", root.GetProperty("appVersion").GetString());
            Assert.Equal("SO de prueba", root.GetProperty("os").GetString());
            Assert.Equal(_db.Directory.Paths.DataDirectory, root.GetProperty("dataDirectory").GetString());
            Assert.Equal(_db.Clock.UtcNow, root.GetProperty("exportedAtUtc").GetDateTime().ToUniversalTime());
        }

        var extracted = Path.Combine(_work, "extraido.db");
        zip.GetEntry("pos.db")!.ExtractToFile(extracted);
        Assert.Equal("ok", DatabaseTestHelpers.QuickCheck(extracted));
        Assert.Equal(3, DatabaseTestHelpers.ReadProductRows(extracted).Count);
        Assert.Empty(Directory.GetFiles(Path.Combine(_work, "tmp")));
    }

    [Fact]
    public async Task LogAbiertoParaEscritura_SeIncluyeIgual()
    {
        var log = WriteLog("pos-20260929.log", TimeSpan.Zero);
        await using var writer = new FileStream(log, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        await writer.WriteAsync("{\"@mt\":\"escribiendo\"}\n"u8.ToArray(), Ct);
        await writer.FlushAsync(Ct);
        var destination = Path.Combine(_work, "destino", "diag.zip");

        await CreateExporter().ExportAsync(destination, Ct);

        using var zip = ZipFile.OpenRead(destination);
        using var reader = new StreamReader(zip.GetEntry("logs/pos-20260929.log")!.Open());
        Assert.Contains("escribiendo", await reader.ReadToEndAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DestinoSinPermisos_FallaSinDejarArchivosIncompletos()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Los permisos POSIX solo se simulan en Linux.");
        Assert.SkipWhen(Environment.UserName == "root", "root ignora los permisos de archivo.");

        var destinationDir = Path.Combine(_work, "destino");
        UnixPermissions.MakeReadOnly(destinationDir);
        try
        {
            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(
                () => CreateExporter().ExportAsync(Path.Combine(destinationDir, "diag.zip"), Ct));
        }
        finally
        {
            UnixPermissions.MakeWritable(destinationDir);
        }

        Assert.Empty(Directory.GetFiles(destinationDir));
        Assert.Empty(Directory.GetFiles(Path.Combine(_work, "tmp")));
    }

    private sealed class FixedAppInfo : IAppInfo
    {
        public string Version => "9.9.9";

        public string OperatingSystem => "SO de prueba";
    }
}
