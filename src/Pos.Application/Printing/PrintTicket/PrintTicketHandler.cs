using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Business;
using Pos.Application.CashShifts;
using Pos.Application.CreditNotes;
using Pos.Application.Licensing;
using Pos.Application.Printing.Ticket;
using Pos.Application.Receivables;
using Pos.Application.Sales;
using Pos.Application.Users;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Users;

namespace Pos.Application.Printing.PrintTicket;

/// <summary>
/// Imprime el ticket de una venta o el de prueba. Solo lee: no escribe en la base ni consume folio,
/// salvo la bitácora de la reimpresión de un corte (017, FR-017). Una falla del dispositivo se registra
/// y se devuelve como error de negocio; nunca lanza (006, FR-009).
/// </summary>
public sealed partial class PrintTicketHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ISaleRepository _sales;
    private readonly ICashShiftRepository _shifts;
    private readonly ICreditNoteRepository? _creditNotes;
    private readonly ICustomerPaymentRepository? _customerPayments;
    private readonly IAuditLog? _audit;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IPrintingSettingsStore _settings;
    private readonly ITicketPrinter _printer;
    private readonly ILogger<PrintTicketHandler> _logger;
    private readonly ILicenseState? _license;

    public PrintTicketHandler(
        IAccessControl access,
        ICurrentUser currentUser,
        ISaleRepository sales,
        ICashShiftRepository shifts,
        IBusinessProfileRepository profiles,
        IPrintingSettingsStore settings,
        ITicketPrinter printer,
        ILogger<PrintTicketHandler> logger,
        ICreditNoteRepository? creditNotes = null,
        ICustomerPaymentRepository? customerPayments = null,
        IAuditLog? audit = null,
        ILicenseState? license = null)
    {
        _license = license;
        _audit = audit;
        _creditNotes = creditNotes;
        _customerPayments = customerPayments;
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

        // 025, FR-030a: la impresión original del ticket de la venta y del corte del turno es parte de terminar el
        // trabajo ya iniciado; no depende de la licencia. En bloqueo se rechaza todo lo demás (reimpresiones incluidas).
        var finishing = !command.IsReprint
            && command.Source is PrintSource.SaleSource or PrintSource.ShiftReportSource or PrintSource.ShiftCutSource;
        if (!finishing && LicenseGate.WhenBlocked(_license) is { } blocked)
        {
            return Result.Failure<PrintedTicket>(blocked);
        }

        // El ticket de una venta es de quien la hizo (o de quien ve todas); el de prueba es de configuración;
        // el corte y los comprobantes de efectivo son del turno (008).
        var access = await CheckAsync(
            finishing,
            command.Source switch
            {
                PrintSource.SaleSource => Permission.ViewOwnSales,
                PrintSource.ShiftReportSource or PrintSource.CashMovementSource or PrintSource.ShiftCutSource => Permission.OperateShift,
                // Quien emite la nota (ProcessReturns) la imprime al emitirla; reimprimir exige ManageCreditNotes (research §13).
                PrintSource.CreditNoteSource => command.IsReprint ? Permission.ManageCreditNotes : Permission.ProcessReturns,
                // Quien registra abonos imprime y reimprime su recibo (014, contracts/ui.md "Abonos").
                PrintSource.CustomerPaymentSource => Permission.RegisterCustomerPayments,
                _ => Permission.ManageSettings,
            },
            cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<PrintedTicket>(access.Error!);
        }

        var settings = _settings.Load();
        var folio = TicketBuilder.SampleFolio;
        ShiftCutReportDto? reprintedCut = null;
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
                    && await CheckAsync(finishing, Permission.ManageShifts, cancellationToken) is { Allowed: false } denied)
                {
                    return Result.Failure<PrintedTicket>(denied.Error!);
                }

                folio = report.Folio;
                ticket = ShiftTicketBuilder.BuildReport(profile, report, settings.Columns, new TicketOptions(command.IsReprint));
            }
            else if (command.Source is PrintSource.ShiftCutSource cutSource)
            {
                var cut = await _shifts.GetCutReportAsync(cutSource.CutId, cancellationToken);
                if (cut is null)
                {
                    return Result.Failure<PrintedTicket>(new NotFound());
                }

                // El corte es de quien lo generó (el Cajero autorizado imprime su Corte X) o del administrador (research §10).
                if (cut.GeneratedById != _currentUser.UserId
                    && await CheckAsync(finishing, Permission.ManageShifts, cancellationToken) is { Allowed: false } denied)
                {
                    return Result.Failure<PrintedTicket>(denied.Error!);
                }

                folio = cut.Folio;
                reprintedCut = command.IsReprint ? cut : null;
                ticket = ShiftTicketBuilder.BuildCut(profile, cut, settings.Columns, new TicketOptions(command.IsReprint));
            }
            else if (command.Source is PrintSource.CreditNoteSource noteSource)
            {
                var data = _creditNotes is null ? null : await _creditNotes.GetTicketDataAsync(noteSource.CreditNoteId, cancellationToken);
                if (data is null)
                {
                    return Result.Failure<PrintedTicket>(new NotFound());
                }

                folio = data.Folio;
                ticket = CreditNoteTicketBuilder.Build(profile, data, settings.Columns, new TicketOptions(command.IsReprint));
            }
            else if (command.Source is PrintSource.CustomerPaymentSource paymentSource)
            {
                var data = _customerPayments is null ? null : await _customerPayments.GetReceiptDataAsync(paymentSource.PaymentId, cancellationToken);
                if (data is null)
                {
                    return Result.Failure<PrintedTicket>(new NotFound());
                }

                folio = data.Folio;
                ticket = CustomerPaymentReceiptBuilder.Build(profile, data, settings.Columns, new TicketOptions(command.IsReprint));
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
                if (reprintedCut is not null && _audit is not null)
                {
                    var kind = reprintedCut.Type == ShiftCutType.Readout ? "Corte X" : "Corte Z";
                    _audit.Add(
                        AuditActions.ShiftCutReprinted,
                        AuditActions.ShiftCutEntity,
                        reprintedCut.CutId,
                        $"{kind} {reprintedCut.Folio}. Turno {reprintedCut.ShiftFolio}");
                    await _audit.SaveAsync(cancellationToken);
                }

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

    private Task<AccessDecision> CheckAsync(bool finishing, Permission permission, CancellationToken cancellationToken) =>
        finishing ? _access.CheckToFinishAsync(permission, cancellationToken) : _access.CheckAsync(permission, cancellationToken);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falla inesperada al imprimir el ticket. Folio={Folio} Impresora={Printer}")]
    private partial void LogException(Exception exception, string folio, string? printer);
}
