using Pos.Domain.Common;

namespace Pos.Domain.Sales;

/// <summary>Pago no en efectivo capturado en el cobro.</summary>
public sealed record CheckoutPayment(PaymentMethod Method, Money Amount, string? Reference);

/// <summary>Pago que se registrará con la venta: monto aplicado y, en efectivo, recibido y cambio.</summary>
public sealed record PaymentEntry(PaymentMethod Method, Money Amount, Money? Received, Money? Change, string? Reference);

/// <summary>
/// Cobro de una venta, en memoria (research §6): a lo más un pago en efectivo y cero o más pagos con
/// tarjeta o transferencia. Solo el efectivo da cambio.
/// </summary>
public sealed class Checkout
{
    public const int ReferenceMaxLength = 50;

    /// <summary>Billetes de los montos rápidos, en pesos.</summary>
    public static IReadOnlyList<int> Bills { get; } = [20, 50, 100, 200, 500, 1000];

    private readonly List<CheckoutPayment> _payments = [];

    public Checkout(Money total) => Total = total;

    public Money Total { get; }

    /// <summary>Efectivo recibido; cero si no hay pago en efectivo.</summary>
    public Money Received { get; private set; }

    /// <summary>Pagos con tarjeta o transferencia, en el orden capturado.</summary>
    public IReadOnlyList<CheckoutPayment> Payments => _payments;

    public Money NonCashTotal => Money.FromCents(_payments.Sum(p => p.Amount.Cents));

    /// <summary>Parte de la venta que falta cubrir sin contar el efectivo; tope de un pago no en efectivo.</summary>
    public Money Pending => Money.FromCents(Total.Cents - NonCashTotal.Cents);

    /// <summary>Parte del total que se cubre en efectivo.</summary>
    public Money CashApplied => Pending;

    public Money Change => Money.FromCents(Math.Max(0, Received.Cents - CashApplied.Cents));

    public Money Shortfall => Money.FromCents(Math.Max(0, Pending.Cents - Received.Cents));

    public bool CanConfirm => Total.Cents > 0 && Shortfall.Cents == 0;

    /// <summary>Venta a crédito: un único pago <see cref="PaymentMethod.OnAccount"/> por el total (014).</summary>
    public bool IsOnAccount => _payments.Any(p => p.Method == PaymentMethod.OnAccount);

    public void SetCashReceived(Money received) => Received = IsOnAccount ? Money.Zero : received;

    /// <summary>
    /// Venta a crédito (014, FR-005): reemplaza todos los pagos capturados por un único pago
    /// <see cref="PaymentMethod.OnAccount"/> por el total, sin efectivo recibido ni cambio.
    /// </summary>
    public void SetOnAccount()
    {
        if (Total.Cents <= 0)
        {
            throw new DomainException("El total de la venta debe ser mayor que 0.");
        }

        _payments.Clear();
        Received = Money.Zero;
        _payments.Add(new CheckoutPayment(PaymentMethod.OnAccount, Total, null));
    }

    /// <summary>Monto rápido: exacto (sin billete) usa el pendiente; un billete reemplaza lo recibido.</summary>
    public void QuickAmount(int? bill = null)
    {
        if (bill is null)
        {
            Received = Pending;
            return;
        }

        if (!Bills.Contains(bill.Value))
        {
            throw new DomainException("El billete no es válido.");
        }

        Received = Money.FromCents(bill.Value * 100L);
    }

    /// <summary>Agrega un pago con tarjeta o transferencia que no puede exceder el pendiente.</summary>
    public void AddNonCash(PaymentMethod method, Money amount, string? reference)
    {
        if (method == PaymentMethod.Cash)
        {
            throw new DomainException("El efectivo se captura como monto recibido.");
        }

        if (method == PaymentMethod.OnAccount || IsOnAccount)
        {
            throw new DomainException("La venta a crédito debe tener un único pago por el total.");
        }

        if (amount.Cents <= 0)
        {
            throw new DomainException("El monto debe ser mayor que 0.");
        }

        if (amount.Cents > Pending.Cents)
        {
            throw new DomainException(
                $"El monto con tarjeta o transferencia no puede exceder el pendiente de {FormatPesos(Pending)}");
        }

        var text = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (method == PaymentMethod.CreditNote)
        {
            if (text is null)
            {
                throw new DomainException("La nota de crédito requiere el folio.");
            }

            if (_payments.Any(p => p.Method == PaymentMethod.CreditNote))
            {
                throw new DomainException("La venta admite a lo más un pago con nota de crédito.");
            }
        }

        if (text is { Length: > ReferenceMaxLength })
        {
            throw new DomainException($"La referencia admite hasta {ReferenceMaxLength} caracteres.");
        }

        _payments.Add(new CheckoutPayment(method, amount, text));
    }

    public void RemovePayment(int index)
    {
        if (index >= 0 && index < _payments.Count)
        {
            _payments.RemoveAt(index);
        }
    }

    /// <summary>Pagos a registrar. La suma de los montos aplicados es el total de la venta.</summary>
    public IReadOnlyList<PaymentEntry> ToPayments()
    {
        if (!CanConfirm)
        {
            throw new DomainException("El pago no cubre el total de la venta.");
        }

        var entries = new List<PaymentEntry>();
        if (CashApplied.Cents > 0)
        {
            entries.Add(new PaymentEntry(PaymentMethod.Cash, CashApplied, Received, Change, null));
        }

        entries.AddRange(_payments.Select(p => new PaymentEntry(p.Method, p.Amount, null, null, p.Reference)));
        return entries;
    }

    private static string FormatPesos(Money money) =>
        "$" + (money.Cents / 100m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
}
