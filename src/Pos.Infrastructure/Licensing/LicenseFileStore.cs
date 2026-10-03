using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;

namespace Pos.Infrastructure.Licensing;

/// <summary>
/// Archivo de prueba <c>license.lic</c> (025, research §4; no es una licencia del proveedor aunque comparta la
/// extensión): encabezado (marca, versión, huella del ID de máquina), nonce, etiqueta y contenido cifrado con
/// AES-256-GCM. Se escribe el archivo de prueba v3; el v2 de 012 se lee y se migra; el v1 de 011 ya no se lee.
/// </summary>
public sealed partial class LicenseFileStore : ILicenseStore
{
    private const int CurrentVersion = 3;
    private const int PreviousVersion = 2;
    private const int FingerprintLength = 8;
    private const int NonceLength = 12;
    private const int TagLength = 16;

    private static readonly byte[] Magic = "POSL"u8.ToArray();

    /// <summary>
    /// Contexto fijo de la derivación de la clave. No es un secreto (está en el código): solo detecta ediciones
    /// casuales del archivo; la licencia comercial depende únicamente de la firma del proveedor (research §4).
    /// </summary>
    private static readonly byte[] KeyContext = "Pos.License.Secret.v1/7c1f9a52-3e0b-4d6f-8b21-5a94d0e8c3b7"u8.ToArray();
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
                || bytes[Magic.Length] is not (CurrentVersion or PreviousVersion))
            {
                return Unusable("formato");
            }

            if (!bytes.AsSpan(Magic.Length + 1, FingerprintLength).SequenceEqual(Fingerprint(machineId)))
            {
                return Unusable("otra máquina");
            }

            var content = Decrypt(bytes, machineId);
            var data = JsonSerializer.Deserialize<FileContent>(content, JsonOptions);
            if (data is null || !string.Equals(data.MachineId, machineId, StringComparison.Ordinal))
            {
                return Unusable("contenido");
            }

            // El archivo de prueba v2 traía los módulos de una licencia de 011/012: se descartan (FR-021).
            var hadModules = bytes[Magic.Length] == PreviousVersion && data.Modules is { Count: > 0 };
            return new LicenseLoadResult.Loaded(data.ToRecord(), hadModules);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException or IOException
            or UnauthorizedAccessException or ArgumentException)
        {
            return Unusable(ex.GetType().Name);
        }
    }

    public void Save(TrialRecord record)
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
        HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(machineId), 32, KeyContext, "pos-license-v1"u8.ToArray());

    private static byte[] Fingerprint(string machineId) =>
        SHA256.HashData(Encoding.UTF8.GetBytes("fp:" + machineId))[..FingerprintLength];

    private LicenseLoadResult.Unusable Unusable(string reason)
    {
        LogUnusable(reason);
        return new LicenseLoadResult.Unusable();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "El archivo de prueba es inutilizable: {Reason}")]
    private partial void LogUnusable(string reason);

    /// <summary>Contenido de los archivos de prueba v2 y v3; <c>Modules</c> solo existe en el v2.</summary>
    private sealed record FileContent(
        string MachineId,
        DateTime FirstRunUtc,
        DateTime LastSeenUtc,
        int TrialDays,
        DateTime? LicenseImportedUtc = null,
        List<Guid>? Modules = null)
    {
        public static FileContent From(TrialRecord record) => new(
            record.MachineId,
            record.FirstRunUtc,
            record.LastSeenUtc,
            record.TrialDays,
            record.LicenseImportedUtc);

        public TrialRecord ToRecord() => new(
            MachineId,
            DateTime.SpecifyKind(FirstRunUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(LastSeenUtc, DateTimeKind.Utc),
            TrialDays,
            LicenseImportedUtc is { } imported ? DateTime.SpecifyKind(imported, DateTimeKind.Utc) : null);
    }
}
