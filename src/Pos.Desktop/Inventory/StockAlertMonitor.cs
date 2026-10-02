using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Inventory.CheckStockAlerts;
using Pos.Desktop.Common;
using Pos.Desktop.Reports;
using Pos.Desktop.Resources;
using Pos.Desktop.Shell;
using Pos.Domain.Users;
using Serilog;

namespace Pos.Desktop.Inventory;

/// <summary>
/// Revisa las alertas de existencia al iniciar sesión y cada hora (022, research §9). Es de la sesión:
/// se detiene al desecharla. Las revisiones no se solapan y una falla solo se registra en el log (FR-015).
/// </summary>
public sealed class StockAlertMonitor : IDisposable
{
    public const string OperationName = "RevisarAlertasDeExistencia";
    public const string UrgentKey = "stock.urgent";
    public const string AlertKey = "stock.alert";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly NotificationCenter _notifications;
    private readonly ICurrentPermissions _permissions;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger _logger;

    private DispatcherTimer? _timer;
    private bool _checking;

    public StockAlertMonitor(
        UseCases useCases,
        OperationRunner runner,
        NotificationCenter notifications,
        ICurrentPermissions permissions,
        ICurrentUser currentUser,
        ILogger logger)
    {
        _useCases = useCases;
        _runner = runner;
        _notifications = notifications;
        _permissions = permissions;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>Revisa de inmediato y luego cada hora; sin permiso de ver inventario no hace nada (FR-013).</summary>
    public void Start()
    {
        if (_timer is not null || !_permissions.Has(Permission.ViewInventory))
        {
            return;
        }

        _timer = new DispatcherTimer(CheckInterval, DispatcherPriority.Background, (_, _) => _ = CheckAsync());
        _timer.Start();
        _ = CheckAsync();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer = null;
    }

    private async Task CheckAsync()
    {
        if (_checking)
        {
            return;
        }

        _checking = true;
        try
        {
            var watch = Stopwatch.StartNew();
            var (completed, result) = await _runner.RunQuietlyResultAsync(
                OperationName,
                () => _useCases.RunAsync<CheckStockAlertsHandler, Result<StockAlertCheck>>(h => h.HandleAsync(CancellationToken.None)));
            watch.Stop();
            if (!completed || result is not { IsSuccess: true } || _timer is null)
            {
                return;
            }

            var check = result.Value;
            _logger
                .ForContext("Operation", OperationName)
                .ForContext("UserId", _currentUser.UserId)
                .Information(
                    "Revisión de alertas de existencia: {UrgentCount} urgentes, {AlertCount} en alerta; notificó urgente {NotifyUrgent}, alerta {NotifyAlert} en {ElapsedMs} ms",
                    check.UrgentCount,
                    check.AlertCount,
                    check.NotifyUrgent,
                    check.NotifyAlert,
                    watch.ElapsedMilliseconds);
            Notify(check);
        }
        finally
        {
            _checking = false;
        }
    }

    private void Notify(StockAlertCheck check)
    {
        if (check.NotifyUrgent)
        {
            _notifications.Show(new NotificationItem(
                NotificationSeverity.Danger,
                Strings.Notify_UrgentTitle,
                string.Format(CultureInfo.CurrentCulture, Strings.Notify_UrgentMessage, check.UrgentCount),
                ReportsModule.InventoryPageId,
                StockFilter.Urgent,
                UrgentKey));
        }

        if (check.NotifyAlert)
        {
            _notifications.Show(new NotificationItem(
                NotificationSeverity.Warning,
                Strings.Notify_AlertTitle,
                string.Format(CultureInfo.CurrentCulture, Strings.Notify_AlertMessage, check.AlertCount),
                ReportsModule.InventoryPageId,
                StockFilter.Alert,
                AlertKey));
        }
    }
}
