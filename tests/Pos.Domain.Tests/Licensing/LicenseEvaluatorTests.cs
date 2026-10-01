using Pos.Domain.Licensing;

namespace Pos.Domain.Tests.Licensing;

/// <summary>011, H2 y H3: días restantes y vencimiento.</summary>
public sealed class LicenseEvaluatorTests
{
    private static readonly DateTime FirstRun = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static LicenseRecord Trial(DateTime? lastSeen = null) => new(1, "m", FirstRun, lastSeen ?? FirstRun, null);

    private static LicenseStatus At(LicenseRecord record, int daysAfterStart) =>
        LicenseEvaluator.Evaluate(record, FirstRun.AddDays(daysAfterStart), TimeZoneInfo.Utc);

    [Theory]
    [InlineData(0, 30)]
    [InlineData(12, 18)]
    [InlineData(29, 1)]
    public void Evaluacion_CuentaLosDiasRestantes(int daysAfterStart, int expected)
    {
        var status = At(Trial(), daysAfterStart);

        Assert.Equal(LicenseKind.Trial, status.Kind);
        Assert.Equal(expected, status.DaysRemaining);
        Assert.False(status.IsReadOnly);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    public void Evaluacion_ConCeroDiasRestantes_YaEstaVencida(int daysAfterStart)
    {
        var status = At(Trial(), daysAfterStart);

        Assert.Equal(LicenseKind.Expired, status.Kind);
        Assert.True(status.IsReadOnly);
    }

    [Theory]
    [InlineData(25, LicenseWarning.Near)]
    [InlineData(29, LicenseWarning.Urgent)]
    [InlineData(10, LicenseWarning.None)]
    public void Evaluacion_AvisaACincoYUnDia(int daysAfterStart, LicenseWarning expected) =>
        Assert.Equal(expected, At(Trial(), daysAfterStart).Warning);

    [Fact]
    public void RelojRetrasado_NoDevuelveDias()
    {
        var record = Trial(lastSeen: FirstRun.AddDays(20));

        var status = At(record, 5);

        Assert.Equal(10, status.DaysRemaining);
    }

    [Fact]
    public void Concesion_ConFechaDeFin_CuentaHastaEsaFecha()
    {
        var grant = new LicenseGrant("m", FirstRun, new DateOnly(2026, 12, 1), "firma");
        var record = Trial() with { Grant = grant };

        Assert.Equal(DaysUntil(record, new DateOnly(2026, 12, 1)), At(record, 31).DaysRemaining);
        Assert.Equal(LicenseKind.Expired, LicenseEvaluator.Evaluate(record, new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Utc).Kind);
    }

    [Fact]
    public void Concesion_SinVencimiento_NuncaVence()
    {
        var record = Trial() with { Grant = new LicenseGrant("m", FirstRun, null, "firma") };

        var status = At(record, 4000);

        Assert.Equal(LicenseKind.Licensed, status.Kind);
        Assert.Null(status.DaysRemaining);
        Assert.False(status.IsReadOnly);
    }

    private static int DaysUntil(LicenseRecord record, DateOnly end) =>
        end.DayNumber - DateOnly.FromDateTime(FirstRun.AddDays(31)).DayNumber;
}
