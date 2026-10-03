using System.Text.Json;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;
using Pos.Infrastructure.Licensing;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>
/// 025, H2/H4: el POS verifica exactamente lo que firma OctopusAdmin (contracts/license-format.md §5, pasos 1
/// a 6). Cada paso del contrato es una frontera distinta, por eso cada uno tiene su caso (plan, Complexity Tracking).
/// </summary>
public sealed class EcdsaLicenseVerifierTests : IDisposable
{
    private const string Machine = "3f5a0c1e9b7d2468ace013579bdf2468ace013579bdf2468ace013579bdf2468";

    private static readonly DateTime Issued = new(2026, 10, 3, 15, 4, 5, DateTimeKind.Utc);

    private static readonly string[] LegacyModules = ["7a99f06e-6c58-43fb-bc34-20bec30f8060"];

    private readonly TestLicenseIssuer _issuer = new();

    public void Dispose() => _issuer.Dispose();

    private LicenseVerification Verify(string content, string machineId = Machine) =>
        new EcdsaLicenseVerifier(_issuer.PublicKey).Verify(content, machineId);

    private LicenseImportRejection Rejected(string content, string machineId = Machine) =>
        Assert.IsType<LicenseVerification.Rejected>(Verify(content, machineId)).Reason;

    [Fact]
    public void VectorDelContrato_SeAceptaConLaClaveDePrueba()
    {
        // contracts/license-format.md §7: firmado por OctopusAdmin con una clave solo de prueba.
        const string publicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEqf8wn3coSlDN1BsJiKsesTuks+oZnez7v4g/RC1VNuQIeOq0tZJrIWLSqTqOw4clijOE6LG0hfyUOGidtChwqA==";
        const string payload = "eyJmb3JtYXRWZXJzaW9uIjozLCJsaWNlbnNlSWQiOiIwMTkyZjNhNC01YjZjLTdkOGUtOWYwMS0yMzQ1Njc4OWFiY2QiLCJpc3N1ZWRBdFV0YyI6IjIwMjYtMTAtMDNUMTU6MDQ6MDVaIiwibWFjaGluZUlkIjoiM2Y1YTBjMWU5YjdkMjQ2OGFjZTAxMzU3OWJkZjI0NjhhY2UwMTM1NzliZGYyNDY4YWNlMDEzNTc5YmRmMjQ2OCIsImN1c3RvbWVyTmFtZSI6IkFiYXJyb3RlcyBMYSBFc3BlcmFuemEiLCJtb2R1bGVzIjpbeyJpZCI6IjRjMjUxN2Y1LTA5NmEtNDYwZS04ZWU3LWIwOWU0NjU3Mjk1MiIsImFjdGl2YXRlc09uIjoiMjAyNi0xMC0wMSIsImV4cGlyZXNPbiI6bnVsbH0seyJpZCI6IjdhOTlmMDZlLTZjNTgtNDNmYi1iYzM0LTIwYmVjMzBmODA2MCIsImFjdGl2YXRlc09uIjoiMjAyNi0xMC0wMSIsImV4cGlyZXNPbiI6IjIwMjctMDktMzAifV19";
        const string signature = "NquLEWbwhaKY7PNshkkMqLRCl7euu5aIyYv0FlwzgW/i3NvdHm71uXszHs/LhcT2QzFQTyVsd0+nZTpdOZ+dwA==";
        var verifier = new EcdsaLicenseVerifier(publicKey);

        var license = Assert.IsType<LicenseVerification.Valid>(verifier.Verify(TestLicenseIssuer.Envelope(3, payload, signature), Machine)).License;

        Assert.Equal(new Guid("0192f3a4-5b6c-7d8e-9f01-23456789abcd"), license.LicenseId);
        Assert.Equal(Issued, license.IssuedAtUtc);
        Assert.Equal("Abarrotes La Esperanza", license.CustomerName);
        Assert.Equal(
            [
                new ModuleGrant(LicensedModule.Pos, new DateOnly(2026, 10, 1), null),
                new ModuleGrant(LicensedModule.Inventory, new DateOnly(2026, 10, 1), new DateOnly(2027, 9, 30)),
            ],
            license.Grants);

        // Cambiar un solo byte del contenido invalida la firma.
        var altered = payload[..40] + (payload[40] == 'A' ? 'B' : 'A') + payload[41..];
        Assert.Equal(
            LicenseImportRejection.BadSignature,
            Assert.IsType<LicenseVerification.Rejected>(verifier.Verify(TestLicenseIssuer.Envelope(3, altered, signature), Machine)).Reason);
    }

    [Fact]
    public void LicenciaFirmada_DevuelveSusEntradas_EIgnoraLosModulosDesconocidos()
    {
        var unknown = Guid.CreateVersion7();
        var content = _issuer.Sign(TestLicenseIssuer.Payload(Machine, Issued,
        [
            (ModuleCatalog.IdOf(LicensedModule.Pos), new DateOnly(2026, 10, 1), null),
            (unknown, new DateOnly(2026, 10, 1), null),
        ]));

        var license = Assert.IsType<LicenseVerification.Valid>(Verify(content)).License;

        Assert.Equal(LicensedModule.Pos, Assert.Single(license.Grants).Module);
    }

    [Fact]
    public void VencimientoAusente_EsIndefinido()
    {
        var payload = $$"""{"formatVersion":3,"licenseId":"0192f3a4-5b6c-7d8e-9f01-23456789abcd","issuedAtUtc":"2026-10-03T15:04:05Z","machineId":"{{Machine}}","customerName":"Cliente","modules":[{"id":"4c2517f5-096a-460e-8ee7-b09e46572952","activatesOn":"2026-10-01"}]}""";

        var license = Assert.IsType<LicenseVerification.Valid>(Verify(_issuer.Sign(payload))).License;

        Assert.Null(Assert.Single(license.Grants).ExpiresOn);
    }

    [Fact]
    public void Paso1_ArchivoIlegible()
    {
        Assert.Equal(LicenseImportRejection.Unreadable, Rejected(new string(' ', ILicenseVerifier.MaxBytes + 1)));
        Assert.Equal(LicenseImportRejection.Unreadable, Rejected("no es json"));
        Assert.Equal(LicenseImportRejection.Unreadable, Rejected("""{"payload":"eA==","signature":"eA=="}"""));
    }

    [Fact]
    public void Paso2_LicenciaFormato2Real_NoEsCompatible()
    {
        // Sobre de 012: sin payload; el formato se comprueba antes de exigirlo (contrato §5, paso 2).
        var legacy = JsonSerializer.Serialize(new
        {
            format = 2,
            machineId = Machine,
            issuedUtc = "2026-09-30T15:00:00Z",
            modules = LegacyModules,
            signature = "MEUCIQ==",
        });

        Assert.Equal(LicenseImportRejection.UnsupportedFormat, Rejected(legacy));
    }

    [Fact]
    public void Paso3_FaltaPayloadOFirma_OFirmaNoP1363_EsIlegible()
    {
        var payload = TestLicenseIssuer.Payload(Machine, Issued, []);
        var valid = JsonDocument.Parse(_issuer.Sign(payload)).RootElement;
        var encoded = valid.GetProperty("payload").GetString()!;
        var signature = valid.GetProperty("signature").GetString()!;

        Assert.Equal(LicenseImportRejection.Unreadable, Rejected(JsonSerializer.Serialize(new { format = 3, signature })));
        Assert.Equal(LicenseImportRejection.Unreadable, Rejected(JsonSerializer.Serialize(new { format = 3, payload = encoded })));
        Assert.Equal(LicenseImportRejection.Unreadable, Rejected(TestLicenseIssuer.Envelope(3, "%%%", signature)));
        Assert.Equal(LicenseImportRejection.Unreadable, Rejected(TestLicenseIssuer.Envelope(3, encoded, signature[..^4])));
        Assert.Equal(LicenseImportRejection.Unreadable, Rejected(_issuer.SignDer(payload)));
    }

    [Fact]
    public void Paso4_FirmaDeOtraClave_NoEsAutentica()
    {
        using var other = new TestLicenseIssuer();

        Assert.Equal(LicenseImportRejection.BadSignature, Rejected(other.Issue(Machine, Issued, LicensedModule.Pos)));
    }

    [Theory]
    [InlineData("""{"formatVersion":3,"issuedAtUtc":"2026-10-03T15:04:05Z","machineId":"m","customerName":"C","modules":[]}""")]
    [InlineData("""{"formatVersion":2,"licenseId":"0192f3a4-5b6c-7d8e-9f01-23456789abcd","issuedAtUtc":"2026-10-03T15:04:05Z","machineId":"m","customerName":"C","modules":[]}""")]
    [InlineData("""{"formatVersion":3,"licenseId":"0192f3a4-5b6c-7d8e-9f01-23456789abcd","issuedAtUtc":"2026-10-03 15:04:05","machineId":"m","customerName":"C","modules":[]}""")]
    [InlineData("""{"formatVersion":3,"licenseId":"0192f3a4-5b6c-7d8e-9f01-23456789abcd","issuedAtUtc":"2026-10-03T15:04:05Z","machineId":"m","customerName":"","modules":[]}""")]
    [InlineData("""{"formatVersion":3,"licenseId":"0192f3a4-5b6c-7d8e-9f01-23456789abcd","issuedAtUtc":"2026-10-03T15:04:05Z","machineId":"m","customerName":"C","modules":[{"id":"4c2517f5-096a-460e-8ee7-b09e46572952","activatesOn":"01/10/2026"}]}""")]
    public void Paso5_ContenidoFirmadoIncompletoOMalFormado_EsIlegible(string payload) =>
        Assert.Equal(LicenseImportRejection.Unreadable, Rejected(_issuer.Sign(payload), "m"));

    [Fact]
    public void Paso6_LicenciaDeOtraMaquina()
    {
        Assert.Equal(LicenseImportRejection.OtherMachine, Rejected(_issuer.Issue("otra", Issued, LicensedModule.Pos)));
    }
}
