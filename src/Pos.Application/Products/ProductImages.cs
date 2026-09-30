using Pos.Domain.Products;

namespace Pos.Application.Products;

/// <summary>Aplica un <see cref="ProductImageChange"/> al agregado; lo comparten el alta y la edición.</summary>
internal static class ProductImages
{
    public static void Apply(Product product, ProductImageChange? change)
    {
        switch (change)
        {
            case ProductImageChange.Replace replace:
                product.SetImage(replace.Image.Content, replace.Image.Thumbnail, replace.Image.Width, replace.Image.Height);
                break;
            case ProductImageChange.Remove:
                product.RemoveImage();
                break;
        }
    }
}
