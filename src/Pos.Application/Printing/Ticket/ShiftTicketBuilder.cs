using System.Globalization;
using Pos.Application.Business;
using Pos.Application.CashShifts;
using Pos.Domain.CashShifts;

namespace Pos.Application.Printing.Ticket;

/// <summary>
/// Arma el corte de caja, los Cortes X y Z (017) y el comprobante de un movimiento de efectivo como
/// renglones ya ajustados al ancho (32 o 48 columnas). Usa las cifras guardadas en la instantánea del
/// turno o del corte, sin recalcular (research §10 y §12).
/// </summary>
public static class ShiftTicketBuilder
{
    public const string ReportTitle = "CORTE DE CAJA";
    public const string DepositTitle = "INGRESO DE EFECTIVO";
    public const string WithdrawalTitle = "RETIRO DE EFECTIVO";
    public const string ReadoutTitle = "CORTE X";
    public const string ClosingTitle = "CORTE Z";
    public const string ReadoutLegend = "LECTURA PARCIAL - NO ES CIERRE DE CAJA";

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
        TicketHeader.Add(lines, profile, columns);
        // 017: los turnos cerrados desde 0.12.0 tienen Corte Z; los anteriores conservan "CORTE DE CAJA".
        lines.Add(new TicketLine(
            report.CutFolio is { } cutFolio ? $"{ClosingTitle} {cutFolio}" : ReportTitle,
            TicketAlignment.Center,
            Bold: true));
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

        AddFigures(lines, Figures.From(report), columns);
        lines.Add(Row("Efectivo contado", TicketBuilder.FormatMoney(report.CountedCashCents), columns));
        AddDifference(lines, report.DifferenceCents, columns);

        if (!string.IsNullOrWhiteSpace(report.Comment))
        {
            lines.Add(Separator(columns));
            AddWrapped(lines, $"Comentario: {report.Comment}", columns);
        }

        return new TicketDocument(lines, profile?.Logo, columns, report.Folio);
    }

    /// <summary>
    /// Corte X o Z (017, contracts/ui.md "Ticket impreso"): las mismas filas de cifras que el corte de
    /// 008; el X lleva la leyenda de lectura parcial y no tiene contado, diferencia ni comentario.
    /// </summary>
    public static TicketDocument BuildCut(
        BusinessProfileDto? profile,
        ShiftCutReportDto cut,
        int columns,
        TicketOptions? options = null,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(cut);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 16);
        options ??= new TicketOptions();
        var zone = timeZone ?? TimeZoneInfo.Local;
        var isReadout = cut.Type == ShiftCutType.Readout;

        var lines = new List<TicketLine>();
        TicketHeader.Add(lines, profile, columns);
        lines.Add(new TicketLine(isReadout ? ReadoutTitle : ClosingTitle, TicketAlignment.Center, Bold: true));
        if (isReadout)
        {
            lines.AddRange(TextWrap.Wrap(ReadoutLegend, columns).Select(t => new TicketLine(t, TicketAlignment.Center)));
        }

        if (options.IsReprint)
        {
            lines.Add(new TicketLine(TicketBuilder.ReprintLegend, TicketAlignment.Center, Bold: true));
        }

        lines.Add(new TicketLine(TextWrap.TwoColumns("Corte", cut.Folio, columns)));
        lines.Add(new TicketLine(TextWrap.TwoColumns("Turno", cut.ShiftFolio, columns)));
        lines.Add(new TicketLine(TextWrap.TwoColumns("Caja", cut.RegisterName, columns)));
        AddWrapped(lines, $"Usuario: {cut.ShiftOwnerName}", columns);
        AddWrapped(lines, $"Generado por: {cut.GeneratedByName}", columns);
        if (!string.IsNullOrWhiteSpace(cut.AuthorizedByName))
        {
            AddWrapped(lines, $"Autorizó: {cut.AuthorizedByName}", columns);
        }

        lines.Add(new TicketLine($"Apertura: {Local(cut.ShiftOpenedAtUtc, zone)}"));
        lines.Add(new TicketLine($"Fecha corte: {Local(cut.GeneratedAtUtc, zone)}"));

        AddFigures(lines, Figures.From(cut), columns);
        if (!isReadout)
        {
            lines.Add(Row("Efectivo contado", TicketBuilder.FormatMoney(cut.CountedCashCents ?? 0), columns));
            AddDifference(lines, cut.DifferenceCents ?? 0, columns);
            if (!string.IsNullOrWhiteSpace(cut.Comment))
            {
                lines.Add(Separator(columns));
                AddWrapped(lines, $"Comentario: {cut.Comment}", columns);
            }
        }

        return new TicketDocument(lines, profile?.Logo, columns, cut.Folio);
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
        TicketHeader.Add(lines, profile, columns);
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

    /// <summary>Filas de cifras del corte de 008, compartidas por el corte de "Turnos" y los Cortes X y Z.</summary>
    private static void AddFigures(List<TicketLine> lines, Figures figures, int columns)
    {
        lines.Add(Separator(columns));
        lines.Add(Row("Fondo inicial", TicketBuilder.FormatMoney(figures.OpeningFloatCents), columns));
        lines.Add(Row("Ventas", figures.SalesCount.ToString(CultureInfo.InvariantCulture), columns));
        lines.Add(Row("Canceladas", figures.CancelledCount.ToString(CultureInfo.InvariantCulture), columns));
        if (figures.TotalSoldCents is { } totalSold)
        {
            lines.Add(Row("Total vendido", TicketBuilder.FormatMoney(totalSold), columns));
        }

        lines.Add(Separator(columns));
        lines.Add(Row("Efectivo", TicketBuilder.FormatMoney(figures.CashSalesCents - figures.CashCancelledCents), columns));
        lines.Add(Row("Tarjeta", TicketBuilder.FormatMoney(figures.CardCents), columns));
        lines.Add(Row("Transferencia", TicketBuilder.FormatMoney(figures.TransferCents), columns));
        if (figures.CashCancelledCents > 0)
        {
            lines.Add(Row("Efectivo cancelado", TicketBuilder.FormatMoney(figures.CashCancelledCents), columns));
        }

        lines.Add(Row("Ingresos", TicketBuilder.FormatMoney(figures.DepositsCents), columns));
        lines.Add(Row("Retiros", TicketBuilder.FormatMoney(figures.WithdrawalsCents), columns));
        lines.Add(Row("Reint. efectivo", TicketBuilder.FormatMoney(figures.CashRefundsCents), columns));
        lines.Add(Row("Reint. tarjeta/transf.", TicketBuilder.FormatMoney(figures.NonCashRefundsCents), columns));
        lines.Add(Row("Notas crédito emitidas", TicketBuilder.FormatMoney(figures.CreditNotesIssuedCents), columns));

        // 014, FR-012: bloque "Crédito"; los turnos cerrados antes de 0.9.0 no lo tienen.
        if (figures.Credit is { } credit)
        {
            lines.Add(Separator(columns));
            lines.Add(new TicketLine("CRÉDITO", TicketAlignment.Center, Bold: true));
            lines.Add(Row("Ventas a crédito", TicketBuilder.FormatMoney(credit.OnAccountSalesCents), columns));
            lines.Add(Row("Abonos efectivo", TicketBuilder.FormatMoney(credit.PaymentsCashCents), columns));
            lines.Add(Row("Abonos tarjeta/transf.", TicketBuilder.FormatMoney(credit.PaymentsNonCashCents), columns));
            lines.Add(Row("Anulaciones de abonos", TicketBuilder.FormatMoney(credit.PaymentVoidsCents), columns));
        }

        lines.Add(Separator(columns));
        lines.Add(Row("Efectivo esperado", TicketBuilder.FormatMoney(figures.ExpectedCashCents), columns));
    }

    private static void AddDifference(List<TicketLine> lines, long differenceCents, int columns) =>
        lines.Add(new TicketLine(
            TextWrap.TwoColumns(DifferenceLabel(differenceCents), TicketBuilder.FormatMoney(Math.Abs(differenceCents)), columns),
            Bold: true));

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

    /// <summary>Cifras comunes del corte; <c>TotalSoldCents</c> nulo en el corte de 008, que no lo imprime.</summary>
    private sealed record Figures(
        long OpeningFloatCents,
        int SalesCount,
        int CancelledCount,
        long? TotalSoldCents,
        long CashSalesCents,
        long CashCancelledCents,
        long CardCents,
        long TransferCents,
        long DepositsCents,
        long WithdrawalsCents,
        long CashRefundsCents,
        long NonCashRefundsCents,
        long CreditNotesIssuedCents,
        ShiftCreditTotals? Credit,
        long ExpectedCashCents)
    {
        public static Figures From(ShiftReportDto report) => new(
            report.OpeningFloatCents,
            report.SalesCount,
            report.CancelledCount,
            null,
            report.CashSalesCents,
            report.CashCancelledCents,
            report.CardCents,
            report.TransferCents,
            report.DepositsCents,
            report.WithdrawalsCents,
            report.CashRefundsCents,
            report.NonCashRefundsCents,
            report.CreditNotesIssuedCents,
            report.Credit,
            report.ExpectedCashCents);

        public static Figures From(ShiftCutReportDto cut) => new(
            cut.OpeningFloatCents,
            cut.SalesCount,
            cut.CancelledCount,
            cut.TotalSoldCents,
            cut.CashSalesCents,
            cut.CashCancelledCents,
            cut.CardCents,
            cut.TransferCents,
            cut.DepositsCents,
            cut.WithdrawalsCents,
            cut.CashRefundsCents,
            cut.NonCashRefundsCents,
            cut.CreditNotesIssuedCents,
            cut.Credit,
            cut.ExpectedCashCents);
    }
}
