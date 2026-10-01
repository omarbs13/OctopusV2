using System.Globalization;
using Pos.Application.Business;
using Pos.Application.CashShifts;
using Pos.Domain.CashShifts;

namespace Pos.Application.Printing.Ticket;

/// <summary>
/// Arma el corte de caja y el comprobante de un movimiento de efectivo como renglones ya ajustados
/// al ancho (32 o 48 columnas). Usa las cifras guardadas en la instantánea del turno, sin recalcular
/// (research §10 y §12).
/// </summary>
public static class ShiftTicketBuilder
{
    public const string ReportTitle = "CORTE DE CAJA";
    public const string DepositTitle = "INGRESO DE EFECTIVO";
    public const string WithdrawalTitle = "RETIRO DE EFECTIVO";

    private const string DateFormat = "dd/MM/yyyy HH:mm";

    public static TicketDocument BuildReport(
        BusinessProfileDto? profile,
        ShiftReportDto report,
        int columns,
        TicketOptions? options = null,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 16);
        options ??= new TicketOptions();
        var zone = timeZone ?? TimeZoneInfo.Local;

        var lines = new List<TicketLine>();
        AddHeader(lines, profile, columns);
        lines.Add(new TicketLine(ReportTitle, TicketAlignment.Center, Bold: true));
        if (options.IsReprint)
        {
            lines.Add(new TicketLine(TicketBuilder.ReprintLegend, TicketAlignment.Center, Bold: true));
        }

        lines.Add(new TicketLine(TextWrap.TwoColumns("Caja", report.RegisterName, columns)));
        lines.Add(new TicketLine(TextWrap.TwoColumns("Turno", report.Folio, columns)));
        AddWrapped(lines, $"Usuario: {report.OpenedByName}", columns);
        lines.Add(new TicketLine($"Apertura: {Local(report.OpenedAtUtc, zone)}"));
        lines.Add(new TicketLine($"Cierre: {Local(report.ClosedAtUtc, zone)}"));
        if (report.ClosedById != report.OpenedById)
        {
            AddWrapped(lines, $"Cerrado por: {report.ClosedByName}", columns);
        }

        lines.Add(Separator(columns));
        lines.Add(Row("Fondo inicial", TicketBuilder.FormatMoney(report.OpeningFloatCents), columns));
        lines.Add(Row("Ventas", report.SalesCount.ToString(CultureInfo.InvariantCulture), columns));
        lines.Add(Row("Canceladas", report.CancelledCount.ToString(CultureInfo.InvariantCulture), columns));

        lines.Add(Separator(columns));
        lines.Add(Row("Efectivo", TicketBuilder.FormatMoney(report.CashSalesCents - report.CashCancelledCents), columns));
        lines.Add(Row("Tarjeta", TicketBuilder.FormatMoney(report.CardCents), columns));
        lines.Add(Row("Transferencia", TicketBuilder.FormatMoney(report.TransferCents), columns));
        if (report.CashCancelledCents > 0)
        {
            lines.Add(Row("Efectivo cancelado", TicketBuilder.FormatMoney(report.CashCancelledCents), columns));
        }

        lines.Add(Row("Ingresos", TicketBuilder.FormatMoney(report.DepositsCents), columns));
        lines.Add(Row("Retiros", TicketBuilder.FormatMoney(report.WithdrawalsCents), columns));
        lines.Add(Row("Reint. efectivo", TicketBuilder.FormatMoney(report.CashRefundsCents), columns));
        lines.Add(Row("Reint. tarjeta/transf.", TicketBuilder.FormatMoney(report.NonCashRefundsCents), columns));
        lines.Add(Row("Notas crédito emitidas", TicketBuilder.FormatMoney(report.CreditNotesIssuedCents), columns));

        lines.Add(Separator(columns));
        lines.Add(Row("Efectivo esperado", TicketBuilder.FormatMoney(report.ExpectedCashCents), columns));
        lines.Add(Row("Efectivo contado", TicketBuilder.FormatMoney(report.CountedCashCents), columns));
        lines.Add(new TicketLine(
            TextWrap.TwoColumns(DifferenceLabel(report.DifferenceCents), TicketBuilder.FormatMoney(Math.Abs(report.DifferenceCents)), columns),
            Bold: true));

        if (!string.IsNullOrWhiteSpace(report.Comment))
        {
            lines.Add(Separator(columns));
            AddWrapped(lines, $"Comentario: {report.Comment}", columns);
        }

        return new TicketDocument(lines, profile?.Logo, columns, report.Folio);
    }

    public static TicketDocument BuildMovementReceipt(
        BusinessProfileDto? profile,
        CashMovementReceiptDto receipt,
        int columns,
        TicketOptions? options = null,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 16);
        options ??= new TicketOptions();
        var zone = timeZone ?? TimeZoneInfo.Local;

        var lines = new List<TicketLine>();
        AddHeader(lines, profile, columns);
        lines.Add(new TicketLine(
            receipt.Type == CashMovementType.In ? DepositTitle : WithdrawalTitle,
            TicketAlignment.Center,
            Bold: true));
        if (options.IsReprint)
        {
            lines.Add(new TicketLine(TicketBuilder.ReprintLegend, TicketAlignment.Center, Bold: true));
        }

        lines.Add(new TicketLine($"Folio: {receipt.Folio}"));
        lines.Add(new TicketLine(Local(receipt.CreatedAtUtc, zone)));
        AddWrapped(lines, $"Usuario: {receipt.CreatedByName}", columns);
        lines.Add(Separator(columns));
        lines.Add(new TicketLine(TextWrap.TwoColumns("MONTO", TicketBuilder.FormatMoney(receipt.AmountCents), columns), Bold: true));
        AddWrapped(lines, $"Motivo: {receipt.Reason}", columns);
        if (!string.IsNullOrWhiteSpace(receipt.AuthorizedByName))
        {
            AddWrapped(lines, $"Autorizó: {receipt.AuthorizedByName}", columns);
        }

        lines.Add(new TicketLine(string.Empty));
        lines.Add(new TicketLine(string.Empty));
        lines.Add(new TicketLine(new string('_', columns)));
        lines.Add(new TicketLine("Firma", TicketAlignment.Center));

        return new TicketDocument(lines, profile?.Logo, columns, receipt.Folio);
    }

    private static string DifferenceLabel(long differenceCents) => differenceCents switch
    {
        0 => "Cuadrado",
        > 0 => "Sobrante",
        _ => "Faltante",
    };

    private static string Local(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone)
            .ToString(DateFormat, CultureInfo.InvariantCulture);

    private static TicketLine Row(string label, string value, int columns) => new(TextWrap.TwoColumns(label, value, columns));

    private static TicketLine Separator(int columns) => new(new string('-', columns));

    private static void AddWrapped(List<TicketLine> lines, string text, int columns) =>
        lines.AddRange(TextWrap.Wrap(text, columns).Select(t => new TicketLine(t)));

    private static void AddHeader(List<TicketLine> lines, BusinessProfileDto? profile, int columns)
    {
        if (profile is null)
        {
            return;
        }

        foreach (var text in TextWrap.Wrap(profile.TradeName, columns))
        {
            lines.Add(new TicketLine(text, TicketAlignment.Center, Bold: true));
        }
    }
}
