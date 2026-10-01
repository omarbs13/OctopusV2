using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Returns;
using Pos.Domain.Users;

namespace Pos.Application.Returns.MarkReversalDone;

/// <summary>
/// Marca como reversado un reintegro de tarjeta o transferencia (FR-018). Es la única mutación
/// permitida de un registro de devolución: una sola vez y con entrada en la bitácora.
/// </summary>
public sealed partial class MarkReversalDoneHandler
{
    private readonly IAccessControl _access;
    private readonly IReturnRepository _returns;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<MarkReversalDoneHandler> _logger;

    public MarkReversalDoneHandler(
        IAccessControl access,
        IReturnRepository returns,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ICurrentUser currentUser,
        ILogger<MarkReversalDoneHandler> logger)
    {
        _access = access;
        _returns = returns;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(Guid refundId, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageCreditNotes, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var refund = await _returns.GetRefundAsync(refundId, cancellationToken);
        if (refund is null)
        {
            return Result.Failure(new NotFound());
        }

        if (refund.Status != RefundStatus.PendingReversal)
        {
            return Result.Failure(new InvalidState(ReturnMessages.AlreadyReversed));
        }

        var description = await _returns.DescribeRefundAsync(refundId, cancellationToken);
        refund.MarkReversed(_currentUser.UserId, _clock.UtcNow);
        _audit.Add(
            AuditActions.CardReversalDone,
            AuditActions.SaleEntity,
            description?.SaleId ?? refund.SaleReturnId,
            $"Devolución {description?.ReturnFolio}, venta {description?.SaleFolio}. Forma de pago: {refund.Method}");

        var outcome = await _returns.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogReversed(refundId, _currentUser.UserId);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reversa de tarjeta marcada como realizada. RefundId={RefundId} UserId={UserId}")]
    private partial void LogReversed(Guid refundId, Guid userId);
}
