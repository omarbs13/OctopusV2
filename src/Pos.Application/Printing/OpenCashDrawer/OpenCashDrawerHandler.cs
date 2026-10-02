using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Users.Access;
using Pos.Application.Products;
using Pos.Domain.Users;

namespace Pos.Application.Printing.OpenCashDrawer;

/// <summary>
/// Abre el cajón. La apertura sin venta exige motivo y escribe <c>DRAWER_OPENED</c> en la bitácora
/// tanto si abre como si falla (Principio IX). Una falla del dispositivo nunca lanza.
/// </summary>
public sealed partial class OpenCashDrawerHandler
{
    public const string AuditAction = AuditActions.DrawerOpened;
    public const string AuditEntityType = AuditActions.CashDrawerEntity;

    private readonly IAccessControl _access;
    private readonly ICashDrawer _drawer;
    private readonly IPrintingSettingsStore _settings;
    private readonly IAuditLog _audit;
    private readonly IValidator<OpenCashDrawerCommand> _validator;
    private readonly ILogger<OpenCashDrawerHandler> _logger;

    public OpenCashDrawerHandler(
        IAccessControl access,
        ICashDrawer drawer,
        IPrintingSettingsStore settings,
        IAuditLog audit,
        IValidator<OpenCashDrawerCommand> validator,
        ILogger<OpenCashDrawerHandler> logger)
    {
        _access = access;
        _drawer = drawer;
        _settings = settings;
        _audit = audit;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(OpenCashDrawerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // La apertura al cobrar pertenece a la venta; la apertura sin venta se puede autorizar (FR-013).
        var access = command.IsManual
            ? await _access.CheckAsync(Permission.OpenDrawerWithoutSale, command.AuthorizationGrantId, cancellationToken)
            : await _access.CheckAsync(Permission.Sell, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        if (command.IsManual)
        {
            var validation = await _validator.ValidateAsync(command, cancellationToken);
            if (!validation.IsValid)
            {
                return Result.Failure(ProductRules.ToError(validation));
            }
        }

        var settings = _settings.Load();
        var outcome = await OpenAsync(settings, command, cancellationToken);

        if (command.IsManual)
        {
            var reason = command.Reason!.Trim();
            _audit.Add(new AuditRecord(
                AuditAction,
                AuditEntityType,
                Guid.CreateVersion7(),
                Details: $"Resultado: {(outcome.Succeeded ? "OK" : "FALLO")}",
                Reason: reason,
                AuthorizedBy: access.AuthorizedBy));
            await _audit.SaveAsync(cancellationToken);
        }

        if (outcome.Succeeded)
        {
            return Result.Success();
        }

        return Result.Failure(outcome.Failure == DeviceFailure.NotConfigured ? new NotConfigured() : new DrawerFailed());
    }

    private async Task<DrawerOutcome> OpenAsync(PrintingSettings settings, OpenCashDrawerCommand command, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
        {
            return DrawerOutcome.Fail(DeviceFailure.NotConfigured);
        }

        try
        {
            var outcome = await _drawer.OpenAsync(settings, cancellationToken);
            if (!outcome.Succeeded)
            {
                LogFailed(command.SaleId, settings.PrinterName, outcome.Failure);
            }

            return outcome;
        }
#pragma warning disable CA1031 // Una falla de dispositivo nunca debe afectar la venta ni cerrar la aplicación.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogException(ex, command.SaleId, settings.PrinterName);
            return DrawerOutcome.Fail(DeviceFailure.IoError);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo abrir el cajón. SaleId={SaleId} Impresora={Printer} Motivo={Failure}")]
    private partial void LogFailed(Guid? saleId, string? printer, DeviceFailure? failure);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falla inesperada al abrir el cajón. SaleId={SaleId} Impresora={Printer}")]
    private partial void LogException(Exception exception, Guid? saleId, string? printer);
}
