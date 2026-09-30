using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Startup;
using Pos.Infrastructure.Startup;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Startup;

public sealed class SqliteBackupServiceTests : IDisposable
{
    private readonly TempDataDirectory _dir = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _dir.Dispose();

    private SqliteBackupService CreateService(TestClock clock) =>
        new(_dir.Paths, clock, NullLogger<SqliteBackupService>.Instance);

    [Fact]
    public async Task Create_GeneraRespaldoConsistenteConNombreConFecha()
    {
        using var db = await TestDb.CreateAsync(_dir);
        await DatabaseTestHelpers.SeedProductsAsync(db, 3);
        var service = CreateService(db.Clock);

        var backup = await service.CreateAsync(BackupKind.Automatic, Ct);

        Assert.Equal(Path.Combine(_dir.Paths.AutoBackupsDirectory, "pos-20260929-153000Z.db"), backup.Path);
        Assert.Equal(db.Clock.UtcNow, backup.CreatedAtUtc);
        Assert.Equal(BackupKind.Automatic, backup.Kind);
        Assert.Equal("ok", DatabaseTestHelpers.QuickCheck(backup.Path));
        Assert.Equal(
            DatabaseTestHelpers.ReadProductRows(_dir.Paths.DatabaseFile),
            DatabaseTestHelpers.ReadProductRows(backup.Path));
        Assert.Empty(Directory.GetFiles(_dir.Paths.AutoBackupsDirectory, "*.tmp"));
    }

    [Fact]
    public async Task Create_EnElMismoSegundo_NoSobrescribe()
    {
        using var db = await TestDb.CreateAsync(_dir);
        var service = CreateService(db.Clock);

        var first = await service.CreateAsync(BackupKind.Automatic, Ct);
        var second = await service.CreateAsync(BackupKind.Automatic, Ct);

        Assert.NotEqual(first.Path, second.Path);
        Assert.True(File.Exists(first.Path));
        Assert.True(File.Exists(second.Path));
    }

    [Theory]
    [InlineData(BackupKind.Automatic, 7)]
    [InlineData(BackupKind.PreMigration, 5)]
    public async Task Create_AplicaRetencionConservandoLosMasRecientes(BackupKind kind, int retained)
    {
        using var db = await TestDb.CreateAsync(_dir);
        var service = CreateService(db.Clock);
        var created = new List<BackupInfo>();

        for (var i = 0; i < retained + 3; i++)
        {
            created.Add(await service.CreateAsync(kind, Ct));
            db.Clock.Advance(TimeSpan.FromDays(1));
        }

        var directory = kind == BackupKind.Automatic
            ? _dir.Paths.AutoBackupsDirectory
            : _dir.Paths.PreMigrationBackupsDirectory;
        var remaining = Directory.GetFiles(directory).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(created.TakeLast(retained).Select(b => b.Path), remaining);
    }

    [Fact]
    public async Task GetLatest_DevuelveElMasRecienteSegunElNombre()
    {
        using var db = await TestDb.CreateAsync(_dir);
        var service = CreateService(db.Clock);
        await service.CreateAsync(BackupKind.Automatic, Ct);
        db.Clock.Advance(TimeSpan.FromHours(30));
        var newest = await service.CreateAsync(BackupKind.Automatic, Ct);

        var latest = await service.GetLatestAsync(BackupKind.Automatic, Ct);

        Assert.Equal(newest, latest);
        Assert.Null(await service.GetLatestAsync(BackupKind.PreMigration, Ct));
    }

    [Fact]
    public async Task Restore_RecuperaLosDatosExactosYEliminaWalYShm()
    {
        using var db = await TestDb.CreateAsync(_dir);
        await DatabaseTestHelpers.SeedProductsAsync(db, 3);
        var expected = DatabaseTestHelpers.ReadProductRows(_dir.Paths.DatabaseFile);
        var service = CreateService(db.Clock);
        var backup = await service.CreateAsync(BackupKind.PreMigration, Ct);
        await DatabaseTestHelpers.SeedProductsAsync(db, 2, prefix: "Extra");

        await service.RestoreAsync(backup, Ct);

        // Se verifica antes de leer: abrir una base WAL vuelve a crear esos archivos.
        Assert.False(File.Exists(_dir.Paths.DatabaseFile + "-wal"));
        Assert.False(File.Exists(_dir.Paths.DatabaseFile + "-shm"));
        Assert.Equal(expected, DatabaseTestHelpers.ReadProductRows(_dir.Paths.DatabaseFile));
    }

    [Fact]
    public async Task Quarantine_MueveLaBaseASuCarpetaConFecha()
    {
        using var db = await TestDb.CreateAsync(_dir);
        var service = CreateService(db.Clock);

        await service.QuarantineCurrentDatabaseAsync(Ct);

        Assert.False(File.Exists(_dir.Paths.DatabaseFile));
        var moved = Path.Combine(_dir.Paths.CorruptDirectory, "20260929-153000Z", "pos.db");
        Assert.True(File.Exists(moved));
    }

    [Fact]
    public async Task CreateTemporaryCopy_GeneraCopiaAbrible()
    {
        using var db = await TestDb.CreateAsync(_dir);
        await DatabaseTestHelpers.SeedProductsAsync(db, 2);
        var service = CreateService(db.Clock);

        var copy = await service.CreateTemporaryCopyAsync(Ct);
        try
        {
            Assert.Equal("ok", DatabaseTestHelpers.QuickCheck(copy));
            Assert.Equal(2, DatabaseTestHelpers.ReadProductRows(copy).Count);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Fact]
    public async Task Create_ConDestinoSinPermisos_NoDejaTemporales()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Los permisos POSIX solo se simulan en Linux.");
        Assert.SkipWhen(Environment.UserName == "root", "root ignora los permisos de archivo.");

        using var db = await TestDb.CreateAsync(_dir);
        var service = CreateService(db.Clock);
        UnixPermissions.MakeReadOnly(_dir.Paths.AutoBackupsDirectory);
        try
        {
            var ex = await Assert.ThrowsAsync<DatabaseAccessException>(() => service.CreateAsync(BackupKind.Automatic, Ct));
            Assert.Equal(DatabaseProblem.PermissionDenied, ex.Problem);
        }
        finally
        {
            UnixPermissions.MakeWritable(_dir.Paths.AutoBackupsDirectory);
        }

        Assert.Empty(Directory.GetFiles(_dir.Paths.AutoBackupsDirectory));
    }
}
