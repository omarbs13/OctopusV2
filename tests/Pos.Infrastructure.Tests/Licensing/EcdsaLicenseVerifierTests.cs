using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Infrastructure.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>011, H4: solo se acepta una licencia firmada por el proveedor y de esta máquina.</summary>
public sealed class EcdsaLicenseVerifierTests : IDisposable
{
    private static readonly DateTime Issued = new(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Until = new(2027, 9, 30);

    private readonly TempDataDirectory _directory = new();
    private readonly TestLicenseIssuer _issuer = new();

    public void Dispose()
    {
        _issuer.Dispose();
        _directory.Dispose();
    }

    private LicenseVerification Verify(string file, string machineId = "maquina-a") =>
        new EcdsaLicenseVerifier(_issuer.PublicKey).Verify(file, machineId);

    [Fact]
    public void LicenciaValida_SeAcepta()
    {
        var file = _issuer.Issue(_directory.Root, "maquina-a", Issued, Until);

        var valid = Assert.IsType<LicenseVerification.Valid>(Verify(file));

        Assert.Equal(Until, valid.Grant.ValidUntil);
        Assert.Equal(Issued, valid.Grant.IssuedAtUtc);
    }

    [Fact]
    public void LicenciaSinVencimiento_SeAcepta()
    {
        var file = _issuer.Issue(_directory.Root, "maquina-a", Issued, null);

        Assert.Null(Assert.IsType<LicenseVerification.Valid>(Verify(file)).Grant.ValidUntil);
    }

    [Fact]
    public void LicenciaDeOtraMaquina_SeRechazaConMotivoClaro()
    {
        var file = _issuer.Issue(_directory.Root, "maquina-b", Issued, Until);

        Assert.Equal(LicenseImportRejection.OtherMachine, Assert.IsType<LicenseVerification.Rejected>(Verify(file)).Reason);
    }

    [Fact]
    public void LicenciaConFirmaAlterada_SeRechaza()
    {
        var file = _issuer.Issue(_directory.Root, "maquina-a", Issued, Until, tamper: true);

        Assert.Equal(LicenseImportRejection.BadSignature, Assert.IsType<LicenseVerification.Rejected>(Verify(file)).Reason);
    }

    [Fact]
    public void ArchivoIlegible_SeRechaza()
    {
        var file = Path.Combine(_directory.Root, "basura.poslic");
        File.WriteAllText(file, "esto no es una licencia");

        Assert.Equal(LicenseImportRejection.Unreadable, Assert.IsType<LicenseVerification.Rejected>(Verify(file)).Reason);
    }

    [Fact]
    public void LicenciaFirmadaPorOtraClave_SeRechaza()
    {
        using var other = new TestLicenseIssuer();
        var file = other.Issue(_directory.Root, "maquina-a", Issued, Until);

        Assert.Equal(LicenseImportRejection.BadSignature, Assert.IsType<LicenseVerification.Rejected>(Verify(file)).Reason);
    }
}
