using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>
/// Base SQLite real en un archivo temporal, con las migraciones aplicadas.
/// Nunca se usa el proveedor InMemory de EF Core (constitución, Principio VI).
/// </summary>
public sealed class TestDb : IDisposable, IDbContextFactory<PosDbContext>
{
    private readonly DbContextOptions<PosDbContext> _options;

    private TestDb(TempDataDirectory directory, bool ownsDirectory)
    {
        Directory = directory;
        OwnsDirectory = ownsDirectory;
        Clock = new TestClock();
        User = new TestCurrentUser();
        _options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(SqliteConnectionStrings.For(directory.Paths.DatabaseFile))
            .AddInterceptors(new AuditingInterceptor(Clock, User))
            .Options;
    }

    public TempDataDirectory Directory { get; }

    public TestClock Clock { get; }

    public TestCurrentUser User { get; }

    private bool OwnsDirectory { get; }

    public static async Task<TestDb> CreateAsync(TempDataDirectory? directory = null)
    {
        var db = new TestDb(directory ?? new TempDataDirectory(), ownsDirectory: directory is null);
        await using var context = db.CreateDbContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return db;
    }

    /// <summary>Base sin crear: solo prepara la fábrica de contextos.</summary>
    public static TestDb CreateUnmigrated(TempDataDirectory directory) => new(directory, ownsDirectory: false);

    public PosDbContext CreateDbContext() => new(_options);

    public void Dispose()
    {
        if (OwnsDirectory)
        {
            Directory.Dispose();
        }
    }
}
