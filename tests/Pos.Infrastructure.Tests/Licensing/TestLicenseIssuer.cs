using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pos.Domain.Licensing;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>
/// Emisor de apoyo: firma licencias formato 3 (025, contracts/license-format.md) con un par de claves en memoria.
/// La emisión real vive en OctopusAdmin, fuera del repositorio; aquí nunca se guarda una clave privada.
/// </summary>
internal sealed class TestLicenseIssuer : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

    public void Dispose() => _key.Dispose();

    /// <summary>Texto de un <c>.lic</c> formato 3 con las entradas indicadas.</summary>
    public string Issue(string machineId, DateTime issuedAtUtc, IEnumerable<ModuleGrant> grants, Guid? licenseId = null) =>
        Sign(Payload(machineId, issuedAtUtc, grants.Select(g => (ModuleCatalog.IdOf(g.Module), g.ActivatesOn, g.ExpiresOn)), licenseId));

    /// <summary>Licencia con módulos indefinidos activos desde 2026-01-01.</summary>
    public string Issue(string machineId, DateTime issuedAtUtc, params LicensedModule[] modules) =>
        Issue(machineId, issuedAtUtc, modules.Select(m => new ModuleGrant(m, new DateOnly(2026, 1, 1), null)));

    /// <summary>Contenido JSON (antes de codificarlo) con identificadores arbitrarios, conocidos o no.</summary>
    public static string Payload(
        string machineId,
        DateTime issuedAtUtc,
        IEnumerable<(Guid Id, DateOnly ActivatesOn, DateOnly? ExpiresOn)> modules,
        Guid? licenseId = null,
        string customerName = "Abarrotes La Esperanza") =>
        JsonSerializer.Serialize(new
        {
            formatVersion = 3,
            licenseId = (licenseId ?? Guid.CreateVersion7()).ToString("D", CultureInfo.InvariantCulture),
            issuedAtUtc = issuedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            machineId,
            customerName,
            modules = modules.Select(m => new
            {
                id = m.Id.ToString("D", CultureInfo.InvariantCulture),
                activatesOn = m.ActivatesOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                expiresOn = m.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            }),
        });

    /// <summary>Sobre formato 3 con el contenido firmado (P1363) tal como lo produce OctopusAdmin.</summary>
    public string Sign(string payload, int format = 3)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Envelope(format, Convert.ToBase64String(bytes), Convert.ToBase64String(_key.SignData(bytes, HashAlgorithmName.SHA256)));
    }

    /// <summary>Firma en DER, que el contrato no acepta.</summary>
    public string SignDer(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var der = _key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        return Envelope(3, Convert.ToBase64String(bytes), Convert.ToBase64String(der));
    }

    public static string Envelope(int format, string payload, string signature) =>
        JsonSerializer.Serialize(new { format, payload, signature });
}
