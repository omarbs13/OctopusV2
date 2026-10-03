using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Licensing;

namespace Pos.Application.Tests.Licensing;

internal sealed class FakeLicenseStore : ILicenseStore
{
    public LicenseLoadResult Next { get; set; } = new LicenseLoadResult.Missing();

    public TrialRecord? Saved { get; private set; }

    public LicenseLoadResult Load() => Next;

    public void Save(TrialRecord record) => Saved = record;
}

internal sealed class FakeSealStore : ILicenseSealStore
{
    public LicenseSealReadResult Result { get; set; } = new LicenseSealReadResult.Missing();

    public LicenseSeal? Seal => (Result as LicenseSealReadResult.Valid)?.Seal;

    public Task<LicenseSealReadResult> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Result);

    public Task WriteAsync(LicenseSeal seal, CancellationToken cancellationToken)
    {
        Result = new LicenseSealReadResult.Valid(seal);
        return Task.CompletedTask;
    }
}

internal sealed class FakeInstalledLicenseStore : IInstalledLicenseStore
{
    public string? Content { get; set; }

    public int Replacements { get; private set; }

    public Task<string?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Content);

    public Task ReplaceAsync(string content, DateTime importedAtUtc, CancellationToken cancellationToken)
    {
        Content = content;
        Replacements++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Verificador de pruebas: el contenido es la clave de una licencia registrada con <see cref="Register"/>; si
/// no está registrada se rechaza con <see cref="Rejection"/>. El paso 6 (máquina) se aplica de verdad.
/// </summary>
internal sealed class FakeVerifier : ILicenseVerifier
{
    private readonly Dictionary<string, SignedLicense> _licenses = new(StringComparer.Ordinal);

    public LicenseImportRejection Rejection { get; set; } = LicenseImportRejection.BadSignature;

    public List<string> Verified { get; } = [];

    public string Register(SignedLicense license)
    {
        var content = $"lic:{license.LicenseId:N}:{license.IssuedAtUtc.Ticks}";
        _licenses[content] = license;
        return content;
    }

    public LicenseVerification Verify(string content, string machineId)
    {
        Verified.Add(content);
        if (!_licenses.TryGetValue(content, out var license))
        {
            return new LicenseVerification.Rejected(Rejection);
        }

        return license.MachineId == machineId
            ? new LicenseVerification.Valid(license)
            : new LicenseVerification.Rejected(LicenseImportRejection.OtherMachine);
    }
}

internal sealed class FakeCatalogInfo : IModuleCatalogInfo
{
    public int CatalogVersion => 1;

    public string NameOf(LicensedModule module) => module.ToString();

    public string DescriptionOf(LicensedModule module) => module.ToString();
}

internal sealed class FakeMachine : IMachineIdProvider
{
    public const string Id = "m";

    public string GetMachineId() => Id;
}

internal sealed class FakeAge(DateTime? firstUser = null) : IInstallationAgeReader
{
    public Task<DateTime?> GetFirstUserCreatedUtcAsync(CancellationToken cancellationToken) => Task.FromResult(firstUser);
}

/// <summary>Ámbito de pruebas que entrega siempre la misma bitácora.</summary>
internal sealed class FakeScopeFactory(RecordingAuditLog audit) : IServiceScopeFactory, IServiceScope, IServiceProvider
{
    public IServiceProvider ServiceProvider => this;

    public IServiceScope CreateScope() => this;

    public object? GetService(Type serviceType) => serviceType == typeof(IAuditLog) ? audit : null;

    public void Dispose()
    {
    }
}

/// <summary>Licencias y registros de prueba.</summary>
internal static class Licenses
{
    public static readonly DateOnly Today = new(2026, 10, 10);

    public static TrialRecord Trial(DateTime firstRunUtc, DateTime? lastSeenUtc = null, DateTime? licenseImportedUtc = null) =>
        new(FakeMachine.Id, firstRunUtc, lastSeenUtc ?? firstRunUtc, TrialRecord.DefaultTrialDays, licenseImportedUtc);

    public static SignedLicense With(params LicensedModule[] modules) =>
        Issued(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc), modules);

    public static SignedLicense Issued(DateTime issuedAtUtc, params LicensedModule[] modules) => new(
        Guid.CreateVersion7(),
        issuedAtUtc,
        FakeMachine.Id,
        "Cliente",
        [.. modules.Select(m => new ModuleGrant(m, new DateOnly(2026, 1, 1), null))]);
}
