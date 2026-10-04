using Pos.Domain.Common;

namespace Pos.Domain.Customers;

/// <summary>
/// Cliente registrado (agregado, 014). El saldo pendiente no se guarda aquí: es la suma de sus cuentas
/// por cobrar pendientes (FR-015). Los campos de auditoría los asigna la persistencia.
/// </summary>
public sealed class Customer
{
    public const int NameMaxLength = 120;
    public const int PhoneMaxLength = 30;
    public const int EmailMaxLength = 254;
    public const int TaxIdMaxLength = 20;
    public const int SearchTextMaxLength = 200;

    private Customer()
    {
        Name = string.Empty;
        Phone = string.Empty;
        SearchText = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Phone { get; private set; }

    public string? Email { get; private set; }

    /// <summary>RFC, recortado y en mayúsculas; único entre todos los clientes si no es nulo.</summary>
    public string? TaxId { get; private set; }

    public long CreditLimitCents { get; private set; }

    public CreditMode CreditMode { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Nombre, teléfono y RFC sin acentos y en minúsculas, para la búsqueda (research §10).</summary>
    public string SearchText { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    /// <summary>Estándar del Principio IV; desactivar usa <see cref="IsActive"/>, no el borrado.</summary>
    public DateTime? DeletedAt { get; private set; }

    public int Version { get; private set; }

    /// <summary>Solo un cliente activo con crédito puede comprar a crédito (Historia 2, escenario 4).</summary>
    public bool CanBuyOnCredit => IsActive && CreditMode == CreditMode.Credit;

    public static Customer Create(
        string name,
        string phone,
        string? email,
        string? taxId,
        long creditLimitCents,
        CreditMode creditMode)
    {
        var customer = new Customer
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            Version = 1,
        };
        customer.Update(name, phone, email, taxId);
        customer.ChangeCredit(creditLimitCents, creditMode);
        return customer;
    }

    /// <summary>Cambia los datos de contacto y recalcula el texto de búsqueda.</summary>
    public void Update(string name, string phone, string? email, string? taxId)
    {
        var normalizedName = Trim(name);
        var normalizedPhone = Trim(phone);
        var normalizedEmail = NormalizeOptional(email);
        var normalizedTaxId = NormalizeTaxId(taxId);

        if (normalizedName.Length is 0 or > NameMaxLength)
        {
            throw new DomainException($"El nombre es obligatorio y admite hasta {NameMaxLength} caracteres.");
        }

        if (normalizedPhone.Length is 0 or > PhoneMaxLength)
        {
            throw new DomainException($"El teléfono es obligatorio y admite hasta {PhoneMaxLength} caracteres.");
        }

        if (normalizedEmail is not null && (normalizedEmail.Length > EmailMaxLength || !IsValidEmail(normalizedEmail)))
        {
            throw new DomainException("El email no tiene un formato válido.");
        }

        if (normalizedTaxId is { Length: > TaxIdMaxLength })
        {
            throw new DomainException($"El RFC admite hasta {TaxIdMaxLength} caracteres.");
        }

        Name = normalizedName;
        Phone = normalizedPhone;
        Email = normalizedEmail;
        TaxId = normalizedTaxId;
        SearchText = BuildSearchText(normalizedName, normalizedPhone, normalizedTaxId);
    }

    /// <summary>Asigna límite y modalidad. Se permite un límite menor que el saldo actual (caso límite).</summary>
    public void ChangeCredit(long creditLimitCents, CreditMode creditMode)
    {
        if (creditLimitCents is < 0 or > Money.MaxCents)
        {
            throw new DomainException("El límite de crédito debe ser mayor o igual que 0 y no exceder el máximo permitido.");
        }

        if (!Enum.IsDefined(creditMode))
        {
            throw new DomainException("La modalidad de crédito no es válida.");
        }

        CreditLimitCents = creditLimitCents;
        CreditMode = creditMode;
    }

    /// <summary>Solo se desactiva con saldo 0 (FR-004).</summary>
    public void Deactivate(long balanceCents)
    {
        if (balanceCents > 0)
        {
            throw new DomainException("El cliente tiene saldo pendiente; no se puede desactivar.");
        }

        IsActive = false;
    }

    public void Activate() => IsActive = true;

    /// <summary>Formato básico de email; la regla vive en <see cref="EmailAddress.IsValid"/>.</summary>
    public static bool IsValidEmail(string? email) => EmailAddress.IsValid(email);

    /// <summary>Recorta y pasa a mayúsculas invariantes; vacío o solo espacios se convierte en nulo.</summary>
    public static string? NormalizeTaxId(string? taxId) =>
        string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim().ToUpperInvariant();

    private static string Trim(string? text) => (text ?? string.Empty).Trim();

    private static string? NormalizeOptional(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string BuildSearchText(string name, string phone, string? taxId)
    {
        var text = TextNormalizer.ForSearch(string.Join(' ', new[] { name, phone, taxId }.Where(t => t is not null)));
        return text.Length <= SearchTextMaxLength ? text : text[..SearchTextMaxLength];
    }
}
