using Pos.Domain.Common;
using Pos.Domain.Receivables;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Receivables;

/// <summary>014, FR-009 y FR-014: formas de pago del abono y anulación única con motivo.</summary>
public sealed class CustomerPaymentTests
{
    private static CustomerPayment New(PaymentMethod method = PaymentMethod.Cash) =>
        CustomerPayment.Register(1, Guid.CreateVersion7(), Guid.CreateVersion7(), 30_000, method, " ref ", Guid.CreateVersion7(), 110_000);

    [Fact]
    public void Registrar_GuardaSaldosAnteriorYNuevoYElFolio()
    {
        var payment = New();

        Assert.Equal("AB-000001", payment.Folio);
        Assert.Equal((110_000L, 80_000L), (payment.BalanceBeforeCents, payment.BalanceAfterCents));
        Assert.Equal("ref", payment.Reference);
        Assert.Equal(CustomerPaymentStatus.Active, payment.Status);
    }

    [Theory]
    [InlineData(PaymentMethod.OnAccount)]
    [InlineData(PaymentMethod.CreditNote)]
    public void Registrar_ConCreditoONotaDeCredito_SeRechaza(PaymentMethod method) =>
        Assert.Throws<DomainException>(() => New(method));

    [Fact]
    public void Registrar_MontoMayorQueElSaldo_SeRechaza() =>
        Assert.Throws<DomainException>(() =>
            CustomerPayment.Register(1, Guid.CreateVersion7(), Guid.CreateVersion7(), 2, PaymentMethod.Cash, null, null, 1));

    [Fact]
    public void Anular_SoloUnaVezYConMotivo()
    {
        var payment = New();
        var admin = Guid.CreateVersion7();
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.Throws<InvalidOperationException>(() => payment.Void("   ", admin, admin, null, now));
        Assert.Throws<InvalidOperationException>(() => payment.Void(new string('x', 251), admin, admin, null, now));
        Assert.Equal(CustomerPaymentStatus.Active, payment.Status);

        payment.Void(" Error de captura ", admin, admin, null, now);

        Assert.Equal((CustomerPaymentStatus.Voided, "Error de captura"), (payment.Status, payment.VoidReason));
        Assert.Equal(now, payment.VoidedAt);
        Assert.Throws<InvalidOperationException>(() => payment.Void("Otra vez", admin, admin, null, now));
    }
}
