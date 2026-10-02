using FluentValidation;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Audit.SearchAuditLog;

/// <summary>
/// Consulta de la bitácora, solo para el Administrador, del más reciente al más antiguo; el historial de un
/// registro, del más antiguo al más reciente (FR-027; 018, FR-017).
/// </summary>
public sealed class SearchAuditLogHandler
{
    private readonly IAccessControl _access;
    private readonly IAuditLogReader _reader;
    private readonly IValidator<SearchAuditLogQuery> _validator;

    public SearchAuditLogHandler(IAccessControl access, IAuditLogReader reader, IValidator<SearchAuditLogQuery> validator)
    {
        _access = access;
        _reader = reader;
        _validator = validator;
    }

    public async Task<Result<AuditPage>> HandleAsync(SearchAuditLogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ViewAuditLog, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<AuditPage>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AuditPage>(ProductRules.ToError(validation));
        }

        var filter = new AuditFilter(query.FromUtc, query.ToUtcExclusive, query.UserId, query.Action, query.Entity, query.Record);
        var search = new AuditSearch(filter, Math.Max(query.Page, 1), AuditPage.DefaultPageSize);
        return Result.Success(await _reader.SearchAsync(search, cancellationToken));
    }
}
