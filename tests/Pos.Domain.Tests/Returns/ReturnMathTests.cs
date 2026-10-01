using Pos.Domain.Common;
using Pos.Domain.Returns;

namespace Pos.Domain.Tests.Returns;

/// <summary>013, research §3: importes exactos al centavo (SC-004).</summary>
public sealed class ReturnMathTests
{
    [Fact]
    public void LineRefund_DevolverTodoEnPartesSumaExactamenteElImporteDeLaLinea()
    {
        // 3 unidades por $10.00 → $10.00 en total: 1/3 + 1/3 + 1/3 no pierde ni sobra un centavo.
        long returned = 0;
        long amount = 0;
        for (var i = 0; i < 3; i++)
        {
            amount += ReturnMath.LineRefund(1000, 3000, returned, 1000);
            returned += 1000;
        }

        Assert.Equal(1000, amount);
    }

    [Fact]
    public void LineRefund_AdmiteCantidadesFraccionariasYRedondeaMitadHaciaArriba()
    {
        // 2.500 kg a $9.99 = 2498 centavos (redondeado una vez); devolver 0.500 kg = 2498 × 0.5 / 2.5 = 499.6 → 500.
        Assert.Equal(500, ReturnMath.LineRefund(2498, 2500, 0, 500));
        // 1/2 exacto de 1 centavo se redondea hacia arriba.
        Assert.Equal(1, ReturnMath.LineRefund(1, 2000, 0, 1000));
    }

    [Fact]
    public void LineRefund_RechazaExcederLoVendido()
    {
        Assert.Throws<DomainException>(() => ReturnMath.LineRefund(1000, 3000, 2000, 2000));
        Assert.Throws<DomainException>(() => ReturnMath.LineRefund(1000, 3000, 0, 0));
    }

    [Fact]
    public void Allocate_RepartePorRestoMayorConSumaExacta()
    {
        // $100.01 devueltos sobre pagos de $60.00 efectivo y $40.01 tarjeta.
        var parts = ReturnMath.Allocate(10_001, [6_000, 4_001]);

        Assert.Equal(10_001, parts.Sum());
        Assert.Equal([6_000, 4_001], parts);
    }

    [Fact]
    public void Allocate_RepartoProporcionalYEmpatesPorOrdenDeCaptura()
    {
        // 1 centavo entre dos pagos iguales: se queda con el primero capturado.
        Assert.Equal([1, 0], ReturnMath.Allocate(1, [500, 500]));
        // Mitad de una venta mixta 70/30.
        Assert.Equal([3_500, 1_500], ReturnMath.Allocate(5_000, [7_000, 3_000]));
    }

    [Fact]
    public void Allocate_NuncaExcedeLoPagadoDeCadaFormaTrasDevolucionesPrevias()
    {
        // Ya se devolvió casi todo el efectivo: quedan $1.00 de efectivo y $50.00 de tarjeta.
        var parts = ReturnMath.Allocate(5_100, [100, 5_000]);

        Assert.Equal([100, 5_000], parts);
        Assert.Throws<DomainException>(() => ReturnMath.Allocate(5_101, [100, 5_000]));
    }

    [Fact]
    public void Allocate_UnSoloPagoRecibeTodo()
    {
        Assert.Equal([2_345], ReturnMath.Allocate(2_345, [10_000]));
    }
}
