using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;

namespace Pos.Infrastructure.Licensing;

/// <summary>
/// Verifica sin conexión la firma ECDSA P-256 del proveedor y que la licencia sea de esta máquina
/// (011, research §3). La app solo conoce la clave pública.
/// </summary>
public sealed class EcdsaLicenseVerifier : ILicenseVerifier
{
    /// <summary>Pendiente: clave pública de producción del proveedor (SubjectPublicKeyInfo en Base64).</summary>
    public const string ProductionPublicKey =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEPJtCVVYVLeYa60jlbbp8MasuCYsZT1FPqR8vfS2UHzE/v2fgA7K7Miy0/6v8t7an5CE9XiINzGuWuWMZoK2TwQ==";

    public const string DevelopmentKeyVariable = "POS_LICENSE_DEV_PUBLIC_KEY";

    private const int SupportedFormat = 1;
    private const long MaxFileBytes = 16 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _publicKey;

    public EcdsaLicenseVerifier()
        : this(ResolvePublicKey())
    {
    }

    public EcdsaLicenseVerifier(string publicKeyBase64) => _publicKey = publicKeyBase64;

    public LicenseVerification Verify(string filePath, string machineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!TryRead(filePath, out var file) || file is null)
        {
            return Rejected(LicenseImportRejection.Unreadable);
        }

        if (file.Format != SupportedFormat || string.IsNullOrWhiteSpace(file.MachineId) || file.IssuedUtc is null
            || string.IsNullOrWhiteSpace(file.Signature) || !TryParseUntil(file.ValidUntil, out var until))
        {
            return Rejected(LicenseImportRejection.Unreadable);
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(file.Signature);
        }
        catch (FormatException)
        {
            return Rejected(LicenseImportRejection.Unreadable);
        }

        var issued = file.IssuedUtc.Value.ToUniversalTime();
        if (!IsSigned(LicenseCanonical.Build(file.Format, file.MachineId, issued, until), signature))
        {
            return Rejected(LicenseImportRejection.BadSignature);
        }

        return string.Equals(file.MachineId, machineId, StringComparison.Ordinal)
            ? new LicenseVerification.Valid(new LicenseGrant(file.MachineId, issued, until, file.Signature))
            : Rejected(LicenseImportRejection.OtherMachine);
    }

    private static LicenseVerification.Rejected Rejected(LicenseImportRejection reason) => new LicenseVerification.Rejected(reason);

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

    private static bool TryRead(string path, out LicenseFileDto? file)
    {
        file = null;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return false;
            }

            file = JsonSerializer.Deserialize<LicenseFileDto>(File.ReadAllText(path), JsonOptions);
            return file is not null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryParseUntil(string? text, out DateOnly? until)
    {
        until = null;
        if (text is null)
        {
            return true;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            until = date;
            return true;
        }

        return false;
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

    private sealed record LicenseFileDto(int Format, string? MachineId, DateTime? IssuedUtc, string? ValidUntil, string? Signature);
}
