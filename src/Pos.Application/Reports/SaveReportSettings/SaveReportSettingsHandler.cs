using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Users.Access;
using Pos.Domain.Audit;
using Pos.Domain.Discounts;
using Pos.Domain.Users;

namespace Pos.Application.Reports.SaveReportSettings;

/// <summary>Guarda el umbral de alerta de arqueo (0.01 % a 100 %); solo quien tiene <c>ManageSettings</c>. Se audita.</summary>
public sealed class SaveReportSettingsHandler
{
    private readonly IAccessControl _access;
    private readonly IReportSettingsStore _store;
    private readonly IAuditLog _audit;

    public SaveReportSettingsHandler(IAccessControl access, IReportSettingsStore store, IAuditLog audit)
    {
        _access = access;
        _store = store;
        _audit = audit;
    }

    public async Task<Result> HandleAsync(long thresholdBasisPoints, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageSettings, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        if (thresholdBasisPoints is < ReportSettings.MinThresholdBasisPoints or > ReportSettings.MaxThresholdBasisPoints)
        {
            return Result.Failure(new ValidationFailed([new FieldError(ReportFields.AlertThreshold, ReportMessages.ThresholdRange)]));
        }

        var previous = _store.Load();
        _store.Save(new ReportSettings { CashDifferenceAlertBasisPoints = thresholdBasisPoints });

        // Sin cambio no se registra nada (018, FR-002).
        if (previous.CashDifferenceAlertBasisPoints != thresholdBasisPoints)
        {
            _audit.Add(new AuditRecord(
                AuditActions.ReportSettingsChanged,
                AuditActions.ReportEntity,
                Guid.CreateVersion7(),
                Details: $"Umbral de alerta de arqueo: {previous.CashDifferenceAlertBasisPoints} -> {thresholdBasisPoints} (centésimas de %)",
                Changes: [new AuditFieldChange("Umbral de alerta de arqueo", DiscountValue.FormatPercent(previous.CashDifferenceAlertBasisPoints), DiscountValue.FormatPercent(thresholdBasisPoints))],
                AuthorizedBy: access.AuthorizedBy));
            await _audit.SaveAsync(cancellationToken);
        }

        return Result.Success();
    }
}
