using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Discounts.Coupons.SetCouponActive;

/// <summary>
/// Activa o desactiva un cupón (<c>ManageDiscounts</c>); no se borran (FR-010). Desactivar registra
/// <c>COUPON_DEACTIVATED</c> y reactivar, <c>COUPON_UPDATED</c> (FR-019).
/// </summary>
public sealed partial class SetCouponActiveHandler
{
    private readonly IAccessControl _access;
    private readonly ICouponRepository _coupons;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly ILogger<SetCouponActiveHandler> _logger;

    public SetCouponActiveHandler(
        IAccessControl access,
        ICouponRepository coupons,
        IAuditLog audit,
        IWriteTransactions transactions,
        ILogger<SetCouponActiveHandler> logger)
    {
        _access = access;
        _coupons = coupons;
        _audit = audit;
        _transactions = transactions;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(SetCouponActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageDiscounts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var coupon = await _coupons.GetAsync(command.Id, cancellationToken);
        if (coupon is null)
        {
            return Result.Failure(new NotFound());
        }

        if (coupon.Version != command.ExpectedVersion)
        {
            return Result.Failure(new Conflict());
        }

        if (command.Active)
        {
            coupon.Activate();
            _audit.Add(AuditActions.CouponUpdated, AuditActions.CouponEntity, coupon.Id, $"Cupón {coupon.Code} activado");
        }
        else
        {
            coupon.Deactivate();
            _audit.Add(AuditActions.CouponDeactivated, AuditActions.CouponEntity, coupon.Id, $"Cupón {coupon.Code} desactivado");
        }

        var outcome = await _coupons.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogChanged(coupon.Id, coupon.Code, command.Active);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cupón activado o desactivado. CouponId={CouponId} Codigo={Code} Activo={Active}")]
    private partial void LogChanged(Guid couponId, string code, bool active);
}
