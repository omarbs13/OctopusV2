using Pos.Domain.Common;

namespace Pos.Domain.Suppliers;

/// <summary>
/// Proveedor de mercancía (agregado, 020, research §9). No se borra: solo se desactiva (FR-006). Las
/// compras guardan su propio nombre del proveedor (FR-016). Los campos de auditoría los asigna la persistencia.
/// </summary>
public sealed class Supplier
{
    public const int NameMaxLength = 150;
    public const int TaxIdMaxLength = 20;
    public const int PhoneMaxLength = 30;
    public const int EmailMaxLength = 254;
    public const int AddressMaxLength = 300;
    public const int SearchTextMaxLength = 200;
    public const int MinCreditDays = 1;
    public const int MaxCreditDays = 365;

    private Supplier()
    {
        Name = string.Empty;
        SearchText = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Recortado; puede repetirse entre proveedores (FR-003).</summary>
    public string Name { get; private set; }

    /// <summary>RFC, recortado y en mayúsculas; único entre todos los proveedores si no es nulo.</summary>
    public string? TaxId { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? Address { get; private set; }

    public PaymentTerms PaymentTerms { get; private set; }

    /// <summary>Nulo en contado; de 1 a 365 en crédito.</summary>
    public int? CreditDays { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Nombre y RFC sin acentos y en minúsculas, para la búsqueda (FR-004).</summary>
    public string SearchText { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    /// <summary>Estándar del Principio IV; desactivar usa <see cref="IsActive"/>, no el borrado.</summary>
    public DateTime? DeletedAt { get; private set; }

    public int Version { get; private set; }

    public static Supplier Create(
        string name,
        string? taxId,
        string? phone,
        string? email,
        string? address,
        PaymentTerms paymentTerms,
        int? creditDays)
    {
        var supplier = new Supplier
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            Version = 1,
        };
        supplier.Update(name, taxId, phone, email, address, paymentTerms, creditDays);
        return supplier;
    }

    /// <summary>Cambia los datos y recalcula el texto de búsqueda; en contado limpia los días de crédito.</summary>
    public void Update(
        string name,
        string? taxId,
        string? phone,
        string? email,
        string? address,
        PaymentTerms paymentTerms,
        int? creditDays)
    {
        var normalizedName = (name ?? string.Empty).Trim();
        var normalizedTaxId = NormalizeTaxId(taxId);
        var normalizedPhone = NormalizeOptional(phone);
        var normalizedEmail = NormalizeOptional(email);
        var normalizedAddress = NormalizeOptional(address);

        if (normalizedName.Length is 0 or > NameMaxLength)
        {
            throw new DomainException($"El nombre es obligatorio y admite hasta {NameMaxLength} caracteres.");
        }

        if (normalizedTaxId is { Length: > TaxIdMaxLength })
        {
            throw new DomainException($"El RFC admite hasta {TaxIdMaxLength} caracteres.");
        }

        if (normalizedPhone is { Length: > PhoneMaxLength })
        {
            throw new DomainException($"El teléfono admite hasta {PhoneMaxLength} caracteres.");
        }

        if (normalizedEmail is not null && (normalizedEmail.Length > EmailMaxLength || !EmailAddress.IsValid(normalizedEmail)))
        {
            throw new DomainException("El email no tiene un formato válido.");
        }

        if (normalizedAddress is { Length: > AddressMaxLength })
        {
            throw new DomainException($"La dirección admite hasta {AddressMaxLength} caracteres.");
        }

        if (!Enum.IsDefined(paymentTerms))
        {
            throw new DomainException("Las condiciones de pago no son válidas.");
        }

        if (paymentTerms == PaymentTerms.Credit && creditDays is not (>= MinCreditDays and <= MaxCreditDays))
        {
            throw new DomainException($"Los días de crédito son obligatorios con crédito, de {MinCreditDays} a {MaxCreditDays}.");
        }

        Name = normalizedName;
        TaxId = normalizedTaxId;
        Phone = normalizedPhone;
        Email = normalizedEmail;
        Address = normalizedAddress;
        PaymentTerms = paymentTerms;
        CreditDays = paymentTerms == PaymentTerms.Credit ? creditDays : null;
        SearchText = BuildSearchText(normalizedName, normalizedTaxId);
    }

    /// <summary>Siempre se permite (FR-006).</summary>
    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Recorta y pasa a mayúsculas invariantes; vacío o solo espacios se convierte en nulo.</summary>
    public static string? NormalizeTaxId(string? taxId) =>
        string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string BuildSearchText(string name, string? taxId)
    {
        var text = TextNormalizer.ForSearch(taxId is null ? name : $"{name} {taxId}");
        return text.Length <= SearchTextMaxLength ? text : text[..SearchTextMaxLength];
    }
}
