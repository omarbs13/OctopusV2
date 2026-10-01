using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;

namespace Pos.Infrastructure.Licensing;

/// <summary>
/// <c>license.lic</c> (011, research §2): encabezado (marca, versión, huella del ID de máquina), nonce,
/// etiqueta y contenido cifrado con AES-256-GCM. La clave sale de HKDF con el ID de máquina, así que
/// copiarlo a otra máquina o editarlo lo invalida.
/// </summary>
public sealed partial class LicenseFileStore : ILicenseStore
{
    private const int CurrentVersion = 1;
    private const int FingerprintLength = 8;
    private const int NonceLength = 12;
    private const int TagLength = 16;

    private static readonly byte[] Magic = "POSL"u8.ToArray();
    private static readonly byte[] AppSecret = "Pos.License.Secret.v1/7c1f9a52-3e0b-4d6f-8b21-5a94d0e8c3b7"u8.ToArray();
    private static readonly int HeaderLength = Magic.Length + 1 + FingerprintLength;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppPaths _paths;
    private readonly IMachineIdProvider _machine;
    private readonly ILogger<LicenseFileStore> _logger;

    public LicenseFileStore(IAppPaths paths, IMachineIdProvider machine, ILogger<LicenseFileStore> logger)
    {
        _paths = paths;
        _machine = machine;
        _logger = logger;
    }

    public LicenseLoadResult Load()
    {
        var file = _paths.LicenseFile;
        if (!File.Exists(file))
        {
            return new LicenseLoadResult.Missing();
        }

        try
        {
            var bytes = File.ReadAllBytes(file);
            var machineId = _machine.GetMachineId();
            if (bytes.Length < HeaderLength + NonceLength + TagLength || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic)
                || bytes[Magic.Length] != CurrentVersion)
            {
                return Invalid(InvalidLicenseReason.Corrupt);
            }

            if (!bytes.AsSpan(Magic.Length + 1, FingerprintLength).SequenceEqual(Fingerprint(machineId)))
            {
                return Invalid(InvalidLicenseReason.OtherMachine);
            }

            var content = Decrypt(bytes, machineId);
            var data = JsonSerializer.Deserialize<FileContent>(content, JsonOptions);
            if (data is null || !string.Equals(data.MachineId, machineId, StringComparison.Ordinal))
            {
                return Invalid(InvalidLicenseReason.OtherMachine);
            }

            return new LicenseLoadResult.Loaded(data.ToRecord());
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException or IOException
            or UnauthorizedAccessException or ArgumentException)
        {
            LogUnreadable(ex.GetType().Name);
            return Invalid(InvalidLicenseReason.Corrupt);
        }
    }

    public void Save(LicenseRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var machineId = _machine.GetMachineId();
        var plain = JsonSerializer.SerializeToUtf8Bytes(FileContent.From(record with { MachineId = machineId }), JsonOptions);

        var header = new byte[HeaderLength];
        Magic.CopyTo(header, 0);
        header[Magic.Length] = CurrentVersion;
        Fingerprint(machineId).CopyTo(header, Magic.Length + 1);

        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagLength];
        using (var aes = new AesGcm(DeriveKey(machineId), TagLength))
        {
            aes.Encrypt(nonce, plain, cipher, tag, header);
        }

        var output = new byte[HeaderLength + NonceLength + TagLength + cipher.Length];
        header.CopyTo(output, 0);
        nonce.CopyTo(output, HeaderLength);
        tag.CopyTo(output, HeaderLength + NonceLength);
        cipher.CopyTo(output, HeaderLength + NonceLength + TagLength);

        Directory.CreateDirectory(Path.GetDirectoryName(_paths.LicenseFile)!);
        var temp = _paths.LicenseFile + ".tmp";
        File.WriteAllBytes(temp, output);
        File.Move(temp, _paths.LicenseFile, overwrite: true);
    }

    private static byte[] Decrypt(byte[] file, string machineId)
    {
        var header = file.AsSpan(0, HeaderLength);
        var nonce = file.AsSpan(HeaderLength, NonceLength);
        var tag = file.AsSpan(HeaderLength + NonceLength, TagLength);
        var cipher = file.AsSpan(HeaderLength + NonceLength + TagLength);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(DeriveKey(machineId), TagLength);
        aes.Decrypt(nonce, cipher, tag, plain, header);
        return plain;
    }

    private static byte[] DeriveKey(string machineId) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(machineId), 32, AppSecret, "pos-license-v1"u8.ToArray());

    private static byte[] Fingerprint(string machineId) =>
        SHA256.HashData(Encoding.UTF8.GetBytes("fp:" + machineId))[..FingerprintLength];

    private LicenseLoadResult.Invalid Invalid(InvalidLicenseReason reason)
    {
        LogInvalid(reason);
        return new LicenseLoadResult.Invalid(reason);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "El archivo de licencia no es válido: {Reason}")]
    private partial void LogInvalid(InvalidLicenseReason reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo leer el archivo de licencia ({ErrorType})")]
    private partial void LogUnreadable(string errorType);

    private sealed record FileContent(string MachineId, DateTime FirstRunUtc, DateTime LastSeenUtc, GrantContent? Grant)
    {
        public static FileContent From(LicenseRecord record) => new(
            record.MachineId,
            record.FirstRunUtc,
            record.LastSeenUtc,
            record.Grant is null
                ? null
                : new GrantContent(
                    record.Grant.MachineId,
                    record.Grant.IssuedAtUtc,
                    record.Grant.ValidUntil?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    record.Grant.Signature));

        public LicenseRecord ToRecord() => new(
            CurrentVersion,
            MachineId,
            DateTime.SpecifyKind(FirstRunUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(LastSeenUtc, DateTimeKind.Utc),
            Grant is null
                ? null
                : new LicenseGrant(
                    Grant.MachineId,
                    DateTime.SpecifyKind(Grant.IssuedAtUtc, DateTimeKind.Utc),
                    Grant.ValidUntil is null
                        ? null
                        : DateOnly.ParseExact(Grant.ValidUntil, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    Grant.Signature));
    }

    private sealed record GrantContent(string MachineId, DateTime IssuedAtUtc, string? ValidUntil, string Signature);
}
