using Pos.Application.Abstractions;
using Pos.Application.Printing;
using Pos.Application.Printing.GetPrintingSettings;
using Pos.Application.Printing.OpenCashDrawer;
using Pos.Application.Printing.PrintTicket;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Shell;
using Pos.Desktop.Sales;
using Serilog;

namespace Pos.Desktop.Settings;

/// <summary>Resultado de abrir el cajón sin venta: el error de motivo deja abierto el diálogo.</summary>
public sealed record DrawerAttempt(string? ReasonError, string? Message, bool Succeeded, bool CanAuthorize = false);

/// <summary>
/// Orquesta la impresión y el cajón desde la interfaz (006): tras el cobro los trabajos van a una cola
/// serial fuera del flujo de la venta; una falla nunca toca la venta, solo avisa con la opción de
/// reintentar. Solo presenta: las reglas viven en los casos de uso.
/// </summary>
public sealed class TicketPrintingService : IDisposable
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly ISessionNavigation _navigation;
    private readonly ILogger _logger;
    private readonly PrintJobQueue _queue;

    private bool _notConfiguredShown;

    public TicketPrintingService(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        ISessionNavigation navigation,
        ILogger logger)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _navigation = navigation;
        _logger = logger;
        _queue = new PrintJobQueue(ex => logger.Fatal(ex, "Error inesperado al atender un trabajo de impresión o cajón"));
    }

    /// <summary>
    /// Se invoca tras confirmar la venta; regresa de inmediato. Abre el cajón si hubo efectivo y la
    /// opción está activa, e imprime si la impresión automática está activa.
    /// </summary>
    public void OnSaleRegistered(Guid saleId, string folio, bool hadCash) =>
        _queue.Enqueue(async () =>
        {
            var settings = await LoadSettingsAsync();
            if (settings is null)
            {
                return;
            }

            if (hadCash && settings.AutoOpenDrawer)
            {
                await OpenDrawerForSaleAsync(saleId);
            }

            if (settings.AutoPrint)
            {
                await PrintAsync(PrintSource.Sale(saleId), isReprint: false, folio, automatic: true);
            }
        });

    /// <summary>
    /// Imprime y, ante una falla, avisa con Reintentar o Continuar sin imprimir. Devuelve el destino
    /// (impresora o ruta del archivo) o nulo si no se imprimió. Con <paramref name="automatic"/> un
    /// éxito no muestra aviso y la impresora sin configurar se avisa una sola vez por sesión.
    /// </summary>
    public async Task<string?> PrintAsync(PrintSource source, bool isReprint, string label, bool automatic)
    {
        while (true)
        {
            var (completed, result) = await _runner.RunAsync(
                "ImprimirTicket",
                () => _useCases.RunAsync<PrintTicketHandler, Result<PrintedTicket>>(
                    h => h.HandleAsync(new PrintTicketCommand(source, isReprint), CancellationToken.None)),
                new Dictionary<string, object?> { ["Folio"] = label, ["Reprint"] = isReprint });

            if (!completed || result is null)
            {
                return null;
            }

            switch (result.Error)
            {
                case null:
                    if (!automatic)
                    {
                        await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Print_Sent, result.Value.Destination));
                    }

                    return result.Value.Destination;

                case NotConfigured:
                    await OfferSettingsAsync(automatic);
                    return null;

                case NotFound:
                    await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Editor_NotFound);
                    return null;

                default:
                    var message = result.Error is PrinterUnavailable ? Strings.Print_Unavailable : Strings.Print_Failed;
                    var retry = await _dialogs.AskAsync(
                        Strings.Print_FailedTitle,
                        string.Format(System.Globalization.CultureInfo.CurrentCulture, message, label),
                        Strings.Print_Retry,
                        Strings.Print_Continue);
                    if (!retry)
                    {
                        return null;
                    }

                    break;
            }
        }
    }

    /// <summary>Abre el cajón por un cobro en efectivo; una falla avisa sin afectar la venta.</summary>
    public async Task OpenDrawerForSaleAsync(Guid saleId)
    {
        var (completed, result) = await _runner.RunAsync(
            "AbrirCajonPorVenta",
            () => _useCases.RunAsync<OpenCashDrawerHandler, Result>(
                h => h.HandleAsync(OpenCashDrawerCommand.ForSale(saleId), CancellationToken.None)),
            new Dictionary<string, object?> { ["SaleId"] = saleId });

        if (!completed || result is null)
        {
            return;
        }

        switch (result.Error)
        {
            case null:
                break;

            case NotConfigured:
                // Sin impresora no hay cajón que abrir; el aviso de configuración lo da la impresión.
                _logger.Information("Cajón no abierto: no hay impresora configurada. SaleId={SaleId}", saleId);
                break;

            default:
                await _dialogs.ShowMessageAsync(Strings.Drawer_FailedTitle, Strings.Drawer_FailedSale);
                break;
        }
    }

    /// <summary>Apertura sin venta con motivo obligatorio; queda en la bitácora aun si falla.</summary>
    public async Task<DrawerAttempt> OpenDrawerManualAsync(string reason, Guid? authorizationGrantId = null)
    {
        var (completed, result) = await _runner.RunAsync(
            "AbrirCajonManual",
            () => _useCases.RunAsync<OpenCashDrawerHandler, Result>(
                h => h.HandleAsync(OpenCashDrawerCommand.Manual(reason, authorizationGrantId), CancellationToken.None)),
            new Dictionary<string, object?> { ["Authorized"] = authorizationGrantId is not null });

        if (!completed || result is null)
        {
            return new DrawerAttempt(null, null, false);
        }

        return result.Error switch
        {
            null => new DrawerAttempt(null, Strings.Drawer_Opened, true),
            ValidationFailed validation => new DrawerAttempt(validation.Errors is [{ } first, ..] ? first.Message : Strings.Drawer_ReasonRequired, null, false),
            NotConfigured => new DrawerAttempt(null, Strings.Drawer_NotConfigured, false),
            Forbidden { CanBeAuthorized: true } => new DrawerAttempt(null, null, false, CanAuthorize: true),
            Forbidden => new DrawerAttempt(null, Strings.Common_Forbidden, false),
            _ => new DrawerAttempt(null, Strings.Drawer_Failed, false),
        };
    }

    public void Dispose() => _queue.Dispose();

    private async Task<PrintingSettings?> LoadSettingsAsync()
    {
        var (completed, settings) = await _runner.RunAsync(
            "CargarConfiguracionImpresion",
            () => _useCases.RunAsync<GetPrintingSettingsHandler, PrintingSettings>(h => Task.FromResult(h.Handle())));
        return completed ? settings : null;
    }

    private async Task OfferSettingsAsync(bool automatic)
    {
        if (automatic)
        {
            if (_notConfiguredShown)
            {
                return;
            }

            _notConfiguredShown = true;
        }

        var open = await _dialogs.AskAsync(
            Strings.Print_NotConfiguredTitle,
            Strings.Print_NotConfigured,
            Strings.Print_OpenSettings,
            Strings.Print_Continue);
        if (open)
        {
            await _navigation.NavigateAsync(SettingsModule.PrinterPageId);
        }
    }
}
