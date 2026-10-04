using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pos.Infrastructure.Licensing;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>
/// contracts/license-format.md §6 (documento v4): la clave compilada es <c>publicKey</c> de
/// <c>contracts/license-public-key.json</c>, el archivo que exporta OctopusAdmin y con el que comprueba
/// que firma con la clave que tienen los POS. Si Admin cambia de clave y se actualiza el archivo, esta
/// prueba falla hasta que se actualice <see cref="EcdsaLicenseVerifier.ProductionPublicKey"/>.
/// </summary>
public sealed class SigningPublicKeyContractTests
{
    private static readonly string ContractPath =
        Path.Combine(AppContext.BaseDirectory, "contracts", "license-public-key.json");

    private static readonly string[] ExpectedFields = ["keyId", "algorithm", "publicKey", "fingerprint"];

    [Fact]
    public void LaClaveCompilada_EsLaDelContrato()
    {
        using var contract = ReadContract();

        Assert.Equal(EcdsaLicenseVerifier.ProductionPublicKey, contract.RootElement.GetProperty("publicKey").GetString());
    }

    [Fact]
    public void ElContrato_EsCoherente()
    {
        var bytes = File.ReadAllBytes(ContractPath);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), "El contrato no debe tener BOM.");
        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);

        using var contract = ReadContract();
        var root = contract.RootElement;
        Assert.Equal(ExpectedFields, root.EnumerateObject().Select(field => field.Name));
        Assert.Equal("ECDSA-P256-SHA256", root.GetProperty("algorithm").GetString());

        var der = Convert.FromBase64String(root.GetProperty("publicKey").GetString()!);
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(der));
        Assert.Equal(fingerprint, root.GetProperty("fingerprint").GetString());
        Assert.Equal(fingerprint[..16], root.GetProperty("keyId").GetString());
    }

    [Fact]
    public void LaClaveCompilada_EsP256()
    {
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(EcdsaLicenseVerifier.ProductionPublicKey), out _);

        Assert.Equal(256, key.KeySize);
        Assert.Equal("1.2.840.10045.3.1.7", key.ExportParameters(false).Curve.Oid.Value);
    }

    private static JsonDocument ReadContract()
    {
        Assert.True(File.Exists(ContractPath), "contracts/license-public-key.json no se copió a la salida de pruebas.");
        return JsonDocument.Parse(File.ReadAllBytes(ContractPath));
    }
}
