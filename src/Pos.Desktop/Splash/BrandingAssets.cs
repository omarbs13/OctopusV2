using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Pos.Application.Abstractions;
using Serilog;

namespace Pos.Desktop.Splash;

/// <summary>Imagen de marca de la aplicación.</summary>
public interface IBrandingAssets
{
    /// <summary>Logotipo del cliente si existe y es válido; si no, el predeterminado.</summary>
    IImage? Logo { get; }
}

/// <summary>
/// Usa <c>logo.png</c> de la carpeta de datos si existe y es un PNG válido (FR-004); si no, el
/// logotipo vectorial predeterminado de los recursos.
/// </summary>
internal sealed class BrandingAssets : IBrandingAssets
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly Lazy<IImage?> _logo;

    public BrandingAssets(IAppPaths paths, ILogger logger)
    {
        _logo = new Lazy<IImage?>(() => LoadLogo(paths.LogoFile, logger));
    }

    public IImage? Logo => _logo.Value;

    /// <summary>Ruta del logotipo del cliente si es utilizable; nulo para usar el predeterminado.</summary>
    public static string? ResolveCustomLogo(string logoFile, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (!File.Exists(logoFile))
        {
            return null;
        }

        try
        {
            var header = new byte[PngSignature.Length];
            using var stream = File.OpenRead(logoFile);
            if (stream.Read(header) == header.Length && header.AsSpan().SequenceEqual(PngSignature))
            {
                return logoFile;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Warning(ex, "No se pudo leer el logotipo {LogoFile}; se usa el predeterminado", logoFile);
            return null;
        }

        logger.Warning("El logotipo {LogoFile} no es un PNG válido; se usa el predeterminado", logoFile);
        return null;
    }

    private static IImage? LoadLogo(string logoFile, ILogger logger)
    {
        if (ResolveCustomLogo(logoFile, logger) is { } custom)
        {
            try
            {
                return new Bitmap(custom);
            }
#pragma warning disable CA1031 // Un logotipo dañado nunca debe impedir el arranque.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.Warning(ex, "No se pudo decodificar el logotipo {LogoFile}; se usa el predeterminado", logoFile);
            }
        }

        return Avalonia.Application.Current?.TryFindResource("Logo.Default", out var resource) == true
            ? resource as IImage
            : null;
    }
}
