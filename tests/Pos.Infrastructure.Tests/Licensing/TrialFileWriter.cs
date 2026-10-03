using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>
/// Escribe un archivo de prueba de versiones anteriores (v1 de 011, v2 de 012) y cargas del sello con el
/// mismo cifrado que la aplicación, para probar la migración (025, research §4).
/// </summary>
internal static class TrialFileWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly byte[] KeyContext = "Pos.License.Secret.v1/7c1f9a52-3e0b-4d6f-8b21-5a94d0e8c3b7"u8.ToArray();

    /// <summary>Archivo de prueba v2 (012): fechas y los módulos de las licencias que se habían importado.</summary>
    public static void WriteV2(string path, string machineId, DateTime firstRun, DateTime lastSeen, params Guid[] modules) =>
        Write(path, machineId, 2, new { machineId, firstRunUtc = firstRun, lastSeenUtc = lastSeen, trialDays = 30, modules });

    /// <summary>Archivo de prueba v1 (011), que ya no se lee.</summary>
    public static void WriteV1(string path, string machineId, DateTime firstRun) =>
        Write(path, machineId, 1, new { machineId, firstRunUtc = firstRun, lastSeenUtc = firstRun });

    /// <summary>Carga cifrada de un sello de 012, sin <c>licenseImportedUtc</c>.</summary>
    public static byte[] SealPayloadV012(string machineId, DateTime firstRun, DateTime lastSeen)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(new { firstRunUtc = firstRun, lastSeenUtc = lastSeen }, JsonOptions);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(machineId), 32, KeyContext, "pos-license-seal-v2"u8.ToArray());
        using (var aes = new AesGcm(key, 16))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        return [.. nonce, .. tag, .. cipher];
    }

    private static void Write(string path, string machineId, byte version, object content)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(content, JsonOptions);
        var header = new byte[13];
        "POSL"u8.CopyTo(header);
        header[4] = version;
        SHA256.HashData(Encoding.UTF8.GetBytes("fp:" + machineId))[..8].CopyTo(header, 5);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(machineId), 32, KeyContext, "pos-license-v1"u8.ToArray());
        using (var aes = new AesGcm(key, 16))
        {
            aes.Encrypt(nonce, plain, cipher, tag, header);
        }

        File.WriteAllBytes(path, [.. header, .. nonce, .. tag, .. cipher]);
    }
}
