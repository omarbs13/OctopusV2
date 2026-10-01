using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Pos.Infrastructure.Licensing;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>Emisor de apoyo: firma licencias con un par de claves de prueba (la emisión real está fuera del repositorio).</summary>
internal sealed class TestLicenseIssuer : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

    public void Dispose() => _key.Dispose();

    public string Issue(string directory, string machineId, DateTime issuedUtc, DateOnly? validUntil, bool tamper = false)
    {
        var signature = _key.SignData(LicenseCanonical.Build(1, machineId, issuedUtc, validUntil), HashAlgorithmName.SHA256);
        var file = new
        {
            format = 1,
            machineId,
            issuedUtc = issuedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            validUntil = validUntil?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            signature = Convert.ToBase64String(signature),
        };

        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.poslic");
        var json = JsonSerializer.Serialize(file);
        File.WriteAllText(path, tamper ? json.Replace("\"validUntil\":\"2027", "\"validUntil\":\"2099", StringComparison.Ordinal) : json);
        return path;
    }
}
