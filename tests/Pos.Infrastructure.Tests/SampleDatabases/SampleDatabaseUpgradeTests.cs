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

        // 012: la tabla del sello de licencia queda creada y vacía; el sello se siembra al primer arranque.
        Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM LicenseSeals"));

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
        AssertReturns(connection, sampleFile);
        AssertCredit(connection, sampleFile);
        AssertDiscounts(connection, sampleFile);
        await AssertLegacyCancelledCashAsync(db, connection, sampleFile);

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

        // Desde 0.9.0 hay además una venta a crédito (la 4) del producto sin inventario; desde 0.10.0, dos
        // ventas con descuento (la 5 y la 6) de una línea cada una.
        var creditSales = HasCredit(sampleFile) ? SampleData.CreditSaleCount : 0;
        var discountSales = HasDiscounts(sampleFile) ? SampleData.DiscountSaleCount : 0;
        var extraSales = creditSales + discountSales;
        Assert.Equal(SampleData.SaleCount + extraSales, Scalar<long>(connection, "SELECT COUNT(*) FROM Sales"));
        Assert.Equal(
            string.Join(',', Enumerable.Range(1, SampleData.SaleCount + extraSales)),
            Scalar<string>(connection, "SELECT group_concat(FolioNumber) FROM (SELECT FolioNumber FROM Sales ORDER BY FolioNumber)"));
        Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM Sales WHERE Status = 'CANCELLED' AND CancellationReason = '{SampleData.CancellationReason}' AND CancelledAt IS NOT NULL"));
        Assert.Equal(4 + extraSales, Scalar<long>(connection, "SELECT COUNT(*) FROM SaleLines"));
        Assert.Equal(3 + extraSales, Scalar<long>(connection, "SELECT COUNT(*) FROM SalePayments"));
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
        Assert.Equal(VersionOf(sampleFile) >= new Version(0, 6, 0) ? 3 : 1, Scalar<long>(connection, "SELECT COUNT(*) FROM Users"));
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

        if (VersionOf(sampleFile) >= new Version(0, 6, 0))
        {
            // Las ventas conservan a su cajero: el administrador hizo la 1 y el cajero la 2 y la 3 (con su
            // borrador); desde 0.9.0 el cajero también hizo la venta a crédito.
            Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM Sales s JOIN Users u ON u.Id = s.CreatedBy WHERE u.UserName = '{SampleData.AdminUserName}'"));
            Assert.Equal(
                2 + (HasCredit(sampleFile) ? SampleData.CreditSaleCount : 0) + (HasDiscounts(sampleFile) ? SampleData.DiscountSaleCount : 0),
                Scalar<long>(connection, $"SELECT COUNT(*) FROM Sales s JOIN Users u ON u.Id = s.CreatedBy WHERE u.UserName = '{SampleData.CashierUserName}'"));
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
        if (VersionOf(sampleFile) >= new Version(0, 7, 0))
        {
            // Desde 0.7.0 la base de ejemplo ya trae turnos (uno cerrado y uno abierto) y sus ventas.
            Assert.Equal(2, Scalar<long>(connection, "SELECT COUNT(*) FROM CashShifts"));
            Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM CashShifts WHERE Status = 'CLOSED' AND CountedCashCents IS NOT NULL"));
            Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM CashShifts WHERE Status = 'OPEN' AND ExpectedCashCents IS NULL"));
            Assert.Equal(
                SampleData.SaleCount + (HasCredit(sampleFile) ? SampleData.CreditSaleCount : 0) + (HasDiscounts(sampleFile) ? SampleData.DiscountSaleCount : 0),
                Scalar<long>(connection, "SELECT COUNT(*) FROM Sales WHERE CashShiftId IS NOT NULL"));
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

    /// <summary>
    /// 013: las tablas de devoluciones y notas existen; las bases anteriores las dejan vacías con los
    /// acumulados en 0 y los turnos cerrados antes sin instantánea de reintegros; la de 0.8.0 conserva
    /// su devolución parcial con nota de crédito, que cuadra con la venta.
    /// </summary>
    private static void AssertReturns(SqliteConnection connection, string sampleFile)
    {
        Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM Sales WHERE ReturnedCents < 0 OR (ReturnedCents <> 0 AND Status <> 'COMPLETED')"));
        Assert.Contains(
            "WHERE \"Status\" = 'PENDING_REVERSAL'",
            Scalar<string>(connection, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'IX_SaleReturnRefunds_Pending'"),
            StringComparison.Ordinal);

        if (VersionOf(sampleFile) < new Version(0, 8, 0))
        {
            foreach (var table in new[] { "SaleReturns", "SaleReturnLines", "SaleReturnRefunds", "CreditNotes", "CreditNoteMovements" })
            {
                Assert.Equal(0, Scalar<long>(connection, $"SELECT COUNT(*) FROM {table}"));
            }

            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM Sales WHERE ReturnedCents <> 0"));
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM SaleLines WHERE ReturnedQuantity <> 0"));
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM SalePayments WHERE CreditNoteId IS NOT NULL"));
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM CashShifts WHERE CashRefundsCents IS NOT NULL OR NonCashRefundsCents IS NOT NULL OR CreditNotesIssuedCents IS NOT NULL"));
            return;
        }

        Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM SaleReturns WHERE Kind = 'PARTIAL' AND Compensation = 'CREDIT_NOTE' AND Reason = '{SampleData.ReturnReason}'"));
        Assert.Equal(
            0,
            Scalar<long>(connection, """
                SELECT COUNT(*) FROM Sales s
                WHERE s.ReturnedCents <> IFNULL((SELECT SUM(l.AmountCents) FROM SaleReturnLines l JOIN SaleReturns r ON r.Id = l.SaleReturnId WHERE r.SaleId = s.Id), 0)
                """));
        Assert.Equal(
            Scalar<long>(connection, "SELECT TotalCents FROM SaleReturns"),
            Scalar<long>(connection, "SELECT InitialCents FROM CreditNotes"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM CreditNoteMovements WHERE Type = 'ISSUE' AND Sequence = 1"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM AuditEntries WHERE Action = 'SALE_RETURNED' AND EntityType = 'Sale'"));
    }

    /// <summary>
    /// 013, research §6: las ventas ya canceladas (sin devolución registrada) conservan su efectivo
    /// heredado en los totales del turno al migrar.
    /// </summary>
    private static async Task AssertLegacyCancelledCashAsync(TestDb db, SqliteConnection connection, string sampleFile)
    {
        // Solo desde 0.7.0 las ventas pertenecen a turnos.
        if (VersionOf(sampleFile) < new Version(0, 7, 0))
        {
            return;
        }

        var expected = Scalar<long>(connection, """
            SELECT IFNULL(SUM(p.AmountCents), 0) FROM SalePayments p JOIN Sales s ON s.Id = p.SaleId
            WHERE s.Status = 'CANCELLED' AND p.Method = 'CASH' AND s.CashShiftId IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM SaleReturns r WHERE r.SaleId = s.Id)
            """);
        var shiftIds = new List<Guid>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Id FROM CashShifts";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                shiftIds.Add(reader.GetGuid(0));
            }
        }

        long cancelledCash = 0;
        await using var context = db.CreateDbContext();
        foreach (var shiftId in shiftIds)
        {
            cancelledCash += (await new Pos.Infrastructure.Sales.SaleRepository(context).GetShiftTotalsAsync(shiftId, TestContext.Current.CancellationToken)).CashCancelledCents;
        }

        Assert.True(expected > 0);
        Assert.Equal(expected, cancelledCash);
    }

    /// <summary>
    /// 014: las tablas de clientes y crédito existen; las bases anteriores a 0.9.0 las dejan vacías, sin
    /// pagos <c>ACCOUNT</c> y con el bloque "Crédito" de los turnos en nulo. La de 0.9.0 conserva su
    /// cliente, su venta a crédito y su abono, con el saldo igual al libro (SC-004).
    /// </summary>
    private static void AssertCredit(SqliteConnection connection, string sampleFile)
    {
        Assert.Contains(
            "WHERE \"TaxId\" IS NOT NULL",
            Scalar<string>(connection, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'IX_Customers_TaxId'"),
            StringComparison.Ordinal);
        Assert.Equal(
            0,
            Scalar<long>(connection, """
                SELECT COUNT(*) FROM Receivables r
                WHERE r.BalanceCents <> r.OriginalCents + IFNULL((SELECT SUM(e.AmountCents) FROM ReceivableEntries e WHERE e.ReceivableId = r.Id), 0)
                """));

        if (!HasCredit(sampleFile))
        {
            foreach (var table in new[] { "Customers", "Receivables", "ReceivableEntries", "CustomerPayments" })
            {
                Assert.Equal(0, Scalar<long>(connection, $"SELECT COUNT(*) FROM {table}"));
            }

            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM SalePayments WHERE Method = 'ACCOUNT'"));
            Assert.Equal(
                0,
                Scalar<long>(connection, """
                    SELECT COUNT(*) FROM CashShifts
                    WHERE OnAccountSalesCents IS NOT NULL OR CustomerPaymentsCashCents IS NOT NULL OR CustomerPaymentsNonCashCents IS NOT NULL
                       OR CustomerPaymentVoidsCashCents IS NOT NULL OR CustomerPaymentVoidsNonCashCents IS NOT NULL
                    """));
            return;
        }

        Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM Customers WHERE Name = '{SampleData.CustomerName}' AND TaxId = '{SampleData.CustomerTaxId}' AND CreditMode = 'CREDIT' AND IsActive = 1"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM SalePayments WHERE Method = 'ACCOUNT'"));
        Assert.Equal(
            1,
            Scalar<long>(connection, """
                SELECT COUNT(*) FROM Receivables r JOIN Sales s ON s.Id = r.SaleId
                WHERE r.OriginalCents = s.TotalCents AND r.Status = 'PENDING'
                """));
        Assert.Equal(SampleData.CreditBalanceCents, Scalar<long>(connection, "SELECT BalanceCents FROM Receivables"));
        Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM CustomerPayments WHERE Number = 1 AND Status = 'ACTIVE' AND Method = 'CASH' AND AmountCents = {SampleData.CreditPaymentCents}"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM ReceivableEntries WHERE Type = 'PAYMENT'"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM AuditEntries WHERE Action = 'CREDIT_SALE_REGISTERED'"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM AuditEntries WHERE Action = 'CUSTOMER_PAYMENT_REGISTERED'"));
    }

    /// <summary>
    /// 015: las tablas de descuentos existen; en las bases anteriores a 0.10.0 cada línea queda con su importe
    /// original igual al registrado y sin descuentos. La de 0.10.0 conserva su cupón, sus dos ventas con
    /// descuento (una autorizada por el administrador) y cuadra: Σ líneas = total y descontado = Σ descuentos.
    /// </summary>
    private static void AssertDiscounts(SqliteConnection connection, string sampleFile)
    {
        Assert.Equal(
            0,
            Scalar<long>(connection, """
                SELECT COUNT(*) FROM SaleLines
                WHERE AmountCents <> OriginalAmountCents - LineDiscountCents - OrderDiscountCents
                """));
        Assert.Equal(
            0,
            Scalar<long>(connection, """
                SELECT COUNT(*) FROM Sales s
                WHERE s.DiscountCents <> IFNULL((SELECT SUM(d.AmountCents) FROM SaleDiscounts d WHERE d.SaleId = s.Id), 0)
                   OR s.DiscountCents <> (SELECT SUM(l.OriginalAmountCents - l.AmountCents) FROM SaleLines l WHERE l.SaleId = s.Id)
                """));

        if (!HasDiscounts(sampleFile))
        {
            foreach (var table in new[] { "Coupons", "SaleDiscounts", "DiscountApprovals" })
            {
                Assert.Equal(0, Scalar<long>(connection, $"SELECT COUNT(*) FROM {table}"));
            }

            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM SaleLines WHERE OriginalAmountCents <> AmountCents"));
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM Sales WHERE DiscountCents <> 0"));
            return;
        }

        Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM Coupons WHERE Code = '{SampleData.CouponCode}' AND UsesCount = 1 AND UsageLimit = 5 AND IsActive = 1"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM DiscountApprovals"));
        Assert.Equal(
            SampleData.LineDiscountCents + SampleData.CouponDiscountCents,
            Scalar<long>(connection, "SELECT SUM(DiscountCents) FROM Sales"));
        Assert.Equal(
            1,
            Scalar<long>(connection, $"""
                SELECT COUNT(*) FROM SaleDiscounts d JOIN Users u ON u.Id = d.AuthorizedBy
                WHERE d.Kind = 'LINE' AND d.AmountCents = {SampleData.LineDiscountCents} AND u.UserName = '{SampleData.AdminUserName}'
                """));
        Assert.Equal(
            1,
            Scalar<long>(connection, $"SELECT COUNT(*) FROM SaleDiscounts WHERE Kind = 'COUPON' AND CouponCode = '{SampleData.CouponCode}' AND AuthorizedBy IS NULL"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM AuditEntries WHERE Action = 'DISCOUNT_APPLIED_AUTHORIZED'"));
        Assert.Contains("\"discount\"", Scalar<string>(connection, "SELECT LinesJson FROM SaleDrafts"), StringComparison.Ordinal);
    }

    private static bool HasSales(string sampleFile) => VersionOf(sampleFile) >= new Version(0, 4, 0);

    private static bool HasDiscounts(string sampleFile) => VersionOf(sampleFile) >= new Version(0, 10, 0);

    private static bool HasCredit(string sampleFile) => VersionOf(sampleFile) >= new Version(0, 9, 0);

    /// <summary>Versión de la base de ejemplo a partir de su nombre (<c>v0.8.0.db</c>).</summary>
    private static Version VersionOf(string sampleFile) => new(sampleFile[1..^3]);

    private static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)command.ExecuteScalar()!;
    }
}
