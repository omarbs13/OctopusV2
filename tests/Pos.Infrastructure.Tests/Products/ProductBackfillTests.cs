using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Pos.Infrastructure.Startup;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>Paso <c>ProductIdentityBackfill</c> (026): claves, duplicados y rendimiento con 10,000 productos.</summary>
public sealed class ProductBackfillTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DosProductosConElMismoNombre_QuedanAmbosYUnoMarcado()
    {
        Insert(("A-1", "Sunwood Miel 18X60", "H87"), ("A-2", "SUNWOOD MIEL 18X60", "H87"), ("A-3", "Otro", "H87"));

        var result = await RunAsync();

        Assert.NotNull(result);
        Assert.Equal(1, result.Duplicates);
        Assert.Equal(3, Count("SELECT COUNT(*) FROM Products"));
        Assert.Equal(1, Count("SELECT COUNT(*) FROM Products WHERE NeedsReview = 1 AND IdentityKey IS NULL"));
        Assert.Equal(2, Count("SELECT COUNT(*) FROM Products WHERE NeedsReview = 0 AND IdentityKey IS NOT NULL"));
    }

    [Fact]
    public async Task SegundaEjecucion_NoCambiaNadaNiDuplicaElResumen()
    {
        Insert(("A-1", "Uno", "H87"), ("A-2", "uno", "XBX"));

        await RunAsync();
        var second = await RunAsync();

        Assert.Null(second);
        Assert.Equal(1, Count("SELECT COUNT(*) FROM ProductMigrationSummaries"));
        Assert.Equal(1, Count("SELECT WithoutPackagingCount FROM ProductMigrationSummaries"));
    }

    [Fact]
    public async Task SinProductosPorProcesar_NoCreaResumen()
    {
        var result = await RunAsync();

        Assert.Null(result);
        Assert.Equal(0, Count("SELECT COUNT(*) FROM ProductMigrationSummaries"));
    }

    [Fact]
    public async Task DiezMilProductos_SeMigranEnMenosDe10Segundos()
    {
        Insert(Enumerable.Range(1, 10_000).Select(i => ($"P-{i:00000}", $"Producto número {i:00000} Ñandú", i % 7 == 0 ? "XBX" : "H87")).ToArray());

        var watch = Stopwatch.StartNew();
        var result = await RunAsync();
        watch.Stop();

        Assert.NotNull(result);
        Assert.Equal(10_000, result.Migrated);
        Assert.Equal(0, Count("SELECT COUNT(*) FROM Products WHERE ModelKey = ''"));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Tardó {watch.Elapsed.TotalSeconds:0.0} s.");
    }

    private async Task<ProductBackfillResult?> RunAsync()
    {
        await using var context = _db.CreateDbContext();
        return await ProductIdentityBackfill.RunAsync(context, DateTime.UtcNow, Ct);
    }

    /// <summary>Productos como los dejaría la migración de datos: Model = Name y claves sin calcular.</summary>
    private void Insert(params (string Sku, string Name, string Unit)[] products)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Products (Id, Name, NameSearch, Sku, PriceCents, UnitCode, IsActive, TracksInventory, IsCritical,
                CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, Version, Model, ModelKey, NeedsReview)
            VALUES ($id, $name, $search, $sku, 100, $unit, 1, 0, 0, $created, $user, $created, $user, 1, $name, '', 0)
            """;
        var id = command.Parameters.Add("$id", SqliteType.Text);
        var name = command.Parameters.Add("$name", SqliteType.Text);
        var search = command.Parameters.Add("$search", SqliteType.Text);
        var sku = command.Parameters.Add("$sku", SqliteType.Text);
        var unit = command.Parameters.Add("$unit", SqliteType.Text);
        command.Parameters.AddWithValue("$created", "2026-01-01 00:00:00");
        command.Parameters.AddWithValue("$user", Guid.Empty.ToString().ToUpperInvariant());
        var created = 0;
        foreach (var product in products)
        {
            id.Value = Guid.CreateVersion7().ToString().ToUpperInvariant();
            name.Value = product.Name;
            search.Value = product.Name.ToLowerInvariant();
            sku.Value = product.Sku;
            unit.Value = product.Unit;
            command.Parameters["$created"].Value = $"2026-01-01 00:00:{created++ % 60:00}";
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private long Count(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={_db.Directory.Paths.DatabaseFile};Pooling=False");
        connection.Open();
        return connection;
    }
}
