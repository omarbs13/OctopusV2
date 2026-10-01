using Pos.Domain.Common;
using Pos.Domain.Reports;

namespace Pos.Domain.Tests.Reports;

public class ReportPeriodTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    [Fact]
    public void Presets_ResuelvenSusRangos()
    {
        Assert.Equal((Today, Today), Range(ReportPeriod.Today(Today)));
        Assert.Equal((new DateOnly(2026, 9, 29), new DateOnly(2026, 9, 29)), Range(ReportPeriod.Yesterday(Today)));
        Assert.Equal((new DateOnly(2026, 9, 24), Today), Range(ReportPeriod.Last7Days(Today)));
        Assert.Equal((new DateOnly(2026, 9, 1), Today), Range(ReportPeriod.ThisMonth(Today)));
        Assert.Equal((new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), Range(ReportPeriod.PreviousMonth(Today)));
    }

    [Fact]
    public void Previous_TieneLaMismaDuracionYTerminaElDiaAnterior()
    {
        var last7 = ReportPeriod.Last7Days(Today).Previous();
        Assert.Equal((new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 23)), Range(last7));

        // "Este mes" (30 días a hoy) → los 30 días anteriores, no el mes calendario anterior.
        var thisMonth = ReportPeriod.ThisMonth(Today).Previous();
        Assert.Equal((new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 31)), Range(thisMonth));
        Assert.Equal(30, thisMonth.Days);
    }

    [Fact]
    public void RangoInvalido_SeRechaza()
    {
        Assert.Equal(ReportPeriodError.EndBeforeStart, ReportPeriod.Validate(Today, Today.AddDays(-1)));
        Assert.Equal(ReportPeriodError.TooLong, ReportPeriod.Validate(Today.AddDays(-366), Today));
        Assert.Null(ReportPeriod.Validate(Today.AddDays(-365), Today));
        Assert.Throws<DomainException>(() => ReportPeriod.Custom(Today, Today.AddDays(-1)));
    }

    private static (DateOnly From, DateOnly To) Range(ReportPeriod period) => (period.FromDate, period.ToDate);
}
