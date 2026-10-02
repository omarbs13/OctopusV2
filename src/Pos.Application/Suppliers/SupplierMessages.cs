using Pos.Domain.Suppliers;

namespace Pos.Application.Suppliers;

/// <summary>Mensajes de validación de proveedores, en español.</summary>
public static class SupplierMessages
{
    public const string NameRequired = "El nombre es obligatorio.";
    public const string EmailInvalid = "Capture un email válido, por ejemplo nombre@dominio.com.";
    public const string PaymentTermsInvalid = "Las condiciones de pago no son válidas.";

    public static readonly string NameTooLong = $"El nombre admite hasta {Supplier.NameMaxLength} caracteres.";
    public static readonly string TaxIdTooLong = $"El RUC admite hasta {Supplier.TaxIdMaxLength} caracteres.";
    public static readonly string PhoneTooLong = $"El teléfono admite hasta {Supplier.PhoneMaxLength} caracteres.";
    public static readonly string EmailTooLong = $"El email admite hasta {Supplier.EmailMaxLength} caracteres.";
    public static readonly string AddressTooLong = $"La dirección admite hasta {Supplier.AddressMaxLength} caracteres.";

    public static readonly string CreditDaysInvalid =
        $"Los días de crédito son obligatorios con crédito, de {Supplier.MinCreditDays} a {Supplier.MaxCreditDays}.";

    public static string TaxIdInUse(string supplierName) => $"El RUC ya está registrado para el proveedor {supplierName}";
}
