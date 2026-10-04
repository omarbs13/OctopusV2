using System.Security.Cryptography;
using Pos.Infrastructure.Licensing;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>
/// 025, contracts/license-format.md §6: la clave compilada es la de <c>contracts/octopus-admin-public-key.txt</c>,
/// el mismo archivo con el que OctopusAdmin comprueba que firma con la clave que tienen los POS.
/// </summary>
public sealed class SigningPublicKeyContractTests
{
    private static readonly string ContractPath =
        Path.Combine(AppContext.BaseDirectory, "contracts", "octopus-admin-public-key.txt");

    [Fact]
    public void LaClaveCompilada_EsLaDelContrato()
    {
        Assert.True(File.Exists(ContractPath), "contracts/octopus-admin-public-key.txt no se copió a la salida de pruebas.");

        Assert.Equal(EcdsaLicenseVerifier.ProductionPublicKey, File.ReadAllText(ContractPath).Trim());
    }

    [Fact]
    public void LaClaveCompilada_EsP256()
    {
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(EcdsaLicenseVerifier.ProductionPublicKey), out _);

        Assert.Equal(256, key.KeySize);
        Assert.Equal("1.2.840.10045.3.1.7", key.ExportParameters(false).Curve.Oid.Value);
    }
}
