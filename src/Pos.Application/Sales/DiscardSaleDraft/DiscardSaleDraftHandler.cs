using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Sales.DiscardSaleDraft;

/// <summary>Borra el borrador de la venta en curso.</summary>
public sealed class DiscardSaleDraftHandler
{
    private readonly IAccessControl _access;
    private readonly ISaleDraftStore _drafts;

    public DiscardSaleDraftHandler(IAccessControl access, ISaleDraftStore drafts)
    {
        _access = access;
        _drafts = drafts;
    }

    public async Task<Result> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.Sell, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        await _drafts.DiscardAsync(cancellationToken);
        return Result.Success();
    }
}
