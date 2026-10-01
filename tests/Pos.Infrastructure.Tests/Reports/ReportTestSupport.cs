using Pos.Application.Sales;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Domain.Users;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Reports;

/// <summary>Arma usuarios y ventas en fechas y formas de pago concretas para probar los reportes.</summary>
internal static class ReportTestSupport
{
    public static async Task<User> AddUserAsync(TestDb db, string userName, UserRole role = UserRole.Cashier)
    {
        var user = User.Create($"Usuario {userName}", userName, role, "hash");
        await using var context = db.CreateDbContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    /// <summary>Vende una sola línea (precio del producto × cantidad) como <paramref name="user"/> en el instante UTC indicado.</summary>
    public static async Task<ConfirmedSale> SellAsync(
        TestDb db,
        User user,
        DateTime utc,
        Product product,
        long quantityThousandths = 1_000,
        PaymentMethod method = PaymentMethod.Cash)
    {
        db.User.UserId = user.Id;
        db.Clock.UtcNow = utc;

        var amount = SaleMath.LineAmount(Quantity.FromThousandths(quantityThousandths), product.Price).Cents;
        var payment = method == PaymentMethod.Cash
            ? new PaymentInput(PaymentMethod.Cash, 0, amount, null)
            : new PaymentInput(method, amount, null, "REF");
        var command = new ConfirmSaleCommand(
            Guid.CreateVersion7(),
            [new ConfirmLineInput(product.Id, quantityThousandths, product.Price.Cents)],
            [payment]);

        var result = await SalesTestSupport.SellAsync(db, command);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    public static Task<Product> SeedProductAsync(TestDb db, string sku = "REP-001", long priceCents = 1_000) =>
        SalesTestSupport.SeedProductAsync(db, sku, tracks: false, priceCents: priceCents);
}
