namespace Pos.Application.Products;

/// <summary>Qué hacer con la imagen al guardar un producto (003, FR-025 y FR-027).</summary>
public abstract record ProductImageChange
{
    private protected ProductImageChange()
    {
    }

    /// <summary>No tocar la imagen actual.</summary>
    public static ProductImageChange KeepCurrent { get; } = new Keep();

    public sealed record Keep : ProductImageChange;

    public sealed record Replace(PreparedProductImage Image) : ProductImageChange;

    public sealed record Remove : ProductImageChange;
}
