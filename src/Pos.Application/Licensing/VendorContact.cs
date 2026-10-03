namespace Pos.Application.Licensing;

/// <summary>Datos de contacto del proveedor que se muestran en los mensajes de licencia.</summary>
public sealed record VendorContact(string Phone, string Email)
{
    // TODO(025, T093): reemplazar con el teléfono y el correo reales del proveedor antes de liberar a producción;
    // se muestran en los mensajes de bloqueo y en "Ayuda > Licencia".
    public static VendorContact Default { get; } = new("[teléfono]", "[email]");
}
