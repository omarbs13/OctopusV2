using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;
using Pos.Infrastructure.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>012, H2; 025, research §4: el archivo de prueba se liga a la máquina y no se puede editar ni copiar.</summary>
public sealed class LicenseFileStoreTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDataDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private LicenseFileStore StoreFor(string machineId) =>
        new(_directory.Paths, new FixedMachine(machineId), NullLogger<LicenseFileStore>.Instance);

    private static TrialRecord Record(string machineId) => new(machineId, Start, Start.AddDays(3), 30, Start.AddDays(2));

    [Fact]
    public void Guardar_YLeer_ConservaLosDatos()
    {
        var store = StoreFor("maquina-a");
        store.Save(Record("maquina-a"));

        var result = Assert.IsType<LicenseLoadResult.Loaded>(store.Load());

        Assert.Equal(Record("maquina-a"), result.Record);
        Assert.False(result.HadLegacyModules);
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
    public void ArchivoDePruebaV2ConModulos_ConservaLasFechasYAvisaQueHabiaLicencia()
    {
        TrialFileWriter.WriteV2(_directory.Paths.LicenseFile, "maquina-a", Start, Start.AddDays(3), ModuleCatalog.IdOf(LicensedModule.Inventory));

        var result = Assert.IsType<LicenseLoadResult.Loaded>(StoreFor("maquina-a").Load());

        Assert.Equal(Start, result.Record.FirstRunUtc);
        Assert.Equal(Start.AddDays(3), result.Record.LastSeenUtc);
        Assert.Null(result.Record.LicenseImportedUtc);
        Assert.True(result.HadLegacyModules);
    }

    [Fact]
    public void ArchivoDePruebaV2SinModulos_NoAvisa()
    {
        TrialFileWriter.WriteV2(_directory.Paths.LicenseFile, "maquina-a", Start, Start.AddDays(3));

        Assert.False(Assert.IsType<LicenseLoadResult.Loaded>(StoreFor("maquina-a").Load()).HadLegacyModules);
    }

    [Fact]
    public void ArchivoDePruebaV1_YaNoSeLee()
    {
        TrialFileWriter.WriteV1(_directory.Paths.LicenseFile, "maquina-a", Start);

        Assert.IsType<LicenseLoadResult.Unusable>(StoreFor("maquina-a").Load());
    }

    private sealed class FixedMachine(string id) : Pos.Application.Licensing.IMachineIdProvider
    {
        public string GetMachineId() => id;
    }
}
