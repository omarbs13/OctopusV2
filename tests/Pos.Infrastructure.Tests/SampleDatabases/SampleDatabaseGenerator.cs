using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.CashShifts.CloseShift;
using Pos.Application.CashShifts.GenerateShiftReadout;
using Pos.Application.Customers.CreateCustomer;
using Pos.Application.Discounts;
using Pos.Application.Discounts.Coupons.SaveCoupon;
using Pos.Application.Purchases;
using Pos.Application.Purchases.RegisterPurchase;
using Pos.Application.Purchases.VoidPurchase;
using Pos.Application.Receivables;
using Pos.Application.Receivables.RegisterCustomerPayment;
using Pos.Application.Returns;
using Pos.Application.Sales;
using Pos.Application.Sales.ConfirmSale;
using Pos.Application.Sales.SaveSaleDraft;
using Pos.Application.Suppliers.CreateSupplier;
using Pos.Application.Suppliers.SetSupplierActive;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Categories;
using Pos.Domain.Common;
using Pos.Domain.Customers;
using Pos.Domain.Discounts;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Domain.Suppliers;
using Pos.Domain.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.CashShifts;
using Pos.Infrastructure.CreditNotes;
using Pos.Infrastructure.Customers;
using Pos.Infrastructure.Discounts;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Purchases;
using Pos.Infrastructure.Receivables;
using Pos.Infrastructure.Returns;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Suppliers;
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

        // Desde 0.8.0: una devolución parcial (el servicio de la venta 1) compensada con una nota de crédito.
        await SeedReturnAsync(db, adminId);

        // Desde 0.9.0: un cliente con crédito, una venta a crédito del cajero y un abono en efectivo.
        await SeedCreditAsync(db, cashierId);

        // Desde 0.10.0: un cupón, una venta con descuento de línea autorizado, una con cupón y un borrador con descuento.
        await SeedDiscountsAsync(db, adminId, cashierId);

        // Desde 0.11.0: una categoría activa con productos vendidos, una inactiva con un producto, una borrada sin
        // productos y el resto de los productos sin categoría.
        await SeedCategoriesAsync(db, adminId);

        // Desde 0.12.0: el turno 1 quedó cerrado sin Corte Z (como los anteriores a la actualización); el
        // turno 2 tiene un Corte X y se cierra con el Corte Z; el turno 3 queda abierto.
        await SeedShiftCutsAsync(db, adminId, cashierId);

        // Desde 0.14.0: un proveedor activo con RUC, uno inactivo y uno sin RUC; una compra vigente con una
        // bonificación y otra anulada, sobre los dos productos que controlan inventario.
        await SeedPurchasesAsync(db, adminId);

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

    private static async Task SeedReturnAsync(TestDb db, Guid adminId)
    {
        var ct = TestContext.Current.CancellationToken;
        db.User.UserId = adminId;
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc);

        var service = await FindProductAsync(db, SampleData.SaleWithoutInventorySku);
        await using var context = db.CreateDbContext();
        var line = await context.SaleLines.AsNoTracking().SingleAsync(l => l.ProductId == service.Id, ct);

        var grants = new AuthorizationGrants(db.Clock);
        var access = new AllowAllAccessControl();
        var processor = new SaleReturnProcessor(
            access,
            grants,
            new SaleRepository(context),
            new ReturnRepository(context),
            new CreditNoteRepository(context),
            new InventoryRepository(context),
            new ReturnCashGate(new CashShiftRepository(context), new SaleRepository(context), access, db.User, SalesTestSupport.ShiftGuardFor(db, context)),
            new TestReturnsSettingsStore(),
            new AuditLog(context),
            new WriteTransactions(context),
            db.Clock,
            db.User,
            NullLogger<SaleReturnProcessor>.Instance);

        var version = (await context.Sales.AsNoTracking().SingleAsync(s => s.Id == line.SaleId, ct)).Version;
        var result = await processor.ProcessAsync(
            new ReturnRequest(
                line.SaleId,
                version,
                ReturnKind.Partial,
                [new ReturnLineRequest(line.Id, line.QuantityThousandths)],
                SampleData.ReturnReason,
                ReturnCompensation.CreditNote,
                grants.Issue(Permission.ApproveReturns, adminId, adminId)),
            ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
    }

    private static async Task SeedCreditAsync(TestDb db, Guid cashierId)
    {
        var ct = TestContext.Current.CancellationToken;
        db.User.UserId = cashierId;
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);
        var access = new AllowAllAccessControl();

        Guid customerId;
        await using (var context = db.CreateDbContext())
        {
            var created = await new CreateCustomerHandler(
                access,
                new CustomerRepository(context),
                new AuditLog(context),
                new WriteTransactions(context),
                new CreateCustomerValidator(),
                NullLogger<CreateCustomerHandler>.Instance)
                .HandleAsync(new CreateCustomerCommand(SampleData.CustomerName, "555-0100", null, SampleData.CustomerTaxId, SampleData.CreditLimitCents, CreditMode.Credit), ct);
            Assert.True(created.IsSuccess, created.Error?.ToString());
            customerId = created.Value;
        }

        // El cajero ya tiene el turno abierto (vendió la 3): la venta a crédito entra en él.
        var service = await FindProductAsync(db, SampleData.SaleWithoutInventorySku);
        await SalesTestSupport.EnsureShiftAsync(db);
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 15, 5, 0, DateTimeKind.Utc);
        await using (var context = db.CreateDbContext())
        {
            var total = SaleMath.LineAmount(Quantity.FromThousandths(SampleData.CreditSaleQuantityThousandths), service.Price).Cents;
            var sold = await new ConfirmSaleHandler(
                access,
                new ProductRepository(context),
                new InventoryRepository(context),
                new SaleRepository(context),
                new SqliteSaleDraftStore(context, db.Clock, db.User),
                SalesTestSupport.ShiftGuardFor(db, context),
                new WriteTransactions(context),
                new ConfirmSaleValidator(),
                NullLogger<ConfirmSaleHandler>.Instance,
                null,
                new CreditNoteRepository(context),
                new AuditLog(context),
                new CustomerRepository(context),
                new ReceivableRepository(context),
                db.User)
                .HandleAsync(
                    new ConfirmSaleCommand(
                        Guid.CreateVersion7(),
                        [new ConfirmLineInput(service.Id, SampleData.CreditSaleQuantityThousandths, service.Price.Cents)],
                        [new PaymentInput(PaymentMethod.OnAccount, total, null, null)],
                        customerId),
                    ct);
            Assert.True(sold.IsSuccess, sold.Error?.ToString());
        }

        db.Clock.UtcNow = new DateTime(2026, 9, 30, 15, 30, 0, DateTimeKind.Utc);
        await using (var context = db.CreateDbContext())
        {
            var paid = await new RegisterCustomerPaymentHandler(
                access,
                new CustomerRepository(context),
                new ReceivableRepository(context),
                new CustomerPaymentRepository(context),
                new PaymentShiftGate(new CashShiftRepository(context), new SaleRepository(context), access, db.User, SalesTestSupport.ShiftGuardFor(db, context)),
                new AuditLog(context),
                new WriteTransactions(context),
                db.User,
                new RegisterCustomerPaymentValidator(),
                NullLogger<RegisterCustomerPaymentHandler>.Instance)
                .HandleAsync(new RegisterCustomerPaymentCommand(Guid.CreateVersion7(), customerId, SampleData.CreditPaymentCents, PaymentMethod.Cash, null), ct);
            Assert.True(paid.IsSuccess, paid.Error?.ToString());
        }

        // La venta del cajero consumió su borrador: se vuelve a conservar uno, como en 0.4.0 a 0.8.0.
        var draftLine = await FindProductAsync(db, SampleData.ImageSku);
        await using var draftContext = db.CreateDbContext();
        await SalesTestSupport.SaveDraftHandler(db, draftContext).HandleAsync(
            new SaveSaleDraftCommand(Guid.CreateVersion7(), [new DraftLineDto(draftLine.Id, 2000, draftLine.Price.Cents)]), ct);
    }

    private static async Task SeedDiscountsAsync(TestDb db, Guid adminId, Guid cashierId)
    {
        var ct = TestContext.Current.CancellationToken;
        var access = new AllowAllAccessControl();

        db.User.UserId = adminId;
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 16, 0, 0, DateTimeKind.Utc);
        await using (var context = db.CreateDbContext())
        {
            var created = await new SaveCouponHandler(
                access,
                new CouponRepository(context),
                new ProductRepository(context),
                new AuditLog(context),
                new WriteTransactions(context),
                new SaveCouponValidator(),
                NullLogger<SaveCouponHandler>.Instance)
                .HandleAsync(new SaveCouponCommand(null, SampleData.CouponCode, DiscountMode.Percent, "10", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31), 5), ct);
            Assert.True(created.IsSuccess, created.Error?.ToString());
        }

        // El cajero vende con su turno abierto: un descuento de $15.00 (15 %) autorizado por el administrador.
        db.User.UserId = cashierId;
        var service = await FindProductAsync(db, SampleData.SaleWithoutInventorySku);
        var couponProduct = await FindProductAsync(db, SampleData.CouponProductSku);
        await SalesTestSupport.EnsureShiftAsync(db);
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 16, 5, 0, DateTimeKind.Utc);
        var draftId = Guid.CreateVersion7();
        var approval = DiscountApproval.Create(draftId, cashierId, adminId, DiscountScope.Line, service.Id, 1_500, db.Clock.UtcNow);
        await using (var context = db.CreateDbContext())
        {
            context.DiscountApprovals.Add(approval);
            await context.SaveChangesAsync(ct);
        }

        await SellWithDiscountsAsync(db, new ConfirmSaleCommand(
            draftId,
            [new ConfirmLineInput(service.Id, 2_000, service.Price.Cents, new LineDiscountInput(DiscountMode.Amount, SampleData.LineDiscountCents, approval.Id))],
            [new PaymentInput(PaymentMethod.Cash, 0, (2 * service.Price.Cents) - SampleData.LineDiscountCents, null)]));

        db.Clock.UtcNow = new DateTime(2026, 9, 30, 16, 10, 0, DateTimeKind.Utc);
        await SellWithDiscountsAsync(db, new ConfirmSaleCommand(
            Guid.CreateVersion7(),
            [new ConfirmLineInput(couponProduct.Id, 1_000, couponProduct.Price.Cents)],
            [new PaymentInput(PaymentMethod.Cash, 0, couponProduct.Price.Cents - SampleData.CouponDiscountCents, null)],
            OrderDiscount: OrderDiscountInput.Coupon(SampleData.CouponCode)));

        // Venta conservada del cajero con un descuento de línea del 5 %, que no necesita aprobación.
        var draftLine = await FindProductAsync(db, SampleData.ImageSku);
        await using var draftContext = db.CreateDbContext();
        await SalesTestSupport.SaveDraftHandler(db, draftContext).HandleAsync(
            new SaveSaleDraftCommand(
                Guid.CreateVersion7(),
                [new DraftLineDto(draftLine.Id, 2000, draftLine.Price.Cents, new DraftDiscountDto(DiscountMode.Percent, 500))]),
            ct);
    }

    private static async Task SellWithDiscountsAsync(TestDb db, ConfirmSaleCommand command)
    {
        await using var context = db.CreateDbContext();
        var sold = await new ConfirmSaleHandler(
            new AllowAllAccessControl(),
            new ProductRepository(context),
            new InventoryRepository(context),
            new SaleRepository(context),
            new SqliteSaleDraftStore(context, db.Clock, db.User),
            SalesTestSupport.ShiftGuardFor(db, context),
            new WriteTransactions(context),
            new ConfirmSaleValidator(),
            NullLogger<ConfirmSaleHandler>.Instance,
            null,
            new CreditNoteRepository(context),
            new AuditLog(context),
            currentUser: db.User,
            discountSettings: new TestDiscountSettingsStore(),
            approvals: new DiscountApprovalStore(context),
            coupons: new CouponRepository(context),
            clock: db.Clock)
            .HandleAsync(command, TestContext.Current.CancellationToken);
        Assert.True(sold.IsSuccess, sold.Error?.ToString());
    }

    private static async Task SeedCategoriesAsync(TestDb db, Guid adminId)
    {
        db.User.UserId = adminId;
        await using var context = db.CreateDbContext();
        var active = Category.Create(SampleData.ActiveCategoryName, "Categoría de muestra con ventas");
        var inactive = Category.Create(SampleData.InactiveCategoryName, null);
        inactive.Deactivate();
        var deleted = Category.Create(SampleData.DeletedCategoryName, null);
        deleted.Delete(db.Clock.UtcNow);
        context.Categories.AddRange(active, inactive, deleted);

        foreach (var product in context.Products.Where(p => SampleData.ActiveCategorySkus.Contains(p.Sku) || p.Sku == SampleData.InactiveCategorySku))
        {
            var categoryId = product.Sku == SampleData.InactiveCategorySku ? inactive.Id : active.Id;
            product.Update(product.Name, product.Sku, product.Barcode, product.Price, product.UnitCode, product.IsActive, product.TracksInventory, product.MinimumStock, hasMovements: true, categoryId: categoryId);
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SeedShiftCutsAsync(TestDb db, Guid adminId, Guid cashierId)
    {
        var ct = TestContext.Current.CancellationToken;
        var access = new AllowAllAccessControl();

        db.User.UserId = adminId;
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 18, 0, 0, DateTimeKind.Utc);
        await using (var context = db.CreateDbContext())
        {
            var readout = await new GenerateShiftReadoutHandler(
                access,
                db.User,
                new CashShiftRepository(context),
                new SaleRepository(context),
                new AuditLog(context),
                new WriteTransactions(context),
                db.Clock,
                NullLogger<GenerateShiftReadoutHandler>.Instance).HandleAsync(new GenerateShiftReadoutCommand(), ct);
            Assert.True(readout.IsSuccess, readout.Error?.ToString());
        }

        // El administrador cierra el turno del cajero (cierre de turno ajeno, que descarta su venta
        // conservada) con un faltante: Corte Z con comentario.
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 19, 0, 0, DateTimeKind.Utc);
        await using (var context = db.CreateDbContext())
        {
            var shift = (await new CashShiftRepository(context).GetOpenAsync(CashRegister.Default, ct))!;
            var expected = shift.ExpectedCash(await new SaleRepository(context).GetShiftTotalsAsync(shift.Id, ct));
            var counted = expected - SampleData.ClosingShortageCents;
            var closed = await new CloseShiftHandler(
                access,
                db.User,
                new CashShiftRepository(context),
                new SaleRepository(context),
                new SqliteSaleDraftStore(context, db.Clock, db.User),
                SalesTestSupport.ShiftGuardFor(db, context),
                new AuditLog(context),
                new WriteTransactions(context),
                db.Clock,
                NullLogger<CloseShiftHandler>.Instance)
                .HandleAsync(new CloseShiftCommand(shift.Id, shift.Version, counted, expected, SampleData.ClosingComment, DiscardHeldSale: true), ct);
            Assert.True(closed.IsSuccess, closed.Error?.ToString());
        }

        // Se vuelve a conservar la venta del cajero con su descuento, como en 0.10.0 y 0.11.0.
        db.User.UserId = cashierId;
        var draftLine = await FindProductAsync(db, SampleData.ImageSku);
        await using (var draftContext = db.CreateDbContext())
        {
            await SalesTestSupport.SaveDraftHandler(db, draftContext).HandleAsync(
                new SaveSaleDraftCommand(
                    Guid.CreateVersion7(),
                    [new DraftLineDto(draftLine.Id, 2000, draftLine.Price.Cents, new DraftDiscountDto(DiscountMode.Percent, 500))]),
                ct);
        }

        db.User.UserId = adminId;
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 20, 0, 0, DateTimeKind.Utc);
        await SalesTestSupport.EnsureShiftAsync(db);
    }

    private static async Task SeedPurchasesAsync(TestDb db, Guid adminId)
    {
        var ct = TestContext.Current.CancellationToken;
        var access = new AllowAllAccessControl();
        db.User.UserId = adminId;
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 21, 0, 0, DateTimeKind.Utc);

        async Task<Guid> CreateSupplierAsync(string name, string? taxId, PaymentTerms terms, string? days)
        {
            await using var context = db.CreateDbContext();
            var result = await new CreateSupplierHandler(
                    access,
                    new SupplierRepository(context),
                    new AuditLog(context),
                    new WriteTransactions(context),
                    new CreateSupplierValidator(),
                    NullLogger<CreateSupplierHandler>.Instance)
                .HandleAsync(new CreateSupplierCommand(name, taxId, "555-0100", null, null, terms, days), ct);
            Assert.True(result.IsSuccess, result.Error?.ToString());
            return result.Value;
        }

        async Task<PurchaseRegisteredDto> RegisterAsync(Guid supplierId, string invoice, params PurchaseLineInput[] lines)
        {
            await using var context = db.CreateDbContext();
            var result = await new RegisterPurchaseHandler(
                    access,
                    new SupplierRepository(context),
                    new ProductRepository(context),
                    new InventoryRepository(context),
                    new PurchaseRepository(context),
                    new AuditLog(context),
                    new WriteTransactions(context),
                    db.Clock,
                    db.User,
                    new RegisterPurchaseValidator(),
                    NullLogger<RegisterPurchaseHandler>.Instance)
                .HandleAsync(new RegisterPurchaseCommand(supplierId, invoice, DiscountDates.LocalToday(db.Clock).AddDays(-1), lines, "40.00"), ct);
            Assert.True(result.IsSuccess, result.Error?.ToString());
            return result.Value;
        }

        var active = await CreateSupplierAsync(SampleData.SupplierName, SampleData.SupplierTaxId, PaymentTerms.Credit, "30");
        var inactive = await CreateSupplierAsync("Proveedor inactivo de muestra", "RUC-PROV-2", PaymentTerms.Cash, null);
        await CreateSupplierAsync("Proveedor sin RUC de muestra", null, PaymentTerms.Cash, null);
        await using (var context = db.CreateDbContext())
        {
            var version = context.Suppliers.Single(x => x.Id == inactive).Version;
            var result = await new SetSupplierActiveHandler(
                    access,
                    new SupplierRepository(context),
                    new AuditLog(context),
                    new WriteTransactions(context),
                    NullLogger<SetSupplierActiveHandler>.Instance)
                .HandleAsync(new SetSupplierActiveCommand(inactive, version, Active: false), ct);
            Assert.True(result.IsSuccess, result.Error?.ToString());
        }

        var kilogram = await FindProductAsync(db, SampleData.InventorySku);
        var piece = await FindProductAsync(db, SampleData.NegativeStockSku);

        // Compra anulada: entra 1 kg y la anulación lo regresa.
        var voided = await RegisterAsync(active, SampleData.VoidedInvoice, new PurchaseLineInput(kilogram.Id, "1", "38.00"));
        await using (var context = db.CreateDbContext())
        {
            var version = context.Purchases.Single(p => p.Id == voided.PurchaseId).Version;
            var result = await new VoidPurchaseHandler(
                    access,
                    new PurchaseRepository(context),
                    new ProductRepository(context),
                    new InventoryRepository(context),
                    new AuditLog(context),
                    new WriteTransactions(context),
                    db.Clock,
                    db.User,
                    new VoidPurchaseValidator(),
                    NullLogger<VoidPurchaseHandler>.Instance)
                .HandleAsync(new VoidPurchaseCommand(voided.PurchaseId, version, SampleData.PurchaseVoidReason), ct);
            Assert.True(result.IsSuccess, result.Error?.ToString());
        }

        // Compra vigente: 2.5 kg a $40.00 y 1 pieza bonificada ($0.00).
        await RegisterAsync(
            active,
            SampleData.ActiveInvoice,
            new PurchaseLineInput(kilogram.Id, "2.5", "40.00"),
            new PurchaseLineInput(piece.Id, "1", "0.00"));
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

    /// <summary>Desde 0.8.0: una devolución parcial de la venta 1 con una nota de crédito por lo devuelto.</summary>
    public const string ReturnReason = "Producto devuelto";

    /// <summary>Desde 0.9.0: un cliente con crédito, una venta a crédito del cajero y un abono en efectivo.</summary>
    public const int CreditSaleCount = 1;
    public const string CustomerName = "Cliente de muestra Ñandú";
    public const string CustomerTaxId = "RUC-0001";
    public const long CreditLimitCents = 100_000;
    public const long CreditPaymentCents = 4_000;

    /// <summary>Desde 0.9.0: la venta a crédito es de 2 piezas del producto sin inventario ($50.00 c/u).</summary>
    public const long CreditSaleQuantityThousandths = 2_000;
    public const long CreditBalanceCents = 10_000 - CreditPaymentCents;

    /// <summary>Desde 0.10.0: dos ventas con descuento del cajero (la 5 y la 6), un cupón y un borrador con descuento.</summary>
    public const int DiscountSaleCount = 2;
    public const string CouponCode = "MUESTRA10";

    /// <summary>Desde 0.10.0: la venta 6 es de 1 pieza de este producto ($60.00) con el cupón del 10 %.</summary>
    public const string CouponProductSku = "MUE-006";

    /// <summary>Desde 0.10.0: la venta 5 son 2 piezas del producto sin inventario ($100.00) con $15.00 autorizados.</summary>
    public const long LineDiscountCents = 1_500;
    public const long CouponDiscountCents = 600;

    /// <summary>Desde 0.11.0: categoría activa con el producto sin inventario y el del cupón (ambos vendidos).</summary>
    public const string ActiveCategoryName = "Bebidas de muestra";
    public static readonly string[] ActiveCategorySkus = [SaleWithoutInventorySku, CouponProductSku];

    /// <summary>Desde 0.11.0: categoría inactiva con el producto inactivo.</summary>
    public const string InactiveCategoryName = "Lácteos de muestra";
    public const string InactiveCategorySku = "REF-001";

    /// <summary>Desde 0.11.0: categoría borrada sin productos.</summary>
    public const string DeletedCategoryName = "Temporal de muestra";

    /// <summary>Desde 0.12.0: un Corte X (X-000001) y un Corte Z (Z-000001) del turno T-000002; T-000001 sin Corte Z.</summary>
    public const string ReadoutFolio = "X-000001";
    public const string ClosingFolio = "Z-000001";
    public const long ClosingShortageCents = 500;
    public const string ClosingComment = "Faltante de muestra";

    /// <summary>Desde 0.14.0: proveedor activo con las dos compras (una vigente y otra anulada).</summary>
    public const string SupplierName = "Distribuidora de muestra";
    public const string SupplierTaxId = "RUC-PROV-1";
    public const string ActiveInvoice = "FAC-0001";
    public const string VoidedInvoice = "FAC-0002";
    public const string PurchaseVoidReason = "Factura capturada por error";

    /// <summary>Desde 0.14.0: la compra vigente suma 2.5 kg al producto en kilo y 1 pieza bonificada a la pieza en negativo.</summary>
    public const long PurchaseKilogramThousandths = 2_500;
    public const long PurchaseBonusThousandths = 1_000;

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
