using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Discounts;
using Pos.Domain.Users;

namespace Pos.Application.Discounts.Coupons.SaveCoupon;

/// <summary>
/// Alta o edición de un cupón (015, FR-009, FR-010): licencia y <c>ManageDiscounts</c> → forma → el código no
/// puede coincidir con un producto → reglas del <see cref="Coupon"/> → guardado y bitácora en una transacción.
/// </summary>
public sealed partial class SaveCouponHandler
{
    private readonly IAccessControl _access;
    private readonly ICouponRepository _coupons;
    private readonly IProductRepository _products;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<SaveCouponCommand> _validator;
    private readonly ILogger<SaveCouponHandler> _logger;

    public SaveCouponHandler(
        IAccessControl access,
        ICouponRepository coupons,
        IProductRepository products,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<SaveCouponCommand> validator,
        ILogger<SaveCouponHandler> logger)
    {
        _access = access;
        _coupons = coupons;
        _products = products;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(SaveCouponCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageDiscounts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<Guid>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(ProductRules.ToError(validation));
        }

        DiscountValue value;
        try
        {
            value = DiscountValue.Parse(command.Mode, command.ValueText);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(new ValidationFailed([new FieldError(DiscountFields.Value, ex.Message)]));
        }

        var code = Coupon.NormalizeCode(command.Code);

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        Coupon coupon;
        string action;
        if (command.Id is { } id)
        {
            var existing = await _coupons.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<Guid>(new NotFound());
            }

            coupon = existing;

            if (coupon.Version != command.ExpectedVersion)
            {
                return Result.Failure<Guid>(new Conflict());
            }

            if (coupon.UsesCount > 0 && (coupon.Code != code || coupon.Discount != value))
            {
                return Result.Failure<Guid>(new CouponHasUses());
            }

            if (coupon.Code != code && await _products.ExistsWithCodeAsync(code, cancellationToken))
            {
                return Result.Failure<Guid>(new CodeCollidesWithProduct());
            }

            try
            {
                coupon.Edit(code, value);
                coupon.Update(command.StartsOn, command.EndsOn, command.UsageLimit);
            }
            catch (DomainException ex)
            {
                return Result.Failure<Guid>(new ValidationFailed([new FieldError(DiscountFields.UsageLimit, ex.Message)]));
            }

            action = AuditActions.CouponUpdated;
        }
        else
        {
            if (await _products.ExistsWithCodeAsync(code, cancellationToken))
            {
                return Result.Failure<Guid>(new CodeCollidesWithProduct());
            }

            coupon = Coupon.Create(code, value, command.StartsOn, command.EndsOn, command.UsageLimit);
            _coupons.Add(coupon);
            action = AuditActions.CouponCreated;
        }

        _audit.Add(action, AuditActions.CouponEntity, coupon.Id, Describe(coupon));

        var outcome = await _coupons.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure<Guid>(outcome.Status == SaveStatus.Duplicate ? new Duplicate(DiscountFields.Code) : new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogSaved(coupon.Id, coupon.Code, action);
        return Result.Success(coupon.Id);
    }

    internal static string Describe(Coupon coupon)
    {
        var limit = coupon.UsageLimit is { } max ? $"{max} usos" : "sin límite de usos";
        return $"Cupón {coupon.Code}: {coupon.Discount}, del {DiscountMessages.FormatDate(coupon.StartsOn)} al {DiscountMessages.FormatDate(coupon.EndsOn)}, {limit}";
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cupón guardado. CouponId={CouponId} Codigo={Code} Accion={Action}")]
    private partial void LogSaved(Guid couponId, string code, string action);
}
