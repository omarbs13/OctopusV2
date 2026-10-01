using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Domain.Receivables;

namespace Pos.Application.Receivables;

/// <summary>
/// Atraso de las cuentas por cobrar con el plazo actual y la fecha local de hoy (research §9). El
/// vencimiento no se guarda: un cambio de plazo afecta a todas las cuentas.
/// </summary>
public sealed class CreditAging
{
    private readonly IClock _clock;
    private readonly IReceivablesSettingsStore _settings;
    private readonly ReportPeriodResolver _periods;

    public CreditAging(IClock clock, IReceivablesSettingsStore settings, ReportPeriodResolver periods)
    {
        _clock = clock;
        _settings = settings;
        _periods = periods;
    }

    /// <summary>Plazo y fecha de hoy fijos para calcular varias cuentas con los mismos valores.</summary>
    public CreditAgingSnapshot Now() =>
        new(_periods, _periods.ToLocalDate(_clock.UtcNow), _settings.Load().PaymentTermDays);
}

public sealed class CreditAgingSnapshot
{
    private readonly ReportPeriodResolver _periods;

    internal CreditAgingSnapshot(ReportPeriodResolver periods, DateOnly today, int termDays)
    {
        _periods = periods;
        Today = today;
        TermDays = termDays;
    }

    public DateOnly Today { get; }

    public int TermDays { get; }

    /// <summary>Días vencido de una cuenta cuya venta fue en <paramref name="saleUtc"/>.</summary>
    public int DaysOverdue(DateTime saleUtc) =>
        ReceivableAging.DaysOverdue(_periods.ToLocalDate(saleUtc), Today, TermDays);
}
