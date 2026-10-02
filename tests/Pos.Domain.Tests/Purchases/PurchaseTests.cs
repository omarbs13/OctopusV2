using Pos.Domain.Common;
using Pos.Domain.Purchases;

namespace Pos.Domain.Tests.Purchases;

/// <summary>020: reglas de registro y anulación de la compra (FR-007–FR-010a, FR-017).</summary>
public sealed class PurchaseTests
{
    private static readonly DateOnly Today = new(2026, 10, 2);
    private static readonly Guid SupplierId = Guid.CreateVersion7();

    private static PurchaseLineDraft Line(long quantity, long cost, Guid? productId = null) =>
        new(productId ?? Guid.CreateVersion7(), "Refresco", "REF-1", "H87", quantity, cost);

    private static Purchase Register(IEnumerable<PurchaseLineDraft> lines, long tax = 0, DateOnly? date = null) =>
        Purchase.Register(SupplierId, "Norte", " f-100 ", date ?? Today, Today, lines, tax);

    [Fact]
    public void Registrar_CalculaImportesYNumeraLineas()
    {
        var purchase = Register([Line(5_000, 1_250), Line(2_500, 4_000)], tax: 2_600);

        Assert.Equal("f-100", purchase.InvoiceNumber);
        Assert.Equal("F-100", purchase.InvoiceKey);
        Assert.Equal(16_250, purchase.SubtotalCents);
        Assert.Equal(18_850, purchase.TotalCents);
        Assert.Equal(2, purchase.LineCount);
        Assert.Equal([1, 2], purchase.Lines.Select(l => l.LineNumber));
        Assert.Equal(PurchaseStatus.Active, purchase.Status);
    }

    [Fact]
    public void SubtotalCero_SeRechaza_PeroUnaBonificacionConOtraLineaSeAcepta()
    {
        Assert.Throws<DomainException>(() => Register([Line(1_000, 0), Line(2_000, 0)]));

        var purchase = Register([Line(1_000, 0), Line(1_000, 500)]);

        Assert.True(purchase.Lines[0].IsBonus);
        Assert.False(purchase.Lines[1].IsBonus);
        Assert.Equal(500, purchase.SubtotalCents);
    }

    [Fact]
    public void ProductoRepetido_SinLineas_ImpuestosNegativos_YFechaFutura_SeRechazan()
    {
        var product = Guid.CreateVersion7();

        Assert.Throws<DomainException>(() => Register([Line(1_000, 100, product), Line(2_000, 100, product)]));
        Assert.Throws<DomainException>(() => Register([]));
        Assert.Throws<DomainException>(() => Register([Line(1_000, 100)], tax: -1));
        Assert.Throws<DomainException>(() => Register([Line(1_000, 100)], date: Today.AddDays(1)));
        Register([Line(1_000, 100)], date: Today);
    }

    [Fact]
    public void Anular_SinMotivoConMotivoLargoODosVeces_SeRechaza()
    {
        var purchase = Register([Line(1_000, 100)]);

        Assert.Throws<DomainException>(() => purchase.Void("  ", Guid.CreateVersion7(), DateTime.UtcNow));
        Assert.Throws<DomainException>(() => purchase.Void(new string('x', 251), Guid.CreateVersion7(), DateTime.UtcNow));
        Assert.Equal(PurchaseStatus.Active, purchase.Status);

        purchase.Void("Error", Guid.CreateVersion7(), DateTime.UtcNow);

        Assert.Throws<DomainException>(() => purchase.Void("Otra vez", Guid.CreateVersion7(), DateTime.UtcNow));
    }

    [Fact]
    public void Anular_DejaAnuladaConFechaUsuarioYMotivo()
    {
        var purchase = Register([Line(1_000, 100)]);
        var user = Guid.CreateVersion7();
        var now = new DateTime(2026, 10, 2, 15, 0, 0, DateTimeKind.Utc);

        purchase.Void("  Factura equivocada ", user, now);

        Assert.Equal(PurchaseStatus.Voided, purchase.Status);
        Assert.Equal("Factura equivocada", purchase.VoidReason);
        Assert.Equal(user, purchase.VoidedBy);
        Assert.Equal(now, purchase.VoidedAt);
    }
}
