using Pos.Domain.Common;
using Pos.Domain.Customers;

namespace Pos.Domain.Tests.Customers;

/// <summary>014: validaciones de integridad del cliente (FR-001, FR-004).</summary>
public sealed class CustomerTests
{
    [Fact]
    public void Crear_LimiteNegativo_SeRechaza() =>
        Assert.Throws<DomainException>(() => Customer.Create("Ana", "555", null, null, -1, CreditMode.Credit));

    [Theory]
    [InlineData("ana")]
    [InlineData("ana@dominio")]
    [InlineData("@dominio.com")]
    [InlineData("ana@@dominio.com")]
    [InlineData("ana @dominio.com")]
    public void Crear_EmailSinArrobaODominio_SeRechaza(string email)
    {
        Assert.False(Customer.IsValidEmail(email));
        Assert.Throws<DomainException>(() => Customer.Create("Ana", "555", email, null, 0, CreditMode.CashOnly));
    }

    [Fact]
    public void Crear_RucRecortadoYEnMayusculas_YTextoDeBusquedaSinAcentos()
    {
        var customer = Customer.Create("  Ana Pérez ", " 555-1234 ", "ana@correo.com", "  abc123  ", 100_000, CreditMode.Credit);

        Assert.Equal("Ana Pérez", customer.Name);
        Assert.Equal("ABC123", customer.TaxId);
        Assert.Equal("ana perez 555-1234 abc123", customer.SearchText);
        Assert.True(customer.IsActive);
        Assert.True(customer.CanBuyOnCredit);
        Assert.Null(Customer.NormalizeTaxId("   "));
    }

    [Fact]
    public void Desactivar_ConSaldo_SeRechaza_YSinSaldoProcede()
    {
        var customer = Customer.Create("Ana", "555", null, null, 100_000, CreditMode.Credit);

        Assert.Throws<DomainException>(() => customer.Deactivate(1));
        Assert.True(customer.IsActive);

        customer.Deactivate(0);
        Assert.False(customer.IsActive);
        Assert.False(customer.CanBuyOnCredit);
    }

    [Fact]
    public void CambiarCredito_PermiteLimiteMenorQueElSaldo_YSoloEfectivoNoCompraACredito()
    {
        var customer = Customer.Create("Ana", "555", null, null, 100_000, CreditMode.Credit);

        customer.ChangeCredit(0, CreditMode.CashOnly);

        Assert.Equal(0, customer.CreditLimitCents);
        Assert.False(customer.CanBuyOnCredit);
    }
}
