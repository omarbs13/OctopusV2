using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Pos.Domain.Licensing;
using Pos.Infrastructure.Licensing;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>Emisor de apoyo: firma licencias con un par de claves de prueba (la emisión real está fuera del repositorio).</summary>
internal sealed class TestLicenseIssuer : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

    public void Dispose() => _key.Dispose();

    /// <summary>Emite un archivo formato 2 con los identificadores indicados (conocidos o no).</summary>
    public string Issue(string directory, string machineId, DateTime issuedUtc, IEnumerable<Guid> modules, bool tamper = false, int format = 2)
    {
        var ids = modules.ToList();
        var signature = _key.SignData(LicenseCanonical.Build(format, machineId, issuedUtc, ids), HashAlgorithmName.SHA256);
        var file = new
        {
            format,
            machineId,
            issuedUtc = issuedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            modules = ids.Select(g => g.ToString("D", CultureInfo.InvariantCulture)),
            signature = Convert.ToBase64String(signature),
        };

        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.poslic");
        var json = JsonSerializer.Serialize(file);
        File.WriteAllText(path, tamper ? json.Replace("\"issuedUtc\":\"2026", "\"issuedUtc\":\"2027", StringComparison.Ordinal) : json);
        return path;
    }

    public string Issue(string directory, string machineId, DateTime issuedUtc, params LicensedModule[] modules) =>
        Issue(directory, machineId, issuedUtc, modules.Select(ModuleCatalog.IdOf));
}
