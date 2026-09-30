using Pos.Application.Abstractions;
using Pos.Domain.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Products.PrepareProductImage;

/// <summary>
/// Valida y optimiza la imagen que eligió el operador, sin guardar nada (003, FR-022 a FR-024). El
/// resultado se muestra como vista previa y viaja en el comando de guardado.
/// </summary>
public sealed class PrepareProductImageHandler
{
    private readonly IAccessControl _access;
    private readonly IImageProcessor _processor;

    public PrepareProductImageHandler(IAccessControl access, IImageProcessor processor)
    {
        _access = access;
        _processor = processor;
    }

    public async Task<Result<PreparedProductImage>> HandleAsync(PrepareProductImageCommand command, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<PreparedProductImage>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(command);

        // El tamaño se revisa antes de leer: un archivo enorme nunca se carga en memoria.
        if (command.Length > Product.ImageMaxBytes)
        {
            return Result.Failure<PreparedProductImage>(new InvalidImage(InvalidImageReason.TooLarge));
        }

        var processed = await Task.Run(() => _processor.Process(command.Content), cancellationToken);
        if (processed.Error is { } reason)
        {
            return Result.Failure<PreparedProductImage>(new InvalidImage(reason));
        }

        return Result.Success(new PreparedProductImage(processed.Content!, processed.Thumbnail!, processed.Width, processed.Height));
    }

    /// <summary>Mensaje para el operador según el motivo del rechazo.</summary>
    public static string MessageFor(InvalidImageReason reason) => reason switch
    {
        InvalidImageReason.TooLarge => ProductMessages.ImageTooLarge,
        InvalidImageReason.UnsupportedFormat => ProductMessages.ImageUnsupportedFormat,
        InvalidImageReason.DimensionsTooLarge => ProductMessages.ImageDimensionsTooLarge,
        _ => ProductMessages.ImageCorrupt,
    };
}
