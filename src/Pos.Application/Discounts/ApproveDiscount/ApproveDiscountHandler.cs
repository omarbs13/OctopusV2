using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Discounts;
using Pos.Domain.Users;

namespace Pos.Application.Discounts.ApproveDiscount;

/// <summary>
/// Guarda la aprobación de un descuento sobre el límite (015, research §7): permiso <c>ApplyDiscounts</c>,
/// equivalente con <see cref="DiscountMath"/>, y <c>ApproveDiscounts</c> (Administrador por rol o Cajero con
/// concesión, que se consume aquí). La aprobación y <c>DISCOUNT_AUTHORIZED</c> van en una transacción. No
/// modifica la venta: el <c>ApprovalId</c> lo lleva el <c>Cart</c> y el borrador.
/// </summary>
public sealed partial class ApproveDiscountHandler
{
    private readonly IAccessControl _access;
    private readonly IDiscountSettingsStore _settings;
    private readonly IDiscountApprovalStore _approvals;
    private readonly IUserRepository _users;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IValidator<ApproveDiscountCommand> _validator;
    private readonly ILogger<ApproveDiscountHandler> _logger;

    public ApproveDiscountHandler(
        IAccessControl access,
        IDiscountSettingsStore settings,
        IDiscountApprovalStore approvals,
        IUserRepository users,
        IAuditLog audit,
        IWriteTransactions transactions,
        ICurrentUser currentUser,
        IClock clock,
        IValidator<ApproveDiscountCommand> validator,
        ILogger<ApproveDiscountHandler> logger)
    {
        _access = access;
        _settings = settings;
        _approvals = approvals;
        _users = users;
        _audit = audit;
        _transactions = transactions;
        _currentUser = currentUser;
        _clock = clock;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<DiscountApprovalDto>> HandleAsync(ApproveDiscountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var apply = await _access.CheckAsync(Permission.ApplyDiscounts, cancellationToken);
        if (!apply.Allowed)
        {
            return Result.Failure<DiscountApprovalDto>(apply.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<DiscountApprovalDto>(ProductRules.ToError(validation));
        }

        DiscountValue value;
        long amount;
        try
        {
            value = DiscountValue.Create(command.Mode, command.Value);
            amount = DiscountMath.Amount(command.BaseCents, value);
        }
        catch (DomainException ex)
        {
            return Result.Failure<DiscountApprovalDto>(new ValidationFailed([new FieldError(DiscountFields.Value, ex.Message)]));
        }

        var limit = _settings.Load().LimitBasisPoints;
        if (!DiscountMath.ExceedsLimit(amount, command.BaseCents, limit))
        {
            return Result.Failure<DiscountApprovalDto>(new ApprovalNotNeeded());
        }

        // El Administrador tiene el permiso por rol y queda como autorizador; un Cajero consume la concesión.
        var approval = await _access.CheckAsync(Permission.ApproveDiscounts, command.GrantId, cancellationToken);
        if (!approval.Allowed)
        {
            LogDenied(command.DraftId, _currentUser.UserId, command.Scope, amount);
            return Result.Failure<DiscountApprovalDto>(approval.Error!);
        }

        var authorizedBy = approval.AuthorizedBy ?? _currentUser.UserId;
        var equivalent = DiscountMath.EquivalentBasisPoints(amount, command.BaseCents);
        var description = DiscountTexts.Describe(command.Scope, value, amount, command.BaseCents);

        await using var transaction = await _transactions.BeginAsync(cancellationToken);
        var record = DiscountApproval.Create(
            command.DraftId,
            _currentUser.UserId,
            authorizedBy,
            command.Scope,
            command.Scope == DiscountScope.Line ? command.ProductId : null,
            equivalent,
            _clock.UtcNow);
        _approvals.Add(record);
        _audit.Add(
            AuditActions.DiscountAuthorized,
            AuditActions.DiscountApprovalEntity,
            record.Id,
            $"{description}. Equivalente {DiscountValue.FormatPercent(equivalent)}. Límite {DiscountValue.FormatPercent(limit)}. Venta en curso {command.DraftId}",
            authorizedBy);
        await _approvals.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var authorizer = await _users.GetAsync(authorizedBy, cancellationToken);
        LogApproved(record.Id, command.DraftId, _currentUser.UserId, authorizedBy, command.Scope, amount, equivalent);
        return Result.Success(new DiscountApprovalDto(record.Id, authorizedBy, authorizer?.FullName ?? string.Empty));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Descuento autorizado. ApprovalId={ApprovalId} DraftId={DraftId} UserId={UserId} AutorizadoPor={AuthorizedBy} Alcance={Scope} MontoCents={AmountCents} EquivalentePb={Equivalent}")]
    private partial void LogApproved(Guid approvalId, Guid draftId, Guid userId, Guid authorizedBy, DiscountScope scope, long amountCents, long equivalent);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Descuento sin autorización válida. DraftId={DraftId} UserId={UserId} Alcance={Scope} MontoCents={AmountCents}")]
    private partial void LogDenied(Guid draftId, Guid userId, DiscountScope scope, long amountCents);
}
