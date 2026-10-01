using System.Globalization;
using Pos.Application.Business;
using Pos.Application.Sales;
using Pos.Domain.Sales;

namespace Pos.Application.Printing.Ticket;

/// <summary>
/// Arma el ticket como renglones de texto ya ajustados al ancho (32 u 48 columnas) según
/// <c>contracts/ticket-format.md</c>. Usa los importes guardados en la venta, sin recalcular.
/// </summary>
public static class TicketBuilder
{
    public const string SampleFolio = "PRUEBA";
    public const string CancelledLegend = "CANCELADA";
    public const string ReprintLegend = "REIMPRESIÓN";

    private const string DateFormat = "dd/MM/yyyy HH:mm";

    /// <summary>Arma el ticket de una venta; <paramref name="timeZone"/> nulo usa la zona del sistema.</summary>
    public static TicketDocument Build(
        BusinessProfileDto? profile,
        SaleDetailDto sale,
        int columns,
        TicketOptions? options = null,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 16);
        options ??= new TicketOptions();

        var lines = new List<TicketLine>();
        AddHeader(lines, profile, columns);

        if (sale.Status == SaleStatus.Cancelled)
        {
            lines.Add(new TicketLine(CancelledLegend, TicketAlignment.Center, Bold: true));
        }

        if (options.IsReprint)
        {
            lines.Add(new TicketLine(ReprintLegend, TicketAlignment.Center, Bold: true));
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(sale.CreatedAtUtc, DateTimeKind.Utc), timeZone ?? TimeZoneInfo.Local);
        lines.Add(new TicketLine($"Folio: {sale.Folio}"));
        lines.Add(new TicketLine(local.ToString(DateFormat, CultureInfo.InvariantCulture)));
        if (!string.IsNullOrWhiteSpace(sale.CreatedByName))
        {
            // 007: el ticket muestra al cajero; el nombre largo se recorta al ancho del papel.
            var cashier = $"Cajero: {sale.CreatedByName.Trim()}";
            lines.Add(new TicketLine(cashier.Length > columns ? cashier[..columns] : cashier));
        }

        if (sale.Credit is { } credit)
        {
            // 014: la venta a crédito muestra al cliente con el nombre que tenía al vender (research §2).
            foreach (var text in TextWrap.Wrap($"Cliente: {credit.CustomerName}", columns))
            {
                lines.Add(new TicketLine(text));
            }
        }

        lines.Add(Separator(columns));

        foreach (var line in sale.Lines.OrderBy(l => l.Position))
        {
            AddSaleLine(lines, line, columns);
        }

        lines.Add(Separator(columns));
        lines.Add(new TicketLine(TextWrap.TwoColumns("TOTAL", FormatMoney(sale.TotalCents), columns), Bold: true));
        if (sale.Credit is not null)
        {
            // En lugar del bloque de pagos: sin efectivo recibido ni cambio (research §12).
            lines.Add(new TicketLine(TextWrap.TwoColumns("A crédito:", FormatMoney(sale.TotalCents), columns), Bold: true));
        }
        else
        {
            AddPayments(lines, sale.Payments, columns);
        }

        if (!string.IsNullOrWhiteSpace(profile?.FooterMessage))
        {
            lines.Add(Separator(columns));
            AddCentered(lines, profile.FooterMessage, columns);
        }

        return new TicketDocument(lines, profile?.Logo, columns, sale.Folio);
    }

    /// <summary>Ticket de prueba: folio <c>PRUEBA</c> y renglones de ejemplo fijos; no toca ninguna venta.</summary>
    public static TicketDocument BuildSample(BusinessProfileDto? profile, int columns, TimeZoneInfo? timeZone = null)
    {
        var sale = new SaleDetailDto(
            Guid.Empty,
            SampleFolio,
            DateTime.UtcNow,
            string.Empty,
            22350,
            SaleStatus.Completed,
            1,
            null,
            null,
            null,
            [
                new SaleLineDto(1, Guid.Empty, "Refresco cola 600 ml", "REF-600", "H87", 0, 1800, 2000, 3600),
                new SaleLineDto(2, Guid.Empty, "Queso oaxaca de rancho tradicional", "QUE-001", "KGM", 3, 15000, 1250, 18750),
            ],
            [new SalePaymentDto(PaymentMethod.Cash, 22350, 30000, 7650, null)],
            Guid.Empty);
        return Build(profile, sale, columns, new TicketOptions(), timeZone);
    }

    public static string FormatMoney(long cents) =>
        (cents < 0 ? "-$" : "$") + (Math.Abs(cents) / 100m).ToString("N2", CultureInfo.InvariantCulture);

    public static string FormatQuantity(long thousandths, int decimalPlaces) =>
        (thousandths / 1000m).ToString("F" + Math.Clamp(decimalPlaces, 0, 3), CultureInfo.InvariantCulture);

    private static TicketLine Separator(int columns) => new(new string('-', columns));

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

        AddCentered(lines, profile.Address, columns);
        if (!string.IsNullOrWhiteSpace(profile.Phone))
        {
            AddCentered(lines, $"Tel. {profile.Phone}", columns);
        }

        if (!string.IsNullOrWhiteSpace(profile.TaxId))
        {
            AddCentered(lines, $"RFC: {profile.TaxId}", columns);
        }
    }

    private static void AddCentered(List<TicketLine> lines, string? text, int columns)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lines.AddRange(TextWrap.Wrap(text, columns).Select(t => new TicketLine(t, TicketAlignment.Center)));
    }

    private static void AddSaleLine(List<TicketLine> lines, SaleLineDto line, int columns)
    {
        var amount = FormatMoney(line.AmountCents);
        var prefix = FormatQuantity(line.QuantityThousandths, line.DecimalPlaces) + " ";
        var indent = new string(' ', prefix.Length);
        var description = TextWrap.Wrap(line.ProductName, columns - indent.Length);
        var text = description.Select((d, i) => (i == 0 ? prefix : indent) + d).ToList();

        var last = text[^1];
        if (last.Length + 1 + amount.Length <= columns)
        {
            text[^1] = TextWrap.TwoColumns(last, amount, columns);
        }
        else
        {
            text.Add(TextWrap.Align(amount, columns, TicketAlignment.Right));
        }

        lines.AddRange(text.Select(t => new TicketLine(t)));
    }

    private static void AddPayments(List<TicketLine> lines, IReadOnlyList<SalePaymentDto> payments, int columns)
    {
        foreach (var payment in payments)
        {
            switch (payment.Method)
            {
                case PaymentMethod.Cash:
                    lines.Add(new TicketLine(TextWrap.TwoColumns("Efectivo recibido", FormatMoney(payment.ReceivedCents ?? payment.AmountCents), columns)));
                    if (payment.ChangeCents is > 0)
                    {
                        lines.Add(new TicketLine(TextWrap.TwoColumns("CAMBIO", FormatMoney(payment.ChangeCents.Value), columns)));
                    }

                    break;
                case PaymentMethod.Card:
                    lines.Add(new TicketLine(TextWrap.TwoColumns("Tarjeta", FormatMoney(payment.AmountCents), columns)));
                    break;
                case PaymentMethod.CreditNote:
                    lines.Add(new TicketLine(TextWrap.TwoColumns("Nota de crédito", FormatMoney(payment.AmountCents), columns)));
                    break;
                case PaymentMethod.OnAccount:
                    lines.Add(new TicketLine(TextWrap.TwoColumns("A crédito:", FormatMoney(payment.AmountCents), columns)));
                    break;
                default:
                    lines.Add(new TicketLine(TextWrap.TwoColumns("Transferencia", FormatMoney(payment.AmountCents), columns)));
                    break;
            }
        }
    }
}
