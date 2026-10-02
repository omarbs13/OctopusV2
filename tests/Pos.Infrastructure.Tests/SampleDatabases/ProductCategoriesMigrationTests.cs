using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (016): la migración <c>ProductCategories</c> crea la tabla <c>Categories</c> y agrega
/// <c>Products.CategoryId</c> con <c>ADD COLUMN</c>, sin clave foránea ni reconstrucciones (research §2, §16).
/// </summary>
public sealed class ProductCategoriesMigrationTests
{
    [Fact]
    public async Task ProductCategories_SoloCreaLaTablaYAgregaLaColumna()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var categories = migrations.Single(m => m.EndsWith("_ProductCategories", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(categories) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, categories);

        Assert.Contains("CREATE TABLE \"Categories\"", script, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE \"Products\" ADD \"CategoryId\" TEXT NULL", script, StringComparison.Ordinal);
        Assert.Equal(1, script.Split('\n').Count(l => l.StartsWith("ALTER TABLE", StringComparison.Ordinal)));
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Categories_NameKey\"", script, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX \"IX_Products_CategoryId\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("FOREIGN KEY", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductosAnterioresA0110_ConservanSusDatosYQuedanSinCategoria()
    {
        using var dir = new TempDataDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "SampleDatabases", "v0.10.0.db"), dir.Paths.DatabaseFile);
        var before = ReadProducts(dir.Paths.DatabaseFile);
        using (var db = TestDb.CreateUnmigrated(dir))
        await using (var context = db.CreateDbContext())
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(before);
        Assert.Equal(before, ReadProducts(dir.Paths.DatabaseFile));
        using var connection = Open(dir.Paths.DatabaseFile);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Products WHERE CategoryId IS NOT NULL";
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
        command.CommandText = "SELECT COUNT(*) FROM Categories";
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
    }

    private static List<string> ReadProducts(string file)
    {
        using var connection = Open(file);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Sku, PriceCents, IsActive, Version, DeletedAt FROM Products ORDER BY Id";
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add(string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "∅" : reader.GetValue(i).ToString())));
        }

        return rows;
    }

    private static SqliteConnection Open(string file)
    {
        var connection = new SqliteConnection($"Data Source={file};Pooling=False");
        connection.Open();
        return connection;
    }
}
