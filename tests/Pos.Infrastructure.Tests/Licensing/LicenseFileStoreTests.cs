using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;
using Pos.Infrastructure.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>012, H2: el archivo de licencia se liga a la máquina y no se puede editar ni copiar.</summary>
public sealed class LicenseFileStoreTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDataDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private LicenseFileStore StoreFor(string machineId) =>
        new(_directory.Paths, new FixedMachine(machineId), NullLogger<LicenseFileStore>.Instance);

    private static LicenseRecord Record(string machineId) =>
        new(2, machineId, Start, Start.AddDays(3), 30, new HashSet<LicensedModule> { LicensedModule.Inventory, LicensedModule.Returns });

    [Fact]
    public void Guardar_YLeer_ConservaLosDatos()
    {
        var store = StoreFor("maquina-a");
        store.Save(Record("maquina-a"));

        var loaded = Assert.IsType<LicenseLoadResult.Loaded>(store.Load()).Record;

        Assert.Equal(Start, loaded.FirstRunUtc);
        Assert.Equal(Start.AddDays(3), loaded.LastSeenUtc);
        Assert.Equal(30, loaded.TrialDays);
        Assert.Equal(Record("maquina-a").Modules.Order(), loaded.Modules.Order());
    }

    [Fact]
    public void ArchivoCopiadoAOtraMaquina_EsInutilizable()
    {
        StoreFor("maquina-a").Save(Record("maquina-a"));

        Assert.IsType<LicenseLoadResult.Unusable>(StoreFor("maquina-b").Load());
    }

    [Fact]
    public void ArchivoAlterado_EsInutilizable()
    {
        var store = StoreFor("maquina-a");
        store.Save(Record("maquina-a"));
        var bytes = File.ReadAllBytes(_directory.Paths.LicenseFile);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(_directory.Paths.LicenseFile, bytes);

        Assert.IsType<LicenseLoadResult.Unusable>(store.Load());
    }

    [Fact]
    public void ArchivoTruncado_EsInutilizable()
    {
        var store = StoreFor("maquina-a");
        store.Save(Record("maquina-a"));
        File.WriteAllBytes(_directory.Paths.LicenseFile, File.ReadAllBytes(_directory.Paths.LicenseFile)[..20]);

        Assert.IsType<LicenseLoadResult.Unusable>(store.Load());
    }

    [Fact]
    public void ArchivoInexistente_SeIndicaComoFaltante() =>
        Assert.IsType<LicenseLoadResult.Missing>(StoreFor("maquina-a").Load());

    [Fact]
    public void ArchivoDeLaVersion011_SeLeeSoloParaMigrar()
    {
        LegacyFile.Write(_directory.Paths.LicenseFile, "maquina-a", Start, Start.AddDays(3), validUntil: "2027-01-01");

        var legacy = Assert.IsType<LicenseLoadResult.LegacyV1>(StoreFor("maquina-a").Load()).License;

        Assert.Equal(Start, legacy.FirstRunUtc);
        Assert.True(legacy.HasGrant);
        Assert.Equal(new DateOnly(2027, 1, 1), legacy.ValidUntil);
    }

    private sealed class FixedMachine(string id) : Pos.Application.Licensing.IMachineIdProvider
    {
        public string GetMachineId() => id;
    }
}
