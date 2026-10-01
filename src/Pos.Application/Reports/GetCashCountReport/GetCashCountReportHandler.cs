using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Reports;
using Pos.Domain.Users;

namespace Pos.Application.Reports.GetCashCountReport;

/// <summary>
/// Arqueo por turno del período; solo quien tiene <c>ViewReports</c>. Calcula porcentaje y alerta con
/// <see cref="CashDifferenceRule"/> solo en turnos cerrados; los abiertos quedan "En curso" sin cifras de
/// efectivo (FR-009, FR-010).
/// </summary>
public sealed class GetCashCountReportHandler
{
    private readonly IAccessControl _access;
    private readonly ICashCountReportReader _reader;
    private readonly ReportPeriodResolver _resolver;
    private readonly IReportSettingsStore _settings;

    public GetCashCountReportHandler(
        IAccessControl access,
        ICashCountReportReader reader,
        ReportPeriodResolver resolver,
        IReportSettingsStore settings)
    {
        _access = access;
        _reader = reader;
        _resolver = resolver;
        _settings = settings;
    }

    public async Task<Result<CashCountReport>> HandleAsync(CashCountReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ViewReports, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CashCountReport>(access.Error!);
        }

        var raw = await _reader.GetAsync(_resolver.Resolve(query.Period), query.CashierId, cancellationToken);
        return Result.Success(Build(raw, _settings.Load().CashDifferenceAlertBasisPoints));
    }

    /// <summary>Completa porcentaje y alerta; también lo usa la tarjeta de alertas de Inicio.</summary>
    internal static CashCountReport Build(IReadOnlyList<CashCountRawRow> raw, long thresholdBasisPoints)
    {
        var rows = raw.Select(r => ToRow(r, thresholdBasisPoints)).ToList();
        var closed = rows.Where(r => !r.IsOpen).ToList();
        return new CashCountReport(
            rows,
            new CashCountTotals(closed.Count, rows.Sum(r => r.TotalSoldCents), closed.Sum(r => r.DifferenceCents ?? 0)),
            thresholdBasisPoints);
    }

    private static CashCountRow ToRow(CashCountRawRow r, long thresholdBasisPoints)
    {
        if (r.IsOpen)
        {
            return new CashCountRow(
                r.ShiftId, r.FolioText, r.CashierName, r.OpenedAtUtc, r.ClosedAtUtc, r.OpeningFloatCents, r.TotalSoldCents,
                r.DepositsCents, r.WithdrawalsCents, true, null, null, null, null, false);
        }

        var basisPoints = r.DifferenceCents is { } difference && r.ExpectedCashCents is { } expected
            ? CashDifferenceRule.PercentBasisPoints(difference, expected)
            : null;
        return new CashCountRow(
            r.ShiftId, r.FolioText, r.CashierName, r.OpenedAtUtc, r.ClosedAtUtc, r.OpeningFloatCents, r.TotalSoldCents,
            r.DepositsCents, r.WithdrawalsCents, false, r.ExpectedCashCents, r.CountedCashCents, r.DifferenceCents,
            basisPoints, CashDifferenceRule.IsAlert(basisPoints, thresholdBasisPoints));
    }
}
