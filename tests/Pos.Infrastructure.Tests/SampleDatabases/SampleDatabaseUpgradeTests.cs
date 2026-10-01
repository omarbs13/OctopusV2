using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Startup;
using Pos.Infrastructure.Startup;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Toda base de ejemplo de una versión publicada migra a la versión actual sin perder datos
/// (constitución, Principio IV; SC-007).
/// </summary>
public sealed class SampleDatabaseUpgradeTests
{
    public static TheoryData<string> SampleFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "SampleDatabases"), "v*.db").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SampleFiles))]
    public async Task BaseDeEjemplo_MigraALaVersionActualConservandoLosDatos(string sampleFile)
    {
        using var dir = new TempDataDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "SampleDatabases", sampleFile), dir.Paths.DatabaseFile);
        using var db = TestDb.CreateUnmigrated(dir);
        var backups = new SqliteBackupService(dir.Paths, db.Clock, NullLogger<SqliteBackupService>.Instance);
        var startup = new DatabaseStartup(
            new SqliteDatabaseMaintenance(db, dir.Paths),
            backups,
            db.Clock,
            NullLogger<DatabaseStartup>.Instance);

        var result = await startup.RunAsync(TestContext.Current.CancellationToken);

        Assert.IsType<StartupResult.Ready>(result);
        Assert.Equal("ok", DatabaseTestHelpers.QuickCheck(dir.Paths.DatabaseFile));

        using var connection = new SqliteConnection($"Data Source={dir.Paths.DatabaseFile};Pooling=False");
        connection.Open();
        Assert.Equal(SampleData.ProductCount, Scalar<long>(connection, "SELECT COUNT(*) FROM Products"));
        Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM Products WHERE Sku = '{SampleData.DeletedSku}' AND DeletedAt IS NOT NULL"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE IsActive = 0 AND DeletedAt IS NULL"));
        Assert.Equal(SampleData.AccentedName, Scalar<string>(connection, $"SELECT Name FROM Products WHERE Sku = '{SampleData.AccentedSku}'"));
        Assert.Equal(SampleData.AccentedPriceCents, Scalar<long>(connection, $"SELECT PriceCents FROM Products WHERE Sku = '{SampleData.AccentedSku}'"));

        // 003: unidad de medida (clarificación 1) e índices filtrados tras reconstruir Products.
        Assert.Equal(8, Scalar<long>(connection, "SELECT COUNT(*) FROM UnitsOfMeasure"));
        Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE UnitCode IS NULL OR UnitCode NOT IN (SELECT Code FROM UnitsOfMeasure)"));
        if (sampleFile == "v0.1.0.db")
        {
            Assert.Equal(SampleData.ProductCount, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE UnitCode = 'H87'"));
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM ProductImages"));
        }
        else
        {
            // Desde 0.2.0: unidad, imagen y precio 0 se conservan al migrar.
            Assert.Equal("KGM", Scalar<string>(connection, $"SELECT UnitCode FROM Products WHERE Sku = '{SampleData.KilogramSku}'"));
            Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM ProductImages"));
            Assert.Equal(
                [SampleData.ImageSeed, SampleData.ImageSeed, SampleData.ImageSeed],
                Scalar<byte[]>(connection, $"SELECT i.Content FROM ProductImages i JOIN Products p ON p.Id = i.ProductId WHERE p.Sku = '{SampleData.ImageSku}'"));
            Assert.Equal(0, Scalar<long>(connection, $"SELECT PriceCents FROM Products WHERE Sku = '{SampleData.ZeroPriceSku}'"));
        }

        AssertInventory(connection, sampleFile);
        AssertSales(connection, sampleFile);
        AssertUsers(connection, sampleFile);
        AssertCashShifts(connection, sampleFile);
        AssertReports(connection);

        foreach (var index in new[] { "IX_Products_Sku", "IX_Products_Barcode", "IX_Products_NameSearch" })
        {
            var sql = Scalar<string>(connection, $"SELECT sql FROM sqlite_master WHERE type = 'index' AND name = '{index}'");
            Assert.Contains("WHERE \"DeletedAt\" IS NULL", sql, StringComparison.Ordinal);
        }

        using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_key_check";
        using var violations = foreignKeys.ExecuteReader();
        Assert.False(violations.Read());
    }

    /// <summary>004: los productos existentes quedan sin control de inventario, sin existencia ni movimientos (FR-023).</summary>
    private static void AssertInventory(SqliteConnection connection, string sampleFile)
    {
        Assert.Equal(3, Scalar<long>(connection, "SELECT COUNT(*) FROM UnitsOfMeasure WHERE DecimalPlaces = 3 AND Code IN ('KGM', 'LTR', 'MTR')"));
        Assert.Equal(5, Scalar<long>(connection, "SELECT COUNT(*) FROM UnitsOfMeasure WHERE DecimalPlaces = 0"));

        if (sampleFile == "v0.3.0.db")
        {
            Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE TracksInventory = 1"));
            Assert.Equal(5000, Scalar<long>(connection, $"SELECT MinimumStock FROM Products WHERE Sku = '{SampleData.InventorySku}'"));
            Assert.Equal(SampleData.InventoryOnHandThousandths, Scalar<long>(connection, "SELECT OnHand FROM ProductStocks"));
            Assert.Equal(SampleData.InventoryMovementCount, Scalar<long>(connection, "SELECT COUNT(*) FROM InventoryMovements"));
            Assert.Equal(4, Scalar<long>(connection, "SELECT COUNT(DISTINCT Type) FROM InventoryMovements"));
        }
        else if (HasSales(sampleFile))
        {
            // 005: la existencia negativa, los movimientos de venta y las ventas se conservan tal cual.
            Assert.Equal(2, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE TracksInventory = 1"));
            Assert.Equal(SampleData.NegativeStockThousandths, Scalar<long>(connection, $"SELECT s.OnHand FROM ProductStocks s JOIN Products p ON p.Id = s.ProductId WHERE p.Sku = '{SampleData.NegativeStockSku}'"));
            Assert.Equal(SampleData.InventoryOnHandAfterSalesThousandths, Scalar<long>(connection, $"SELECT s.OnHand FROM ProductStocks s JOIN Products p ON p.Id = s.ProductId WHERE p.Sku = '{SampleData.InventorySku}'"));
            Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM InventoryMovements WHERE Type = 'SALE_CANCEL'"));
            Assert.Equal(3, Scalar<long>(connection, "SELECT COUNT(*) FROM InventoryMovements WHERE Type = 'SALE'"));
            Assert.Equal(
                0,
                Scalar<long>(connection, """
                    SELECT COUNT(*) FROM ProductStocks s
                    WHERE s.OnHand <> (SELECT IFNULL(SUM(CASE WHEN m.Type IN ('ADJUST_OUT', 'SALE') THEN -m.Quantity ELSE m.Quantity END), 0)
                                       FROM InventoryMovements m WHERE m.ProductId = s.ProductId)
                    """));
        }
        else
        {
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE TracksInventory <> 0 OR MinimumStock IS NOT NULL"));
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM ProductStocks"));
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM InventoryMovements"));
        }
    }

    /// <summary>
    /// 005: las bases anteriores quedan con las tablas de ventas vacías; la de 0.4.0 conserva sus
    /// ventas, pagos, borrador y bitácora, y las ventas cuadran con sus líneas y pagos.
    /// </summary>
    private static void AssertSales(SqliteConnection connection, string sampleFile)
    {
        if (!HasSales(sampleFile))
        {
            foreach (var table in new[] { "Sales", "SaleLines", "SalePayments", "SaleDrafts", "AuditEntries" })
            {
                Assert.Equal(0, Scalar<long>(connection, $"SELECT COUNT(*) FROM {table}"));
            }

            return;
        }

        Assert.Equal(SampleData.SaleCount, Scalar<long>(connection, "SELECT COUNT(*) FROM Sales"));
        Assert.Equal("1,2,3", Scalar<string>(connection, "SELECT group_concat(FolioNumber) FROM (SELECT FolioNumber FROM Sales ORDER BY FolioNumber)"));
        Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM Sales WHERE Status = 'CANCELLED' AND CancellationReason = '{SampleData.CancellationReason}' AND CancelledAt IS NOT NULL"));
        Assert.Equal(4, Scalar<long>(connection, "SELECT COUNT(*) FROM SaleLines"));
        Assert.Equal(3, Scalar<long>(connection, "SELECT COUNT(*) FROM SalePayments"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM SaleDrafts"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM AuditEntries WHERE Action = 'SALE_CANCELLED' AND EntityType = 'Sale'"));
        Assert.Equal(
            0,
            Scalar<long>(connection, """
                SELECT COUNT(*) FROM Sales s
                WHERE s.TotalCents <> (SELECT SUM(AmountCents) FROM SaleLines WHERE SaleId = s.Id)
                   OR s.TotalCents <> (SELECT SUM(AmountCents) FROM SalePayments WHERE SaleId = s.Id)
                """));
    }

    /// <summary>
    /// 007: existe "Sistema" (sin contraseña, inactivo), los registros previos siguen a su nombre, el
    /// borrador pasa a la fila de "Sistema" con sus líneas y la llave primaria ya no es <c>Slot</c>.
    /// </summary>
    private static void AssertUsers(SqliteConnection connection, string sampleFile)
    {
        // Desde 0.6.0 la base de ejemplo ya trae un administrador y un cajero además de "Sistema".
        Assert.Equal(sampleFile is "v0.6.0.db" or "v0.7.0.db" ? 3 : 1, Scalar<long>(connection, "SELECT COUNT(*) FROM Users"));
        Assert.Equal(
            1,
            Scalar<long>(connection, $"""
                SELECT COUNT(*) FROM Users
                WHERE Id = '{SystemUser.Id}' AND IsSystem = 1 AND IsActive = 0 AND PasswordHash IS NULL
                  AND NormalizedUserName = 'SISTEMA'
                """));

        Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM pragma_table_info('SaleDrafts') WHERE name = 'Slot'"));
        Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE sql LIKE '%CK_SaleDrafts_Slot%'"));

        if (!HasSales(sampleFile))
        {
            return;
        }

        if (sampleFile is "v0.6.0.db" or "v0.7.0.db")
        {
            // Las ventas conservan a su cajero: el administrador hizo la 1 y el cajero la 2 y la 3 (con su borrador).
            Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM Sales s JOIN Users u ON u.Id = s.CreatedBy WHERE u.UserName = '{SampleData.AdminUserName}'"));
            Assert.Equal(2, Scalar<long>(connection, $"SELECT COUNT(*) FROM Sales s JOIN Users u ON u.Id = s.CreatedBy WHERE u.UserName = '{SampleData.CashierUserName}'"));
            Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM SaleDrafts d JOIN Users u ON u.Id = d.UserId WHERE u.UserName = '{SampleData.CashierUserName}'"));
            return;
        }

        // Ventas y movimientos previos conservan su usuario ("Sistema") y sus totales.
        Assert.Equal(SampleData.SaleCount, Scalar<long>(connection, $"SELECT COUNT(*) FROM Sales WHERE CreatedBy = '{SystemUser.Id}'"));
        Assert.Equal(0, Scalar<long>(connection, $"SELECT COUNT(*) FROM Sales WHERE CreatedBy <> '{SystemUser.Id}'"));

        // El borrador existente queda bajo "Sistema" con sus líneas (la reconstrucción de la tabla los copia).
        Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM SaleDrafts WHERE UserId = '{SystemUser.Id}'"));
        Assert.Contains("productId", Scalar<string>(connection, $"SELECT LinesJson FROM SaleDrafts WHERE UserId = '{SystemUser.Id}'"), StringComparison.Ordinal);
    }

    /// <summary>
    /// 008: las ventas anteriores quedan sin turno (<c>CashShiftId</c> nulo), no hay turnos y el índice
    /// único filtrado garantiza un solo turno abierto por caja.
    /// </summary>
    private static void AssertCashShifts(SqliteConnection connection, string sampleFile)
    {
        if (sampleFile == "v0.7.0.db")
        {
            // Desde 0.7.0 la base de ejemplo ya trae turnos (uno cerrado y uno abierto) y sus ventas.
            Assert.Equal(2, Scalar<long>(connection, "SELECT COUNT(*) FROM CashShifts"));
            Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM CashShifts WHERE Status = 'CLOSED' AND CountedCashCents IS NOT NULL"));
            Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM CashShifts WHERE Status = 'OPEN' AND ExpectedCashCents IS NULL"));
            Assert.Equal(SampleData.SaleCount, Scalar<long>(connection, "SELECT COUNT(*) FROM Sales WHERE CashShiftId IS NOT NULL"));
            return;
        }

        Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM Sales WHERE CashShiftId IS NOT NULL"));
        Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM CashShifts"));
        Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM CashMovements"));
        Assert.Contains(
            "WHERE \"Status\" = 'OPEN'",
            Scalar<string>(connection, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'IX_CashShifts_OpenPerRegister'"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 009: los productos existentes quedan sin marca de crítico y el índice de existencias por fecha
    /// existe; la migración solo agrega una columna y un índice.
    /// </summary>
    private static void AssertReports(SqliteConnection connection)
    {
        Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE IsCritical <> 0"));
        Assert.Equal(
            1,
            Scalar<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_InventoryMovements_Product_CreatedAt'"));
    }

    private static bool HasSales(string sampleFile) => sampleFile is "v0.4.0.db" or "v0.5.0.db" or "v0.6.0.db" or "v0.7.0.db";

    private static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)command.ExecuteScalar()!;
    }
}
