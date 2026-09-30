using Pos.Domain.Products;

namespace Pos.Application.Products.ListUnitsOfMeasure;

/// <summary>Catálogo fijo de unidades de medida (003, FR-017). Sale de Domain; no consulta la base.</summary>
public sealed class ListUnitsOfMeasureHandler
{
    private readonly IReadOnlyList<UnitOfMeasureDto> _units =
        [.. UnitOfMeasure.All.OrderBy(u => u.SortOrder).Select(u => new UnitOfMeasureDto(u.Code, u.Name))];

    public IReadOnlyList<UnitOfMeasureDto> Handle() => _units;
}

public sealed record UnitOfMeasureDto(string Code, string Name);
