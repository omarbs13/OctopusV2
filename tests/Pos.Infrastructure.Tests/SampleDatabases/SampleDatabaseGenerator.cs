using System.Reflection;
using Microsoft.Data.Sqlite;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Genera la base de ejemplo de la versión actual (constitución, Principio IV). Solo se ejecuta con
/// POS_GENERATE_SAMPLE_DB=1; el archivo resultante se versiona y nunca se modifica después.
/// Ver docs/migraciones.md.
/// </summary>
public sealed class SampleDatabaseGenerator
{
    public const string EnabledVariable = "POS_GENERATE_SAMPLE_DB";

    [Fact]
    public async Task GenerarBaseDeEjemploDeLaVersionActual()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable(EnabledVariable) == "1",
            $"Solo se ejecuta con {EnabledVariable}=1.");

        var version = typeof(PosDbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var target = Path.Combine(FindSampleDirectory(), $"v{version}.db");
        Assert.False(File.Exists(target), $"La base de ejemplo {target} ya existe y no debe modificarse.");

        using var db = await TestDb.CreateAsync();
        db.Clock.UtcNow = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        await using (var context = db.CreateDbContext())
        {
            context.Products.AddRange(SampleData.Products(db.Clock.UtcNow));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Desde 0.2.0: un producto con imagen y uno con precio 0, que la fundación permitía y que las
        // versiones nuevas deben conservar (003, clarificación 2).
        var withImage = await FindIdAsync(db, SampleData.ImageSku);
        await DatabaseTestHelpers.SetImageAsync(db, withImage, SampleData.ImageSeed);
        DatabaseTestHelpers.Execute(
            db.Directory.Paths.DatabaseFile,
            $"UPDATE Products SET PriceCents = 0 WHERE Sku = '{SampleData.ZeroPriceSku}';");

        // Un solo archivo autocontenido: sin WAL pendiente. Primero se liberan las conexiones del pool.
        SqliteConnection.ClearAllPools();
        DatabaseTestHelpers.Execute(db.Directory.Paths.DatabaseFile, "PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode=DELETE; VACUUM;");
        File.Copy(db.Directory.Paths.DatabaseFile, target);
    }

    private static async Task<Guid> FindIdAsync(TestDb db, string sku)
    {
        await using var context = db.CreateDbContext();
        return context.Products.Single(p => p.Sku == sku).Id;
    }

    private static string FindSampleDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pos.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "tests", "Pos.Infrastructure.Tests", "SampleDatabases");
    }
}

/// <summary>
/// Datos de muestra de cada base de ejemplo: 20 productos que cubren activos, inactivos, uno
/// borrado, uno con acentos y caracteres especiales, y productos sin código de barras.
/// </summary>
public static class SampleData
{
    public const int ProductCount = 20;
    public const string DeletedSku = "BORR-001";
    public const string AccentedName = "Jalapeño «Extra» en lata 100% & más";
    public const string AccentedSku = "JAL-010";
    public const long AccentedPriceCents = 123450;

    /// <summary>Desde 0.2.0: producto vendido por kilo.</summary>
    public const string KilogramSku = "MUE-002";

    /// <summary>Desde 0.2.0: producto con imagen (bytes arbitrarios <see cref="ImageSeed"/>).</summary>
    public const string ImageSku = "MUE-003";
    public const byte ImageSeed = 42;

    /// <summary>Desde 0.2.0: producto con precio 0, permitido antes de 003.</summary>
    public const string ZeroPriceSku = "MUE-004";

    public static IEnumerable<Product> Products(DateTime utcNow)
    {
        for (var i = 1; i <= 16; i++)
        {
            var sku = $"MUE-{i:000}";
            var unit = sku == KilogramSku ? "KGM" : "H87";
            yield return Product.Create($"Producto de muestra {i:00}", sku, $"7500000000{i:000}", Money.FromCents(i * 1000), unit);
        }

        yield return Product.Create(AccentedName, AccentedSku, null, Money.FromCents(AccentedPriceCents), "H87");
        yield return Product.Create("Pan sin código", "PAN-001", null, Money.FromCents(5200), "H87");

        var inactive = Product.Create("Refresco descontinuado", "REF-001", "7501055300075", Money.FromCents(1800), "H87");
        inactive.Update(inactive.Name, inactive.Sku, inactive.Barcode, inactive.Price, "H87", isActive: false);
        yield return inactive;

        var deleted = Product.Create("Producto borrado", DeletedSku, "7501234567890", Money.FromCents(100), "H87");
        deleted.Delete(utcNow);
        yield return deleted;
    }
}
