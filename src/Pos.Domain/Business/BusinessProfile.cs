using Pos.Domain.Common;

namespace Pos.Domain.Business;

/// <summary>
/// Datos del negocio que aparecen en el ticket (006). Una sola fila por instalación. Los campos de
/// auditoría los asigna la persistencia.
/// </summary>
public sealed class BusinessProfile
{
    public const int TradeNameMaxLength = 80;
    public const int AddressMaxLength = 200;
    public const int PhoneMaxLength = 30;
    public const int TaxIdMaxLength = 13;
    public const int FooterMaxLength = 200;

    /// <summary>Tamaño máximo del archivo de logotipo que se acepta (1 MB).</summary>
    public const long LogoMaxBytes = 1024 * 1024;

    private BusinessProfile()
    {
        TradeName = string.Empty;
        Address = string.Empty;
        Phone = string.Empty;
    }

    public Guid Id { get; private set; }

    public string TradeName { get; private set; }

    public string Address { get; private set; }

    public string Phone { get; private set; }

    public string? TaxId { get; private set; }

    /// <summary>Logotipo ya optimizado por el procesador de imágenes, o nulo.</summary>
    public byte[]? Logo { get; private set; }

    public string? FooterMessage { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public int Version { get; private set; }

    public static BusinessProfile Create(
        string tradeName,
        string address,
        string phone,
        string? taxId,
        string? footerMessage)
    {
        var profile = new BusinessProfile { Id = Guid.CreateVersion7(), Version = 1 };
        profile.Apply(tradeName, address, phone, taxId, footerMessage);
        return profile;
    }

    public void Update(string tradeName, string address, string phone, string? taxId, string? footerMessage) =>
        Apply(tradeName, address, phone, taxId, footerMessage);

    public void SetLogo(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            throw new DomainException("El logotipo no puede estar vacío.");
        }

        Logo = content;
    }

    public void RemoveLogo() => Logo = null;

    /// <summary>Recorta los extremos; nulo se trata como vacío.</summary>
    public static string NormalizeText(string? text) => (text ?? string.Empty).Trim();

    /// <summary>Sin espacios y en mayúsculas; vacío se convierte en nulo.</summary>
    public static string? NormalizeTaxId(string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId))
        {
            return null;
        }

        return new string([.. taxId.Where(c => !char.IsWhiteSpace(c))]).ToUpperInvariant();
    }

    /// <summary>Recorta los extremos y conserva los saltos de línea internos; vacío se convierte en nulo.</summary>
    public static string? NormalizeFooter(string? footer) =>
        string.IsNullOrWhiteSpace(footer) ? null : footer.Trim();

    public static bool IsValidTradeName(string normalized) => normalized.Length is > 0 and <= TradeNameMaxLength;

    public static bool IsValidAddress(string normalized) => normalized.Length is > 0 and <= AddressMaxLength;

    public static bool IsValidPhone(string normalized) => normalized.Length is > 0 and <= PhoneMaxLength;

    public static bool IsValidTaxId(string? normalized) => normalized is null || normalized.Length <= TaxIdMaxLength;

    public static bool IsValidFooter(string? normalized) => normalized is null || normalized.Length <= FooterMaxLength;

    private void Apply(string tradeName, string address, string phone, string? taxId, string? footerMessage)
    {
        var name = NormalizeText(tradeName);
        var addr = NormalizeText(address);
        var tel = NormalizeText(phone);
        var rfc = NormalizeTaxId(taxId);
        var footer = NormalizeFooter(footerMessage);

        if (!IsValidTradeName(name))
        {
            throw new DomainException($"El nombre comercial es obligatorio y admite hasta {TradeNameMaxLength} caracteres.");
        }

        if (!IsValidAddress(addr))
        {
            throw new DomainException($"La dirección es obligatoria y admite hasta {AddressMaxLength} caracteres.");
        }

        if (!IsValidPhone(tel))
        {
            throw new DomainException($"El teléfono es obligatorio y admite hasta {PhoneMaxLength} caracteres.");
        }

        if (!IsValidTaxId(rfc))
        {
            throw new DomainException($"El RFC admite hasta {TaxIdMaxLength} caracteres.");
        }

        if (!IsValidFooter(footer))
        {
            throw new DomainException($"El mensaje de pie admite hasta {FooterMaxLength} caracteres.");
        }

        TradeName = name;
        Address = addr;
        Phone = tel;
        TaxId = rfc;
        FooterMessage = footer;
    }
}
