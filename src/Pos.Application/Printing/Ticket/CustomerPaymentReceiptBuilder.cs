using System.Globalization;
using Pos.Application.Business;
using Pos.Application.Receivables;
using Pos.Domain.Sales;

namespace Pos.Application.Printing.Ticket;

/// <summary>
/// Arma el recibo de un abono (014, FR-013): folio, fecha local, cliente, monto, forma de pago,
/// referencia y saldos anterior y nuevo, como renglones ya ajustados al ancho. Un abono anulado se
/// reimprime con la leyenda "ANULADO" (research §12).
/// </summary>
public static class CustomerPaymentReceiptBuilder
{
    public const string Title = "RECIBO DE ABONO";
    public const string VoidedLegend = "ANULADO";

    private const string DateFormat = "dd/MM/yyyy HH:mm";

    public static TicketDocument Build(
        BusinessProfileDto? profile,
        CustomerPaymentReceiptData receipt,
        int columns,
        TicketOptions? options = null,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 16);
        options ??= new TicketOptions();

        var lines = new List<TicketLine>();
        TicketHeader.Add(lines, profile, columns);

        lines.Add(new TicketLine(Title, TicketAlignment.Center, Bold: true));
        if (receipt.IsVoided)
        {
            lines.Add(new TicketLine(VoidedLegend, TicketAlignment.Center, Bold: true));
        }

        if (options.IsReprint)
        {
            lines.Add(new TicketLine(TicketBuilder.ReprintLegend, TicketAlignment.Center, Bold: true));
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(receipt.CreatedAtUtc, DateTimeKind.Utc), timeZone ?? TimeZoneInfo.Local);
        lines.Add(new TicketLine($"Folio: {receipt.Folio}", Bold: true));
        lines.Add(new TicketLine(local.ToString(DateFormat, CultureInfo.InvariantCulture)));
        lines.AddRange(TextWrap.Wrap($"Cliente: {receipt.CustomerName}", columns).Select(t => new TicketLine(t)));
        lines.Add(new TicketLine(new string('-', columns)));
        lines.Add(new TicketLine(TextWrap.TwoColumns("ABONO", TicketBuilder.FormatMoney(receipt.AmountCents), columns), Bold: true));
        lines.Add(new TicketLine(TextWrap.TwoColumns("Forma de pago", MethodText(receipt.Method), columns)));
        if (!string.IsNullOrWhiteSpace(receipt.Reference))
        {
            lines.AddRange(TextWrap.Wrap($"Referencia: {receipt.Reference}", columns).Select(t => new TicketLine(t)));
        }

        lines.Add(new TicketLine(new string('-', columns)));
        lines.Add(new TicketLine(TextWrap.TwoColumns("Saldo anterior", TicketBuilder.FormatMoney(receipt.BalanceBeforeCents), columns)));
        lines.Add(new TicketLine(TextWrap.TwoColumns("Saldo nuevo", TicketBuilder.FormatMoney(receipt.BalanceAfterCents), columns), Bold: true));
        return new TicketDocument(lines, profile?.Logo, columns, receipt.Folio);
    }

    private static string MethodText(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.Card => "Tarjeta",
        _ => "Transferencia",
    };
}
