namespace Pos.Application.Products.PrepareProductImage;

/// <summary>Archivo seleccionado por el operador; <paramref name="Length"/> es su tamaño en bytes.</summary>
public sealed record PrepareProductImageCommand(Stream Content, long Length);
