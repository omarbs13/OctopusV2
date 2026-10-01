using Pos.Domain.Common;
using Pos.Domain.Receivables;

namespace Pos.Domain.Tests.Receivables;

/// <summary>014, research §3: el saldo es siempre <c>OriginalCents + Σ entradas</c> y está entre 0 y el original.</summary>
public sealed class ReceivableTests
{
    private static Receivable New(long original = 50_000) =>
        Receivable.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "Ana", original, null);

    private static void AssertLedger(Receivable receivable) =>
        Assert.Equal(receivable.OriginalCents + receivable.Entries.Sum(e => e.AmountCents), receivable.BalanceCents);

    [Fact]
    public void Abonos_LlevanElSaldoACeroYPasaAPagada_YAnularRegresaAPendiente()
    {
        var receivable = New();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        receivable.ApplyPayment(first, 20_000);
        Assert.Equal((30_000L, ReceivableStatus.Pending), (receivable.BalanceCents, receivable.Status));

        receivable.ApplyPayment(second, 30_000);
        Assert.Equal((0L, ReceivableStatus.Paid), (receivable.BalanceCents, receivable.Status));

        receivable.RevertPayment(second);
        Assert.Equal((30_000L, ReceivableStatus.Pending), (receivable.BalanceCents, receivable.Status));
        Assert.Equal(ReceivableEntryType.PaymentVoid, receivable.Entries[^1].Type);
        Assert.Equal(30_000, receivable.Entries[^1].AmountCents);
        AssertLedger(receivable);

        // Un abono ya revertido no se revierte otra vez.
        Assert.Throws<DomainException>(() => receivable.RevertPayment(second));
    }

    [Fact]
    public void Saldo_NuncaQuedaNegativoNiMayorQueElOriginal()
    {
        var receivable = New(10_000);

        Assert.Throws<DomainException>(() => receivable.ApplyPayment(Guid.CreateVersion7(), 10_001));
        Assert.Throws<DomainException>(() => receivable.ApplyPayment(Guid.CreateVersion7(), 0));
        Assert.Throws<DomainException>(() => receivable.RevertPayment(Guid.CreateVersion7()));
        Assert.Equal(10_000, receivable.BalanceCents);
        Assert.Empty(receivable.Entries);
    }

    [Fact]
    public void Devolucion_SobreParteYaAbonada_LiberaElExcedente()
    {
        var receivable = New(50_000);
        receivable.ApplyPayment(Guid.CreateVersion7(), 30_000);

        receivable.ApplyReturn(Guid.CreateVersion7(), 50_000, out var excess);

        Assert.Equal(30_000, excess);
        Assert.Equal(0, receivable.BalanceCents);
        Assert.Equal(
            [ReceivableEntryType.Payment, ReceivableEntryType.Return, ReceivableEntryType.ExcessOut],
            receivable.Entries.Select(e => e.Type));
        AssertLedger(receivable);

        receivable.Cancel();
        Assert.Equal(ReceivableStatus.Cancelled, receivable.Status);
        Assert.Throws<DomainException>(() => receivable.ApplyReturn(Guid.CreateVersion7(), 1, out _));
    }

    [Fact]
    public void Excedente_AplicadoAOtraCuenta_LaPagaSinExcederSuSaldo()
    {
        var receivable = New(20_000);

        receivable.ApplyExcess(Guid.CreateVersion7(), 20_000);

        Assert.Equal(ReceivableStatus.Paid, receivable.Status);
        Assert.Throws<DomainException>(() => receivable.ApplyExcess(Guid.CreateVersion7(), 1));
        AssertLedger(receivable);
    }

    [Fact]
    public void Cancelar_ConSaldo_SeRechaza() =>
        Assert.Throws<DomainException>(() => New().Cancel());
}
