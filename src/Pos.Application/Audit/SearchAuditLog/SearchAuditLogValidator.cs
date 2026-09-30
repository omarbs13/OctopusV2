using FluentValidation;
using Pos.Application.Users;

namespace Pos.Application.Audit.SearchAuditLog;

public sealed class SearchAuditLogValidator : AbstractValidator<SearchAuditLogQuery>
{
    public SearchAuditLogValidator() =>
        RuleFor(q => q)
            .Must(q => q.FromUtc is not { } from || q.ToUtcExclusive is not { } to || from <= to)
            .WithMessage(UserMessages.DateRangeInvalid)
            .OverridePropertyName(UserFields.DateRange);
}
