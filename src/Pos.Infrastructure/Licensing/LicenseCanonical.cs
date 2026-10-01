using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Pos.Infrastructure.Licensing;

/// <summary>
/// Contenido canónico que firma el proveedor (012, contrato 1): campos en orden fijo, UTF-8, sin espacios,
/// GUID en minúscula formato D ordenados ascendentemente.
/// </summary>
internal static class LicenseCanonical
{
    public static byte[] Build(int format, string machineId, DateTime issuedUtc, IEnumerable<Guid> modules)
    {
        var issued = issuedUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var ids = modules
            .Select(g => g.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(id => "\"" + id + "\"");
        return Encoding.UTF8.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"format\":{format},\"machineId\":{JsonSerializer.Serialize(machineId)},\"issuedUtc\":\"{issued}\",\"modules\":[{string.Join(",", ids)}]}}"));
    }
}
