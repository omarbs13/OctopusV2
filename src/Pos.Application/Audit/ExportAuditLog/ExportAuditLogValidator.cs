using FluentValidation;
using Pos.Application.Users;

namespace Pos.Application.Audit.ExportAuditLog;

public sealed class ExportAuditLogValidator : AbstractValidator<ExportAuditLogCommand>
{
    public const string RangeRequired = "Elige un rango de fechas para exportar.";

    public ExportAuditLogValidator()
    {
        RuleFor(c => c.Filter)
            .NotNull()
            .Must(f => f.FromUtc is not null && f.ToUtcExclusive is not null)
            .WithMessage(RangeRequired)
            .OverridePropertyName(UserFields.DateRange);

        RuleFor(c => c.Filter)
            .Must(f => f.FromUtc is not { } from || f.ToUtcExclusive is not { } to || from < to)
            .When(c => c.Filter is not null)
            .WithMessage(UserMessages.DateRangeInvalid)
            .OverridePropertyName(UserFields.DateRange);
    }
}
