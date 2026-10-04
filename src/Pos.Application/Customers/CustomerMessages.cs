using Pos.Domain.Customers;

namespace Pos.Application.Customers;

/// <summary>Mensajes de validación de clientes, en español.</summary>
public static class CustomerMessages
{
    public const string NameRequired = "El nombre es obligatorio.";
    public const string PhoneRequired = "El teléfono es obligatorio.";
    public const string EmailInvalid = "Capture un email válido, por ejemplo nombre@dominio.com.";
    public const string LimitInvalid = "El límite debe ser mayor o igual que 0 y no exceder $999,999.99.";
    public const string CreditModeInvalid = "La modalidad de crédito no es válida.";
    public const string TaxIdDuplicate = "El RFC ya está registrado para otro cliente";

    public static readonly string NameTooLong = $"El nombre admite hasta {Customer.NameMaxLength} caracteres.";
    public static readonly string PhoneTooLong = $"El teléfono admite hasta {Customer.PhoneMaxLength} caracteres.";
    public static readonly string EmailTooLong = $"El email admite hasta {Customer.EmailMaxLength} caracteres.";
    public static readonly string TaxIdTooLong = $"El RFC admite hasta {Customer.TaxIdMaxLength} caracteres.";
}
