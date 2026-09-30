namespace Pos.Application.Business.SaveBusinessProfile;

/// <summary>Qué hacer con el logotipo al guardar los datos del negocio.</summary>
public abstract record LogoChange
{
    private protected LogoChange()
    {
    }

    public static LogoChange KeepCurrent { get; } = new Keep();

    public sealed record Keep : LogoChange;

    public sealed record Remove : LogoChange;

    /// <summary>Archivo elegido; <paramref name="Length"/> es su tamaño en bytes.</summary>
    public sealed record Replace(Stream Content, long Length) : LogoChange;
}

public sealed record SaveBusinessProfileCommand(
    string TradeName,
    string Address,
    string Phone,
    string? TaxId,
    string? FooterMessage,
    LogoChange Logo);
