using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Discounts.ApproveDiscount;
using Pos.Domain.Discounts;
using Pos.Domain.Users;

namespace Pos.Infrastructure.Tests.Discounts;

/// <summary>015, research §7: la aprobación se guarda al aplicar, con la concesión de 2 minutos de 007.</summary>
public sealed class ApproveDiscountTests : DiscountTestBase
{
    private static ApproveDiscountCommand Command(Guid draftId, Guid productId, long amountCents, Guid? grant) =>
        new(draftId, DiscountScope.Line, productId, DiscountMode.Amount, amountCents, 10_000, grant);

    [Fact]
    public async Task DentroDelLimite_NoRequiereAprobacionYNoGuardaNada()
    {
        var result = await Discounts.ApproveAsync(Command(Guid.CreateVersion7(), Guid.CreateVersion7(), 1_000, null));

        Assert.IsType<ApprovalNotNeeded>(result.Error);
        Assert.Equal(0, await ApprovalCountAsync());
    }

    [Fact]
    public async Task CajeroConConcesionVencida_EsRechazadoYNoGuardaNada()
    {
        var grant = Discounts.Grant();
        Db.Clock.Advance(TimeSpan.FromMinutes(3));

        var result = await Discounts.ApproveAsync(Command(Guid.CreateVersion7(), Guid.CreateVersion7(), 1_500, grant));

        var forbidden = Assert.IsType<Forbidden>(result.Error);
        Assert.Equal(Permission.ApproveDiscounts, forbidden.Permission);
        Assert.True(forbidden.CanBeAuthorized);
        Assert.Equal(0, await ApprovalCountAsync());
    }

    [Fact]
    public async Task CajeroConConcesionValida_GuardaLaAprobacionYLaBitacoraYConsumeLaConcesion()
    {
        var draftId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        var grant = Discounts.Grant();

        var result = await Discounts.ApproveAsync(Command(draftId, productId, 1_500, grant));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(Users.Admin.Id, result.Value.AuthorizedBy);
        await using (var context = Db.CreateDbContext())
        {
            var approval = await context.DiscountApprovals.SingleAsync(Ct);
            Assert.Equal(draftId, approval.DraftId);
            Assert.Equal(Users.Cashier.Id, approval.RequestedBy);
            Assert.Equal(productId, approval.ProductId);
            Assert.Equal(1_500, approval.ApprovedBasisPoints);
            var entry = await context.AuditEntries.SingleAsync(e => e.Action == AuditActions.DiscountAuthorized, Ct);
            Assert.Equal(Users.Admin.Id, entry.AuthorizedBy);
        }

        // La concesión es de un solo uso.
        var again = await Discounts.ApproveAsync(Command(draftId, productId, 1_500, grant));
        Assert.IsType<Forbidden>(again.Error);
    }
}
