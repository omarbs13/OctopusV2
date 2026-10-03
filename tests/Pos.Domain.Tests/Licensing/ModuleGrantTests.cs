using Pos.Domain.Licensing;

namespace Pos.Domain.Tests.Licensing;

/// <summary>025, FR-010/FR-031 y regla de antigüedad (FR-018): vigencia inclusiva y reemplazo de licencias.</summary>
public sealed class ModuleGrantTests
{
    private static DateOnly Day(int day) => new(2026, 10, day);

    [Fact]
    public void Vencimiento_EsInclusivo()
    {
        var grant = new ModuleGrant(LicensedModule.Inventory, Day(1), Day(15));

        Assert.True(grant.IsActiveOn(Day(15)));
        Assert.False(grant.IsActiveOn(Day(16)));
        Assert.False(grant.IsActiveOn(new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void SinVencimiento_ActivoDesdeLaActivacionEnAdelante()
    {
        var grant = new ModuleGrant(LicensedModule.Inventory, Day(1), null);

        Assert.True(grant.IsActiveOn(Day(1)));
        Assert.True(grant.IsActiveOn(new DateOnly(2099, 1, 1)));
    }

    [Fact]
    public void VencimientoAnteriorALaActivacion_NuncaActivo()
    {
        var grant = new ModuleGrant(LicensedModule.Inventory, Day(10), Day(5));

        Assert.True(grant.IsInvalid);
        Assert.False(grant.IsActiveOn(Day(5)));
        Assert.False(grant.IsActiveOn(Day(10)));
    }

    [Fact]
    public void Antiguedad_MismoIdOEmitidaDespues_SeAcepta_LoDemasNo()
    {
        var issued = new DateTime(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc);
        var current = new SignedLicense(Guid.CreateVersion7(), issued, "m", "C", []);

        Assert.True(current.IsAcceptableReplacementFor(null));
        Assert.True((current with { IssuedAtUtc = issued.AddDays(-1) }).IsAcceptableReplacementFor(current));
        Assert.True((current with { LicenseId = Guid.CreateVersion7(), IssuedAtUtc = issued.AddSeconds(1) }).IsAcceptableReplacementFor(current));
        Assert.False((current with { LicenseId = Guid.CreateVersion7() }).IsAcceptableReplacementFor(current));
        Assert.False((current with { LicenseId = Guid.CreateVersion7(), IssuedAtUtc = issued.AddSeconds(-1) }).IsAcceptableReplacementFor(current));
    }
}
