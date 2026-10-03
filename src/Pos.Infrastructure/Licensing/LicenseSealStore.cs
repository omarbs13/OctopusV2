using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Licensing;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Licensing;

/// <summary>
/// Copia protegida de las fechas de la prueba (012, research §3; 025, research §4): una sola fila con la
/// carga cifrada con AES-256-GCM (nonce + etiqueta + cifrado). La clave sale de HKDF con un contexto
/// distinto al del archivo. Distingue la fila ausente de la alterada: una alterada vence la prueba (FR-025).
/// </summary>
public sealed class LicenseSealStore : ILicenseSealStore
{
    private const int NonceLength = 12;
    private const int TagLength = 16;

    /// <summary>Contexto fijo de la derivación; no es un secreto, solo detecta ediciones casuales (research §4).</summary>
    private static readonly byte[] KeyContext = "Pos.License.Secret.v1/7c1f9a52-3e0b-4d6f-8b21-5a94d0e8c3b7"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<PosDbContext> _contexts;
    private readonly IMachineIdProvider _machine;

    public LicenseSealStore(IDbContextFactory<PosDbContext> contexts, IMachineIdProvider machine)
    {
        _contexts = contexts;
        _machine = machine;
    }

    public async Task<LicenseSealReadResult> ReadAsync(CancellationToken cancellationToken)
    {
        await using var db = await _contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.LicenseSeals.AsNoTracking().OrderBy(e => e.Id).FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return new LicenseSealReadResult.Missing();
        }

        if (row.Payload.Length <= NonceLength + TagLength)
        {
            return new LicenseSealReadResult.Tampered();
        }

        try
        {
            var payload = row.Payload;
            var cipher = payload.AsSpan(NonceLength + TagLength);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(DeriveKey(), TagLength);
            aes.Decrypt(payload.AsSpan(0, NonceLength), cipher, payload.AsSpan(NonceLength, TagLength), plain);
            var content = JsonSerializer.Deserialize<SealContent>(plain, JsonOptions);
            return content is null
                ? new LicenseSealReadResult.Tampered()
                : new LicenseSealReadResult.Valid(new LicenseSeal(
                    DateTime.SpecifyKind(content.FirstRunUtc, DateTimeKind.Utc),
                    DateTime.SpecifyKind(content.LastSeenUtc, DateTimeKind.Utc),
                    content.LicenseImportedUtc is { } imported ? DateTime.SpecifyKind(imported, DateTimeKind.Utc) : null));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
        {
            return new LicenseSealReadResult.Tampered();
        }
    }

    public async Task WriteAsync(LicenseSeal seal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seal);

        var plain = JsonSerializer.SerializeToUtf8Bytes(new SealContent(seal.FirstRunUtc, seal.LastSeenUtc, seal.LicenseImportedUtc), JsonOptions);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagLength];
        using (var aes = new AesGcm(DeriveKey(), TagLength))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        var payload = new byte[NonceLength + TagLength + cipher.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceLength);
        cipher.CopyTo(payload, NonceLength + TagLength);

        await using var db = await _contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.LicenseSeals.OrderBy(e => e.Id).FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            db.LicenseSeals.Add(new LicenseSealEntity { Id = Guid.CreateVersion7(), Payload = payload });
        }
        else
        {
            row.Payload = payload;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private byte[] DeriveKey() =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(_machine.GetMachineId()), 32, KeyContext, "pos-license-seal-v2"u8.ToArray());

    /// <summary>Un sello de 012 no tiene <c>LicenseImportedUtc</c> y se lee como nulo.</summary>
    private sealed record SealContent(DateTime FirstRunUtc, DateTime LastSeenUtc, DateTime? LicenseImportedUtc = null);
}
