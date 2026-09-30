using Pos.Domain.Common;

namespace Pos.Domain.Products;

/// <summary>
/// Imagen de un producto, ya optimizada (003, FR-021 a FR-024). Pertenece al agregado Producto:
/// solo se crea, reemplaza o quita desde <see cref="Product"/>. Los campos de auditoría los asigna
/// la persistencia.
/// </summary>
public sealed class ProductImage
{
    public const string WebpContentType = "image/webp";
    public const int ContentTypeMaxLength = 20;

    private ProductImage()
    {
        Content = [];
        Thumbnail = [];
        ContentType = WebpContentType;
    }

    public Guid ProductId { get; private set; }

    /// <summary>Imagen optimizada (WEBP, lado mayor de hasta <see cref="Product.ImageMaxSide"/> px).</summary>
    public byte[] Content { get; private set; }

    /// <summary>Miniatura para el listado (WEBP, lado mayor de hasta <see cref="Product.ThumbnailMaxSide"/> px).</summary>
    public byte[] Thumbnail { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public string ContentType { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    internal static ProductImage Create(Guid productId, byte[] content, byte[] thumbnail, int width, int height)
    {
        var image = new ProductImage { ProductId = productId };
        image.Replace(content, thumbnail, width, height);
        return image;
    }

    internal void Replace(byte[] content, byte[] thumbnail, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(thumbnail);
        if (content.Length == 0 || thumbnail.Length == 0)
        {
            throw new DomainException("La imagen y su miniatura no pueden estar vacías.");
        }

        if (width is < 1 or > Product.ImageMaxSide || height is < 1 or > Product.ImageMaxSide)
        {
            throw new DomainException($"La imagen debe medir entre 1 y {Product.ImageMaxSide} px por lado.");
        }

        Content = content;
        Thumbnail = thumbnail;
        Width = width;
        Height = height;
        ContentType = WebpContentType;
    }
}
