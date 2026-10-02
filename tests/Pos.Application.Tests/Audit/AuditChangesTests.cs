using Pos.Application.Audit;

namespace Pos.Application.Tests.Audit;

/// <summary>018 (research §2): el comparador produce solo los campos distintos, en el orden de la instantánea.</summary>
public sealed class AuditChangesTests
{
    private static readonly AuditField[] Before =
    [
        new("Nombre", "Coca-Cola 600 ml"),
        new("Precio", "$25.00"),
        new("Categoría", "Bebidas"),
        new("Código de barras", null),
    ];

    [Fact]
    public void Compare_SoloLosCamposDistintosEnOrden()
    {
        AuditField[] after =
        [
            new("Nombre", "Coca-Cola 600 ml"),
            new("Precio", "$28.50"),
            new("Categoría", "Refrescos"),
            new("Código de barras", null),
        ];

        var changes = AuditChanges.Compare(Before, after);

        Assert.Equal(["Precio", "Categoría"], changes.Select(c => c.Field));
        Assert.Equal(("$25.00", "$28.50"), (changes[0].Before, changes[0].After));
        Assert.Equal(("Bebidas", "Refrescos"), (changes[1].Before, changes[1].After));
    }

    [Fact]
    public void Created_TodosLosCamposConValorConAntesNulo()
    {
        var changes = AuditChanges.Created(Before);

        Assert.Equal(["Nombre", "Precio", "Categoría"], changes.Select(c => c.Field));
        Assert.All(changes, c => Assert.Null(c.Before));
    }

    [Fact]
    public void InstantaneasIguales_ListaVacia() =>
        Assert.Empty(AuditChanges.Compare(Before, [.. Before]));
}
