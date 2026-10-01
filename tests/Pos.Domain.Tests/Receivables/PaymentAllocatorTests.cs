using Pos.Domain.Common;
using Pos.Domain.Receivables;

namespace Pos.Domain.Tests.Receivables;

/// <summary>014, FR-010 y FR-011: reparto de la más antigua a la más reciente.</summary>
public sealed class PaymentAllocatorTests
{
    private static readonly DateTime Day1 = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Fifo_CubreLaMasAntiguaAntesDeLaSiguiente()
    {
        var older = new PendingBalance(Guid.CreateVersion7(), Day1, 20_000);
        var newer = new PendingBalance(Guid.CreateVersion7(), Day1.AddDays(1), 50_000);

        // El orden de entrada no importa: decide la fecha de la venta.
        var result = PaymentAllocator.Allocate(30_000, [newer, older]);

        Assert.Equal([new Allocation(older.ReceivableId, 20_000), new Allocation(newer.ReceivableId, 10_000)], result);
    }

    [Fact]
    public void MismaFecha_DesempataPorId()
    {
        var first = new PendingBalance(Guid.Parse("00000000-0000-7000-8000-000000000001"), Day1, 100);
        var second = new PendingBalance(Guid.Parse("00000000-0000-7000-8000-000000000002"), Day1, 100);

        var result = PaymentAllocator.Allocate(100, [second, first]);

        Assert.Equal(first.ReceivableId, Assert.Single(result).ReceivableId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70_001)]
    public void MontoCeroONegativoOMayorQueLosSaldos_Lanza(long amount)
    {
        PendingBalance[] pending = [new(Guid.CreateVersion7(), Day1, 20_000), new(Guid.CreateVersion7(), Day1, 50_000)];

        Assert.Throws<DomainException>(() => PaymentAllocator.Allocate(amount, pending));
    }
}
