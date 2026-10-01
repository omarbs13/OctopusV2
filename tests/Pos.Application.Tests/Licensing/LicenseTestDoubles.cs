using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Licensing;

namespace Pos.Application.Tests.Licensing;

internal sealed class FakeLicenseStore : ILicenseStore
{
    public LicenseLoadResult Next { get; set; } = new LicenseLoadResult.Missing();

    public LicenseRecord? Saved { get; private set; }

    public LicenseLoadResult Load() => Next;

    public void Save(LicenseRecord record) => Saved = record;
}

internal sealed class FakeSealStore : ILicenseSealStore
{
    public LicenseSeal? Seal { get; set; }

    public Task<LicenseSeal?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Seal);

    public Task WriteAsync(LicenseSeal seal, CancellationToken cancellationToken)
    {
        Seal = seal;
        return Task.CompletedTask;
    }
}

internal sealed class FakeMachine : IMachineIdProvider
{
    public string GetMachineId() => "m";
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
