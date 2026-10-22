using Pos.Application.Products.ListUnitsOfMeasure;

namespace Pos.Application.Tests.Products;

public class ListUnitsOfMeasureHandlerTests
{
    [Fact]
    public void DevuelveElCatalogoFijoEnOrden()
    {
        var units = new ListUnitsOfMeasureHandler().Handle();

        Assert.Equal(
            ["Pieza", "Kilogramo", "Gramo", "Litro", "Mililitro", "Metro", "Caja", "Paquete"],
            units.Select(u => u.Name));
        Assert.Equal("H87", units[0].Code);
    }
}
