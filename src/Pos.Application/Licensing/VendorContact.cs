namespace Pos.Application.Licensing;

/// <summary>Datos de contacto del proveedor que se muestran en los mensajes de licencia.</summary>
public sealed record VendorContact(string Phone, string Email)
{
    // Pendiente: datos reales del proveedor.
    public static VendorContact Default { get; } = new("[teléfono]", "[email]");
}
