using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;

namespace Pos.Infrastructure.Licensing;

/// <summary>
/// Verifica sin conexión una licencia formato 3 (025, contracts/license-format.md §2 a §5, pasos 1 a 6).
/// La firma ECDSA P-256/SHA-256 (IEEE P1363) se comprueba sobre los bytes decodificados de <c>payload</c>:
/// el contenido nunca se reconstruye. La app solo conoce la clave pública (FR-041).
/// </summary>
public sealed class EcdsaLicenseVerifier : ILicenseVerifier
{
    /// <summary>
    /// TODO(025, research §15): reemplazar con la clave pública de producción de OctopusAdmin
    /// (SubjectPublicKeyInfo DER en Base64) cuando se entregue. Bloquea solo la liberación a producción.
    /// </summary>
    public const string ProductionPublicKey =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEPJtCVVYVLeYa60jlbbp8MasuCYsZT1FPqR8vfS2UHzE/v2fgA7K7Miy0/6v8t7an5CE9XiINzGuWuWMZoK2TwQ==";

    public const string DevelopmentKeyVariable = "POS_LICENSE_DEV_PUBLIC_KEY";

    private const int SupportedFormat = 3;
    private const int SignatureLength = 64;
    private const int CustomerNameMaxLength = 200;

    private readonly string _publicKey;

    public EcdsaLicenseVerifier()
        : this(ResolvePublicKey())
    {
    }

    public EcdsaLicenseVerifier(string publicKeyBase64) => _publicKey = publicKeyBase64;

    public LicenseVerification Verify(string content, string machineId)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Paso 1: tamaño y sobre JSON con "format" numérico.
        if (Encoding.UTF8.GetByteCount(content) > ILicenseVerifier.MaxBytes)
        {
            return Rejected(LicenseImportRejection.Unreadable);
        }

        JsonDocument envelope;
        try
        {
            envelope = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            return Rejected(LicenseImportRejection.Unreadable);
        }

        using (envelope)
        {
            var root = envelope.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("format", out var format)
                || format.ValueKind != JsonValueKind.Number
                || !format.TryGetInt32(out var formatNumber))
            {
                return Rejected(LicenseImportRejection.Unreadable);
            }

            // Paso 2: antes de exigir payload y firma, porque el formato 2 no tiene payload.
            if (formatNumber != SupportedFormat)
            {
                return Rejected(LicenseImportRejection.UnsupportedFormat);
            }

            // Paso 3: Base64 estándar sin espacios ni saltos de línea; firma P1363 de 64 bytes.
            if (!TryDecode(root, "payload", out var payload) || !TryDecode(root, "signature", out var signature)
                || signature.Length != SignatureLength)
            {
                return Rejected(LicenseImportRejection.Unreadable);
            }

            // Paso 4: firma sobre los bytes decodificados.
            if (!IsSigned(payload, signature))
            {
                return Rejected(LicenseImportRejection.BadSignature);
            }

            // Paso 5: contenido completo y bien formado.
            var license = ReadContent(payload);
            if (license is null)
            {
                return Rejected(LicenseImportRejection.Unreadable);
            }

            // Paso 6: la licencia es de esta máquina.
            return string.Equals(license.MachineId, machineId, StringComparison.Ordinal)
                ? new LicenseVerification.Valid(license)
                : Rejected(LicenseImportRejection.OtherMachine);
        }
    }

    private static LicenseVerification.Rejected Rejected(LicenseImportRejection reason) => new(reason);

    private static string ResolvePublicKey()
    {
#if DEBUG
        var overridden = Environment.GetEnvironmentVariable(DevelopmentKeyVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }
#endif
        return ProductionPublicKey;
    }

    private static bool TryDecode(JsonElement root, string name, out byte[] bytes)
    {
        bytes = [];
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = value.GetString()!;
        if (text.Length == 0 || text.Any(char.IsWhiteSpace))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private bool IsSigned(byte[] content, byte[] signature)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(_publicKey), out _);
            return key.VerifyData(content, signature, HashAlgorithmName.SHA256);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }
    }

    /// <summary>Contenido del contrato §3; nulo si falta un campo obligatorio o tiene formato inválido.</summary>
    private static SignedLicense? ReadContent(byte[] payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryInt(root, "formatVersion", out var version) || version != SupportedFormat
                || !TryString(root, "licenseId", out var licenseText)
                || !Guid.TryParseExact(licenseText, "D", out var licenseId)
                || !TryString(root, "issuedAtUtc", out var issuedText)
                || !DateTime.TryParseExact(
                    issuedText,
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var issued)
                || !TryString(root, "machineId", out var machineId) || machineId.Length == 0
                || !TryString(root, "customerName", out var customer)
                || customer.Length is 0 or > CustomerNameMaxLength
                || !root.TryGetProperty("modules", out var modules) || modules.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var grants = new List<ModuleGrant>();
            foreach (var entry in modules.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || !TryString(entry, "id", out var idText) || !Guid.TryParse(idText, out var id)
                    || !TryDate(entry, "activatesOn", out var activates)
                    || !TryOptionalDate(entry, "expiresOn", out var expires))
                {
                    return null;
                }

                // Los identificadores desconocidos se ignoran (FR-022).
                if (ModuleCatalog.TryGetModule(id, out var module))
                {
                    grants.Add(new ModuleGrant(module, activates, expires));
                }
            }

            return new SignedLicense(licenseId, DateTime.SpecifyKind(issued, DateTimeKind.Utc), machineId, customer, grants);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static bool TryString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()!;
        return true;
    }

    private static bool TryDate(JsonElement element, string name, out DateOnly value)
    {
        value = default;
        return TryString(element, name, out var text)
            && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    /// <summary><c>null</c> o ausente = indefinido.</summary>
    private static bool TryOptionalDate(JsonElement element, string name, out DateOnly? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (!TryDate(element, name, out var date))
        {
            return false;
        }

        value = date;
        return true;
    }
}
