using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Returns.SearchPendingReversals;

/// <summary>Reintegros de tarjeta y transferencia pendientes de reversa manual; solo <c>ManageCreditNotes</c>.</summary>
public sealed class SearchPendingReversalsHandler
{
    private readonly IAccessControl _access;
    private readonly IReturnRepository _returns;

    public SearchPendingReversalsHandler(IAccessControl access, IReturnRepository returns)
    {
        _access = access;
        _returns = returns;
    }

    public async Task<Result<ReversalPage>> HandleAsync(SearchPendingReversalsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageCreditNotes, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ReversalPage>(access.Error!);
        }

        return Result.Success(await _returns.SearchReversalsAsync(
            new ReversalSearch(query.Filter, Math.Max(1, query.Page), ReversalPage.DefaultPageSize),
            cancellationToken));
    }
}
