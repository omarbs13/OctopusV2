namespace Pos.Application.Business;

/// <summary>Mensajes de los datos del negocio, en español.</summary>
public static class BusinessMessages
{
    public const string TradeNameRequired = "Capture el nombre comercial.";
    public const string TradeNameTooLong = "El nombre comercial admite hasta 80 caracteres.";
    public const string AddressRequired = "Capture la dirección.";
    public const string AddressTooLong = "La dirección admite hasta 200 caracteres.";
    public const string PhoneRequired = "Capture el teléfono.";
    public const string PhoneTooLong = "El teléfono admite hasta 30 caracteres.";
    public const string TaxIdTooLong = "El RFC admite hasta 13 caracteres.";
    public const string FooterTooLong = "El mensaje de pie admite hasta 200 caracteres.";
    public const string LogoTooLarge = "El logotipo debe pesar 1 MB o menos.";
    public const string LogoInvalid = "El logotipo debe ser una imagen JPG, PNG o WEBP válida.";
}
