using Pos.Domain.Common;
using Pos.Domain.Suppliers;

namespace Pos.Domain.Tests.Suppliers;

/// <summary>020: validaciones de integridad del proveedor (FR-001–FR-003).</summary>
public sealed class SupplierTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(366)]
    public void Credito_SinDiasOFueraDeRango_SeRechaza(int? days) =>
        Assert.Throws<DomainException>(() => Supplier.Create("Norte", null, null, null, null, PaymentTerms.Credit, days));

    [Fact]
    public void Contado_NoPideDias_YPasarAContadoLimpiaLosDias()
    {
        var cash = Supplier.Create("Norte", null, null, null, null, PaymentTerms.Cash, null);
        Assert.Null(cash.CreditDays);

        var supplier = Supplier.Create("Norte", null, null, null, null, PaymentTerms.Credit, 30);
        Assert.Equal(30, supplier.CreditDays);

        supplier.Update("Norte", null, null, null, null, PaymentTerms.Cash, 30);

        Assert.Equal(PaymentTerms.Cash, supplier.PaymentTerms);
        Assert.Null(supplier.CreditDays);
    }

    [Fact]
    public void Ruc_SeGuardaRecortadoYEnMayusculas_YVacioComoNulo()
    {
        var supplier = Supplier.Create(" Distribuidora del Norte ", " abc123 ", null, null, null, PaymentTerms.Cash, null);

        Assert.Equal("Distribuidora del Norte", supplier.Name);
        Assert.Equal("ABC123", supplier.TaxId);
        Assert.Equal("distribuidora del norte abc123", supplier.SearchText);
        Assert.True(supplier.IsActive);
        Assert.Null(Supplier.Create("Sur", "   ", null, null, null, PaymentTerms.Cash, null).TaxId);
    }

    [Fact]
    public void EmailInvalido_SeRechaza() =>
        Assert.Throws<DomainException>(() => Supplier.Create("Norte", null, null, "correo@", null, PaymentTerms.Cash, null));

    [Fact]
    public void NombreVacioOLargo_SeRechaza()
    {
        Assert.Throws<DomainException>(() => Supplier.Create("  ", null, null, null, null, PaymentTerms.Cash, null));
        Assert.Throws<DomainException>(() => Supplier.Create(new string('a', 151), null, null, null, null, PaymentTerms.Cash, null));
        Supplier.Create(new string('a', 150), null, null, null, null, PaymentTerms.Cash, null);
    }
}
