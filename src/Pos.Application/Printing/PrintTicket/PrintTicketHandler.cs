using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.CashShifts;
using Pos.Application.Printing.Ticket;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Printing.PrintTicket;

/// <summary>
/// Imprime el ticket de una venta o el de prueba. Solo lee: no escribe en la base ni consume folio.
/// Una falla del dispositivo se registra y se devuelve como error de negocio; nunca lanza (006, FR-009).
/// </summary>
public sealed partial class PrintTicketHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ISaleRepository _sales;
    private readonly ICashShiftRepository _shifts;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IPrintingSettingsStore _settings;
    private readonly ITicketPrinter _printer;
    private readonly ILogger<PrintTicketHandler> _logger;

    public PrintTicketHandler(
        IAccessControl access,
        ICurrentUser currentUser,
        ISaleRepository sales,
        ICashShiftRepository shifts,
        IBusinessProfileRepository profiles,
        IPrintingSettingsStore settings,
        ITicketPrinter printer,
        ILogger<PrintTicketHandler> logger)
    {
        _access = access;
        _currentUser = currentUser;
        _sales = sales;
        _shifts = shifts;
        _profiles = profiles;
        _settings = settings;
        _printer = printer;
        _logger = logger;
    }

    public async Task<Result<PrintedTicket>> HandleAsync(PrintTicketCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // El ticket de una venta es de quien la hizo (o de quien ve todas); el de prueba es de configuración;
        // el corte y los comprobantes de efectivo son del turno (008).
        var access = await _access.CheckAsync(
            command.Source switch
            {
                PrintSource.SaleSource => Permission.ViewOwnSales,
                PrintSource.ShiftReportSource or PrintSource.CashMovementSource => Permission.OperateShift,
                _ => Permission.ManageSettings,
            },
            cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<PrintedTicket>(access.Error!);
        }

        var settings = _settings.Load();
        var folio = TicketBuilder.SampleFolio;
        try
        {
            var profileEntity = await _profiles.GetAsync(cancellationToken);
            var profile = profileEntity is null ? null : BusinessProfileDto.From(profileEntity);

            TicketDocument ticket;
            if (command.Source is PrintSource.SaleSource source)
            {
                var sale = await _sales.GetDetailAsync(source.SaleId, cancellationToken);
                if (sale is null)
                {
                    return Result.Failure<PrintedTicket>(new NotFound());
                }

                if (await SaleAccess.CheckOwnershipAsync(_access, _currentUser, sale.CreatedById, cancellationToken) is { } forbidden)
                {
                    return Result.Failure<PrintedTicket>(forbidden);
                }

                folio = sale.Folio;
                ticket = TicketBuilder.Build(profile, sale, settings.Columns, new TicketOptions(command.IsReprint));
            }
            else if (command.Source is PrintSource.ShiftReportSource shiftSource)
            {
                var report = await _shifts.GetReportAsync(shiftSource.ShiftId, cancellationToken);
                if (report is null)
                {
                    return Result.Failure<PrintedTicket>(new NotFound());
                }

                // El corte es del administrador o de quien cerró el turno, que lo imprime al cerrar (research §12).
                if (report.ClosedById != _currentUser.UserId
                    && await _access.CheckAsync(Permission.ManageShifts, cancellationToken) is { Allowed: false } denied)
                {
                    return Result.Failure<PrintedTicket>(denied.Error!);
                }

                folio = report.Folio;
                ticket = ShiftTicketBuilder.BuildReport(profile, report, settings.Columns, new TicketOptions(command.IsReprint));
            }
            else if (command.Source is PrintSource.CashMovementSource movementSource)
            {
                var receipt = await _shifts.FindMovementAsync(movementSource.MovementId, cancellationToken);
                if (receipt is null)
                {
                    return Result.Failure<PrintedTicket>(new NotFound());
                }

                // Comprobante: del administrador o del movimiento propio en el turno propio (research §12).
                var own = receipt.CreatedById == _currentUser.UserId && receipt.ShiftOwnerId == _currentUser.UserId;
                if (!own && await _access.CheckAsync(Permission.ManageShifts, cancellationToken) is { Allowed: false } denied)
                {
                    return Result.Failure<PrintedTicket>(denied.Error!);
                }

                folio = receipt.Folio;
                ticket = ShiftTicketBuilder.BuildMovementReceipt(profile, receipt, settings.Columns, new TicketOptions(command.IsReprint));
            }
            else
            {
                ticket = TicketBuilder.BuildSample(profile, settings.Columns);
            }

            if (!settings.IsConfigured)
            {
                return Result.Failure<PrintedTicket>(new NotConfigured());
            }

            var outcome = await _printer.PrintAsync(ticket, settings, cancellationToken);
            if (outcome.Succeeded)
            {
                return Result.Success(new PrintedTicket(outcome.Destination ?? string.Empty));
            }

            LogFailed(folio, settings.PrinterName, outcome.Failure);
            return Result.Failure<PrintedTicket>(outcome.Failure switch
            {
                DeviceFailure.NotConfigured => new NotConfigured(),
                DeviceFailure.Unavailable => new PrinterUnavailable(),
                _ => new PrintFailed(),
            });
        }
#pragma warning disable CA1031 // Una falla de dispositivo nunca debe afectar la venta ni cerrar la aplicación.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogException(ex, folio, settings.PrinterName);
            return Result.Failure<PrintedTicket>(new PrintFailed());
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo imprimir el ticket. Folio={Folio} Impresora={Printer} Motivo={Failure}")]
    private partial void LogFailed(string folio, string? printer, DeviceFailure? failure);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falla inesperada al imprimir el ticket. Folio={Folio} Impresora={Printer}")]
    private partial void LogException(Exception exception, string folio, string? printer);
}
