namespace Pos.Application.Products;

/// <summary>
/// Imagen ya validada y optimizada, lista para guardarse con el producto. Solo
/// <see cref="PrepareProductImage.PrepareProductImageHandler"/> la construye, así que un comando
/// nunca trae bytes sin validar (003, research §7).
/// </summary>
public sealed class PreparedProductImage
{
    internal PreparedProductImage(byte[] content, byte[] thumbnail, int width, int height)
    {
        Content = content;
        Thumbnail = thumbnail;
        Width = width;
        Height = height;
    }

    public byte[] Content { get; }

    public byte[] Thumbnail { get; }

    public int Width { get; }

    public int Height { get; }
}
