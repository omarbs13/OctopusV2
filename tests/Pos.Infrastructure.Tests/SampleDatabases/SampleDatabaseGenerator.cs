using System.Reflection;
using Microsoft.Data.Sqlite;
using Pos.Application.Sales;
using Pos.Application.Sales.SaveSaleDraft;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Users;
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

        // Desde 0.3.0: un producto que controla inventario, con movimientos de los cuatro tipos.
        await using (var context = db.CreateDbContext())
        {
            var product = context.Products.Single(p => p.Sku == SampleData.InventorySku);
            var unit = UnitOfMeasure.Find(product.UnitCode)!;
            product.Update(product.Name, product.Sku, product.Barcode, product.Price, product.UnitCode, product.IsActive, tracksInventory: true, Quantity.FromThousandths(5000));

            var stock = ProductStock.Start(product.Id);
            context.ProductStocks.Add(stock);
            context.InventoryMovements.AddRange(
                stock.Record(MovementType.Initial, Quantity.FromThousandths(10_500), unit, true, true, null, null),
                stock.Record(MovementType.Receipt, Quantity.FromThousandths(2_000), unit, true, true, null, "F-1234"),
                stock.Record(MovementType.AdjustIn, Quantity.FromThousandths(1_500), unit, true, true, "Conteo físico", null),
                stock.Record(MovementType.AdjustOut, Quantity.FromThousandths(4_000), unit, true, true, "Merma", null));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Desde 0.6.0: un administrador y un cajero (007), que hacen las ventas.
        var (adminId, cashierId) = await SeedUsersAsync(db);

        // Desde 0.4.0: ventas completadas y canceladas, una existencia negativa y un borrador.
        await SeedSalesAsync(db, adminId, cashierId);

        // Un solo archivo autocontenido: sin WAL pendiente. Primero se liberan las conexiones del pool.
        SqliteConnection.ClearAllPools();
        DatabaseTestHelpers.Execute(db.Directory.Paths.DatabaseFile, "PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode=DELETE; VACUUM;");
        File.Copy(db.Directory.Paths.DatabaseFile, target);
    }

    private static async Task<(Guid AdminId, Guid CashierId)> SeedUsersAsync(TestDb db)
    {
        var admin = User.Create("Administrador de muestra", SampleData.AdminUserName, UserRole.Admin, "hash-de-muestra");
        var cashier = User.Create("Cajero de muestra", SampleData.CashierUserName, UserRole.Cashier, "hash-de-muestra");
        await using var context = db.CreateDbContext();
        context.Users.AddRange(admin, cashier);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (admin.Id, cashier.Id);
    }

    private static async Task SeedSalesAsync(TestDb db, Guid adminId, Guid cashierId)
    {
        var ct = TestContext.Current.CancellationToken;
        db.User.UserId = adminId;

        // La pieza con inventario arranca con 3 y se venden 5: queda en -2.
        await using (var context = db.CreateDbContext())
        {
            var piece = context.Products.Single(p => p.Sku == SampleData.NegativeStockSku);
            piece.Update(piece.Name, piece.Sku, piece.Barcode, piece.Price, piece.UnitCode, piece.IsActive, tracksInventory: true, minimumStock: null);
            await context.SaveChangesAsync(ct);
        }

        var negative = await FindProductAsync(db, SampleData.NegativeStockSku);
        var kilogram = await FindProductAsync(db, SampleData.InventorySku);
        var service = await FindProductAsync(db, SampleData.SaleWithoutInventorySku);
        await SalesTestSupport.StockAsync(db, negative, "3");

        db.Clock.UtcNow = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        await SalesTestSupport.SellOkAsync(db, (kilogram, 2000), (service, 1000));
        db.User.UserId = cashierId;
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 12, 30, 0, DateTimeKind.Utc);
        var cancelled = await SalesTestSupport.SellOkAsync(db, (kilogram, 1500));
        Assert.True((await SalesTestSupport.CancelAsync(db, cancelled.SaleId, SampleData.CancellationReason)).IsSuccess);
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 13, 0, 0, DateTimeKind.Utc);
        await SalesTestSupport.SellOkAsync(db, (negative, 5000));

        var draftLine = await FindProductAsync(db, SampleData.ImageSku);
        await using var draftContext = db.CreateDbContext();
        await SalesTestSupport.SaveDraftHandler(db, draftContext).HandleAsync(
            new SaveSaleDraftCommand(Guid.CreateVersion7(), [new DraftLineDto(draftLine.Id, 2000, draftLine.Price.Cents)]), ct);
    }

    private static async Task<Product> FindProductAsync(TestDb db, string sku)
    {
        await using var context = db.CreateDbContext();
        return context.Products.Single(p => p.Sku == sku);
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

    /// <summary>Desde 0.3.0: producto en kilo que controla inventario, con 4 movimientos.</summary>
    public const string InventorySku = KilogramSku;

    /// <summary>Desde 0.3.0: existencia final del producto con inventario (10.500 + 2 + 1.5 − 4).</summary>
    public const long InventoryOnHandThousandths = 10_000;

    public const int InventoryMovementCount = 4;

    /// <summary>Desde 0.4.0: pieza con inventario que una venta dejó en -2.</summary>
    public const string NegativeStockSku = "MUE-001";
    public const long NegativeStockThousandths = -2_000;

    /// <summary>Desde 0.4.0: producto vendido sin control de inventario.</summary>
    public const string SaleWithoutInventorySku = "MUE-005";

    /// <summary>Desde 0.4.0: existencia final del producto en kilo (10.000 − 2 − 1.5 + 1.5 de la cancelación).</summary>
    public const long InventoryOnHandAfterSalesThousandths = 8_000;

    /// <summary>Desde 0.4.0: 3 ventas (una cancelada), 4 líneas, 3 pagos, un borrador y una entrada de bitácora.</summary>
    public const int SaleCount = 3;
    public const string CancellationReason = "Error de captura";

    /// <summary>Desde 0.6.0: usuarios de muestra. El administrador hace la venta 1; el cajero, la 2 y la 3 y el borrador.</summary>
    public const string AdminUserName = "admin";
    public const string CashierUserName = "cajero";

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
