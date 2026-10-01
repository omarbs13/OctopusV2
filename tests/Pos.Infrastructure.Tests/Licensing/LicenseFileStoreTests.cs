using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;
using Pos.Infrastructure.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>011, H1: el archivo de licencia se liga a la máquina y no se puede editar ni copiar.</summary>
public sealed class LicenseFileStoreTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDataDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private LicenseFileStore StoreFor(string machineId) =>
        new(_directory.Paths, new FixedMachine(machineId), NullLogger<LicenseFileStore>.Instance);

    private static LicenseRecord Record(string machineId) => new(1, machineId, Start, Start, null);

    [Fact]
    public void Guardar_YLeer_ConservaLosDatos()
    {
        var store = StoreFor("maquina-a");
        store.Save(Record("maquina-a"));

        var loaded = Assert.IsType<LicenseLoadResult.Loaded>(store.Load());

        Assert.Equal(Record("maquina-a"), loaded.Record);
    }

    [Fact]
    public void ArchivoCopiadoAOtraMaquina_SeRechaza()
    {
        StoreFor("maquina-a").Save(Record("maquina-a"));

        var result = StoreFor("maquina-b").Load();

        Assert.Equal(InvalidLicenseReason.OtherMachine, Assert.IsType<LicenseLoadResult.Invalid>(result).Reason);
    }

    [Fact]
    public void ArchivoAlterado_SeRechaza()
    {
        var store = StoreFor("maquina-a");
        store.Save(Record("maquina-a"));
        var bytes = File.ReadAllBytes(_directory.Paths.LicenseFile);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(_directory.Paths.LicenseFile, bytes);

        var result = store.Load();

        Assert.Equal(InvalidLicenseReason.Corrupt, Assert.IsType<LicenseLoadResult.Invalid>(result).Reason);
    }

    [Fact]
    public void ArchivoInexistente_SeIndicaComoFaltante()
    {
        Assert.IsType<LicenseLoadResult.Missing>(StoreFor("maquina-a").Load());
    }

    [Fact]
    public async Task ArchivoBorrado_SeRegeneraConElMismoIdYElInicioDelPrimerUsuario()
    {
        var store = StoreFor("maquina-a");
        var clock = new TestClock { UtcNow = new DateTime(2026, 9, 29, 15, 0, 0, DateTimeKind.Utc) };
        var state = new LicenseState(clock);
        var firstUser = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc);
        var bootstrapper = new LicenseBootstrapper(store, state, new FixedMachine("maquina-a"), new FixedAge(firstUser), clock, NullLogger<LicenseBootstrapper>.Instance);

        await bootstrapper.RunAsync(Ct);

        var created = Assert.IsType<LicenseLoadResult.Loaded>(store.Load()).Record;
        Assert.Equal("maquina-a", created.MachineId);
        Assert.Equal(firstUser, created.FirstRunUtc);

        File.Delete(_directory.Paths.LicenseFile);
        await bootstrapper.RunAsync(Ct);

        var regenerated = Assert.IsType<LicenseLoadResult.Loaded>(store.Load()).Record;
        Assert.Equal("maquina-a", regenerated.MachineId);
        Assert.Equal(firstUser, regenerated.FirstRunUtc);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FixedMachine(string id) : IMachineIdProvider
    {
        public string GetMachineId() => id;
    }

    private sealed class FixedAge(DateTime? firstUser) : IInstallationAgeReader
    {
        public Task<DateTime?> GetFirstUserCreatedUtcAsync(CancellationToken cancellationToken) => Task.FromResult(firstUser);
    }
}
