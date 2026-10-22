using Microsoft.Data.Sqlite;
using Pos.Infrastructure.Startup;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (026): la migración <c>TileSupplierFields</c> + el paso <c>ProductIdentityBackfill</c> conservan
/// productos, ventas, imágenes y existencias de la base de la versión anterior, mueven el precio de las cajas a
/// X CAJA y marcan lo que necesita revisión.
/// </summary>
public sealed class TileSupplierFieldsMigrationTests
{
    private static readonly string[] CountedTables = ["Products", "Sales", "SaleLines", "ProductImages", "ProductStocks", "InventoryMovements"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BaseV0170_ConservaConteosTotalesYLineasDeVenta()
    {
        using var dir = PrepareSample(out var longName);
        var before = Snapshot(dir.Paths.DatabaseFile);

        await MigrateAsync(dir);

        var after = Snapshot(dir.Paths.DatabaseFile);
        Assert.Equal(before.Counts, after.Counts);
        Assert.Equal(before.SaleTotal, after.SaleTotal);
        Assert.Equal(before.SaleLines, after.SaleLines);
        Assert.Equal(before.ReturnsTotal, after.ReturnsTotal);
        Assert.Equal("ok", DatabaseTestHelpers.QuickCheck(dir.Paths.DatabaseFile));
        Assert.NotEmpty(longName);
    }

    [Fact]
    public async Task BaseV0170_CajasPasanAXCajaYElRestoConservaElPrecioPorPieza()
    {
        using var dir = PrepareSample(out _);

        await MigrateAsync(dir);

        using var connection = Open(dir.Paths.DatabaseFile);
        // El producto de caja conserva su precio anterior como X CAJA y no tiene precio por pieza.
        Assert.Equal(
            (null, 3000L),
            ReadPrices(connection, "MUE-003"));
        // Un producto de pieza conserva su precio como precio por pieza.
        Assert.Equal((1000L, null), ReadPrices(connection, "MUE-001"));
        Assert.Equal(
            0L,
            Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE UnitCode <> 'XBX' AND PricePerBoxCents IS NOT NULL"));
        Assert.Equal(
            Scalar<long>(connection, "SELECT COUNT(*) FROM Products"),
            Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE Model = Name"));
    }

    [Fact]
    public async Task BaseV0170_NombreDe200CaracteresSeConservaCompletoYQuedaParaRevision()
    {
        using var dir = PrepareSample(out var longName);

        await MigrateAsync(dir);

        using var connection = Open(dir.Paths.DatabaseFile);
        Assert.Equal(200, longName.Length);
        Assert.Equal(longName, Scalar<string>(connection, "SELECT Model FROM Products WHERE Sku = 'MUE-010'"));
        Assert.Equal(1L, Scalar<long>(connection, "SELECT NeedsReview FROM Products WHERE Sku = 'MUE-010'"));
    }

    [Fact]
    public async Task BaseV0170_DuplicadosConservanAmbosYUnoQuedaMarcado()
    {
        using var dir = PrepareSample(out _);

        await MigrateAsync(dir);

        using var connection = Open(dir.Paths.DatabaseFile);
        Assert.Equal(2L, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE Name = 'Repetido'"));
        Assert.Equal(1L, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE Name = 'Repetido' AND IdentityKey IS NOT NULL AND NeedsReview = 0"));
        Assert.Equal(1L, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE Name = 'Repetido' AND IdentityKey IS NULL AND NeedsReview = 1"));
    }

    [Fact]
    public async Task BaseV0170_ResumenSeCreaUnaVezYReiniciarNoLoDuplica()
    {
        using var dir = PrepareSample(out _);

        await MigrateAsync(dir);
        await MigrateAsync(dir);

        using var connection = Open(dir.Paths.DatabaseFile);
        Assert.Equal(1L, Scalar<long>(connection, "SELECT COUNT(*) FROM ProductMigrationSummaries"));
        Assert.Equal(0L, Scalar<long>(connection, "SELECT Shown FROM ProductMigrationSummaries"));
        Assert.Equal(1L, Scalar<long>(connection, "SELECT DuplicateCount FROM ProductMigrationSummaries"));
        Assert.Equal(0L, Scalar<long>(connection, "SELECT WithoutModelCount FROM ProductMigrationSummaries"));
        Assert.Equal(1L, Scalar<long>(connection, "SELECT WithoutPackagingCount FROM ProductMigrationSummaries"));
    }

    [Fact]
    public async Task RestaurarElMismoRespaldoDosVeces_DaElMismoResultado()
    {
        using var first = PrepareSample(out _);
        using var second = PrepareSample(out _);

        await MigrateAsync(first);
        await MigrateAsync(second);

        Assert.Equal(KeyRows(first.Paths.DatabaseFile), KeyRows(second.Paths.DatabaseFile));
    }

    /// <summary>Copia v0.17.0, y prepara casos que la base de ejemplo no trae: caja, nombre largo y duplicado.</summary>
    private static TempDataDirectory PrepareSample(out string longName)
    {
        var dir = new TempDataDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "SampleDatabases", "v0.17.0.db"), dir.Paths.DatabaseFile);
        longName = new string('N', 200);
        using var connection = Open(dir.Paths.DatabaseFile);
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE Products SET UnitCode = 'XBX' WHERE Sku = 'MUE-003';
            UPDATE Products SET Name = '{longName}', NameSearch = '{longName.ToLowerInvariant()}' WHERE Sku = 'MUE-010';
            UPDATE Products SET Name = 'Repetido', NameSearch = 'repetido' WHERE Sku IN ('MUE-011', 'MUE-012');
            """;
        command.ExecuteNonQuery();
        return dir;
    }

    private static async Task MigrateAsync(TempDataDirectory dir)
    {
        using var db = TestDb.CreateUnmigrated(dir);
        await new SqliteDatabaseMaintenance(db, dir.Paths).MigrateAsync(Ct);
    }

    private static (long? Piece, long? Box) ReadPrices(SqliteConnection connection, string sku)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT PriceCents, PricePerBoxCents FROM Products WHERE Sku = '{sku}'";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.IsDBNull(0) ? null : reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetInt64(1));
    }

    private static (Dictionary<string, long> Counts, long SaleTotal, List<string> SaleLines, long ReturnsTotal) Snapshot(string file)
    {
        using var connection = Open(file);
        var counts = CountedTables.ToDictionary(t => t, t => Scalar<long>(connection, $"SELECT COUNT(*) FROM {t}"));
        var lines = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM SaleLines ORDER BY Id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            lines.Add(string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "∅" : reader.GetValue(i).ToString())));
        }

        return (counts, Scalar<long>(connection, "SELECT COALESCE(SUM(TotalCents), 0) FROM Sales"), lines, Scalar<long>(connection, "SELECT COALESCE(SUM(ReturnedCents), 0) FROM Sales"));
    }

    private static List<string> KeyRows(string file)
    {
        using var connection = Open(file);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Sku, ModelKey, IFNULL(IdentityKey, '∅'), NeedsReview FROM Products ORDER BY Sku";
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add($"{reader.GetString(0)}|{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetInt64(3)}");
        }

        return rows;
    }

    private static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static SqliteConnection Open(string file)
    {
        var connection = new SqliteConnection($"Data Source={file};Pooling=False");
        connection.Open();
        return connection;
    }
}
