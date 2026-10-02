using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Reports;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Users;

namespace Pos.Application.Inventory.CheckStockAlerts;

/// <summary>
/// Revisión periódica de alertas de existencia (022, research §6): evalúa el nivel de cada producto,
/// decide qué notificar al usuario de la sesión como máximo una vez al día por producto y nivel, registra
/// lo notificado y purga los registros de más de 7 días, todo en una transacción.
/// </summary>
public sealed class CheckStockAlertsHandler
{
    /// <summary>Días que se conservan los registros de notificación.</summary>
    public const int RetentionDays = 7;

    private static readonly IReadOnlySet<StockAlertLevel> NoLevels = new HashSet<StockAlertLevel>();

    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly IStockAlertStore _store;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ReportPeriodResolver _resolver;

    public CheckStockAlertsHandler(
        IAccessControl access,
        ICurrentUser currentUser,
        IStockAlertStore store,
        IWriteTransactions transactions,
        IClock clock,
        ReportPeriodResolver resolver)
    {
        _access = access;
        _currentUser = currentUser;
        _store = store;
        _transactions = transactions;
        _clock = clock;
        _resolver = resolver;
    }

    public async Task<Result<StockAlertCheck>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewInventory, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<StockAlertCheck>(access.Error!);
        }

        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
        {
            return Result.Failure<StockAlertCheck>(new Forbidden(Permission.ViewInventory, CanBeAuthorized: false));
        }

        var utcNow = _clock.UtcNow;
        var today = _resolver.ToLocalDate(utcNow);

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var levels = (await _store.GetCandidatesAsync(cancellationToken))
            .Select(c => (c.ProductId, Level: StockAlertRule.Evaluate(
                StockLevel.FromThousandths(c.OnHandThousandths),
                c.MinimumThousandths is { } m ? Quantity.FromThousandths(m) : null,
                c.ReorderPointThousandths is { } r ? Quantity.FromThousandths(r) : null)))
            .Where(c => c.Level != StockAlertLevel.None)
            .ToList();

        var acknowledged = (await _store.GetAcknowledgedAsync(userId, today, cancellationToken))
            .GroupBy(a => a.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<StockAlertLevel>)g.Select(a => a.Level).ToHashSet());
        IReadOnlySet<StockAlertLevel> AcknowledgedFor(Guid productId) =>
            acknowledged.TryGetValue(productId, out var set) ? set : NoLevels;

        var notifyUrgent = levels.Any(c => c.Level == StockAlertLevel.Urgent && StockAlertDedup.IsPending(c.Level, AcknowledgedFor(c.ProductId)));
        var notifyAlert = levels.Any(c => c.Level == StockAlertLevel.Alert && StockAlertDedup.IsPending(c.Level, AcknowledgedFor(c.ProductId)));

        // Mostrar cuenta como notificado (research §4): se registran todos los productos del nivel que se
        // muestra y que aún no tienen ese registro hoy.
        foreach (var (productId, level) in levels)
        {
            var show = level == StockAlertLevel.Urgent ? notifyUrgent : notifyAlert;
            if (show && !AcknowledgedFor(productId).Contains(level))
            {
                _store.Add(StockAlertAcknowledgement.Create(userId, productId, level, today, utcNow));
            }
        }

        await _store.PurgeBeforeAsync(today.AddDays(-RetentionDays), cancellationToken);
        var urgentCount = levels.Count(c => c.Level == StockAlertLevel.Urgent);
        var alertCount = levels.Count(c => c.Level == StockAlertLevel.Alert);

        // Otra revisión simultánea ya registró (y notificó): se revierte todo, purga incluida.
        if ((await _store.SaveChangesAsync(cancellationToken)).Status != SaveStatus.Saved)
        {
            return Result.Success(new StockAlertCheck(urgentCount, alertCount, NotifyUrgent: false, NotifyAlert: false));
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(new StockAlertCheck(urgentCount, alertCount, notifyUrgent, notifyAlert));
    }
}
