using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;
using Pos.Infrastructure.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>012, H2/H3: solo se acepta una licencia extendida formato 2 firmada y de esta máquina.</summary>
public sealed class EcdsaLicenseVerifierTests : IDisposable
{
    private static readonly DateTime Issued = new(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);

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
    public void LicenciaValida_DevuelveLosModulosFirmados()
    {
        var file = _issuer.Issue(_directory.Root, "maquina-a", Issued, LicensedModule.Inventory, LicensedModule.CashShifts);

        var valid = Assert.IsType<LicenseVerification.Valid>(Verify(file));

        Assert.Equal([LicensedModule.Inventory, LicensedModule.CashShifts], valid.Grant.Modules.Order());
        Assert.Equal(Issued, valid.Grant.IssuedUtc);
    }

    [Fact]
    public void IdentificadorDesconocido_SeIgnora_SinInvalidarLaLicencia()
    {
        var file = _issuer.Issue(_directory.Root, "maquina-a", Issued, [Guid.NewGuid(), ModuleCatalog.IdOf(LicensedModule.Returns)]);

        var valid = Assert.IsType<LicenseVerification.Valid>(Verify(file));

        Assert.Equal([LicensedModule.Returns], valid.Grant.Modules);
    }

    [Fact]
    public void LicenciaDeOtraMaquina_SeRechazaConMotivoClaro()
    {
        var file = _issuer.Issue(_directory.Root, "maquina-b", Issued, LicensedModule.Inventory);

        Assert.Equal(LicenseImportRejection.OtherMachine, Assert.IsType<LicenseVerification.Rejected>(Verify(file)).Reason);
    }

    [Fact]
    public void LicenciaConContenidoAlterado_SeRechaza()
    {
        var file = _issuer.Issue(_directory.Root, "maquina-a", Issued, [ModuleCatalog.IdOf(LicensedModule.Inventory)], tamper: true);

        Assert.Equal(LicenseImportRejection.BadSignature, Assert.IsType<LicenseVerification.Rejected>(Verify(file)).Reason);
    }

    [Fact]
    public void FormatoUno_SeRechazaComoIlegible()
    {
        var file = _issuer.Issue(_directory.Root, "maquina-a", Issued, [ModuleCatalog.IdOf(LicensedModule.Inventory)], format: 1);

        Assert.Equal(LicenseImportRejection.Unreadable, Assert.IsType<LicenseVerification.Rejected>(Verify(file)).Reason);
    }

    [Fact]
    public void ArchivoMayorA16Kb_SeRechaza()
    {
        var file = Path.Combine(_directory.Root, "grande.poslic");
        File.WriteAllText(file, new string(' ', 17 * 1024));

        Assert.Equal(LicenseImportRejection.Unreadable, Assert.IsType<LicenseVerification.Rejected>(Verify(file)).Reason);
    }

    [Fact]
    public void LicenciaFirmadaPorOtraClave_SeRechaza()
    {
        using var other = new TestLicenseIssuer();
        var file = other.Issue(_directory.Root, "maquina-a", Issued, LicensedModule.Inventory);

        Assert.Equal(LicenseImportRejection.BadSignature, Assert.IsType<LicenseVerification.Rejected>(Verify(file)).Reason);
    }
}
