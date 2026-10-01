using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Returns.PreviewReturn;

/// <summary>
/// Calcula el monto y el reparto de una devolución con <c>ReturnMath</c>; la interfaz no calcula
/// (Principio III). Solo lee: no consume concesiones ni guarda nada.
/// </summary>
public sealed class PreviewReturnHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ISaleRepository _sales;
    private readonly IReturnRepository _returns;
    private readonly IReturnsSettingsStore _settings;
    private readonly ReturnCashGate _cashGate;
    private readonly IClock _clock;

    public PreviewReturnHandler(
        IAccessControl access,
        ICurrentUser currentUser,
        ISaleRepository sales,
        IReturnRepository returns,
        IReturnsSettingsStore settings,
        ReturnCashGate cashGate,
        IClock clock)
    {
        _access = access;
        _currentUser = currentUser;
        _sales = sales;
        _returns = returns;
        _settings = settings;
        _cashGate = cashGate;
        _clock = clock;
    }

    public async Task<Result<ReturnPreview>> HandleAsync(PreviewReturnCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ProcessReturns, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ReturnPreview>(access.Error!);
        }

        var sale = await _sales.GetAsync(command.SaleId, cancellationToken);
        if (sale is null)
        {
            return Result.Failure<ReturnPreview>(new NotFound());
        }

        if (await SaleAccess.CheckOwnershipAsync(_access, _currentUser, sale.CreatedBy, cancellationToken) is { } forbidden)
        {
            return Result.Failure<ReturnPreview>(forbidden);
        }

        var windowDays = _settings.Load().ReturnWindowDays;
        var withinWindow = _clock.UtcNow - sale.CreatedAt <= TimeSpan.FromDays(windowDays);

        ReturnPlan plan;
        try
        {
            plan = ReturnPlanner.Build(
                sale,
                command.Lines ?? ReturnPlanner.AllAvailable(sale),
                await _returns.GetReturnedByPaymentAsync(sale.Id, cancellationToken));
        }
        catch (DomainException)
        {
            return Result.Failure<ReturnPreview>(new NothingToReturn());
        }

        var cash = await _cashGate.ResolveAsync(plan.CashCents, cancellationToken);
        return Result.Success(new ReturnPreview(
            plan.TotalCents,
            [.. plan.Lines.Select(l => new ReturnPreviewLine(l.SaleLineId, l.QuantityThousandths, l.AmountCents))],
            [.. plan.Shares.Select(s => new RefundBreakdownItem(s.Payment.Method, s.AmountCents))],
            plan.CashCents,
            withinWindow,
            cash.IsSuccess,
            windowDays));
    }
}
