using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>Escribe un <c>license.lic</c> versión 1 (011) con el mismo contenedor, para probar la migración.</summary>
internal static class LegacyFile
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly byte[] AppSecret = "Pos.License.Secret.v1/7c1f9a52-3e0b-4d6f-8b21-5a94d0e8c3b7"u8.ToArray();

    public static void Write(string path, string machineId, DateTime firstRun, DateTime lastSeen, string? validUntil)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                machineId,
                firstRunUtc = firstRun,
                lastSeenUtc = lastSeen,
                grant = new { machineId, issuedAtUtc = firstRun, validUntil, signature = "firma" },
            },
            JsonOptions);

        var header = new byte[13];
        "POSL"u8.CopyTo(header);
        header[4] = 1;
        SHA256.HashData(Encoding.UTF8.GetBytes("fp:" + machineId))[..8].CopyTo(header, 5);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(machineId), 32, AppSecret, "pos-license-v1"u8.ToArray());
        using (var aes = new AesGcm(key, 16))
        {
            aes.Encrypt(nonce, plain, cipher, tag, header);
        }

        File.WriteAllBytes(path, [.. header, .. nonce, .. tag, .. cipher]);
    }
}
