using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Pos.Infrastructure.Licensing;

/// <summary>Contenido canónico que firma el proveedor (011, contrato 2): campos en orden fijo, UTF-8, sin espacios.</summary>
internal static class LicenseCanonical
{
    public static byte[] Build(int format, string machineId, DateTime issuedUtc, DateOnly? validUntil)
    {
        var issued = issuedUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var until = validUntil is { } date
            ? JsonSerializer.Serialize(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            : "null";
        return Encoding.UTF8.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"format\":{format},\"machineId\":{JsonSerializer.Serialize(machineId)},\"issuedUtc\":\"{issued}\",\"validUntil\":{until}}}"));
    }
}
