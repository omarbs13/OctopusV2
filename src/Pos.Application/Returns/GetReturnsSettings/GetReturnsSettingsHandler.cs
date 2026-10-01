using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Returns.GetReturnsSettings;

/// <summary>Lee el plazo de devoluciones; solo <c>ManageCreditNotes</c>.</summary>
public sealed class GetReturnsSettingsHandler
{
    private readonly IAccessControl _access;
    private readonly IReturnsSettingsStore _store;

    public GetReturnsSettingsHandler(IAccessControl access, IReturnsSettingsStore store)
    {
        _access = access;
        _store = store;
    }

    public async Task<Result<ReturnsSettings>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageCreditNotes, cancellationToken);
        return access.Allowed ? Result.Success(_store.Load()) : Result.Failure<ReturnsSettings>(access.Error!);
    }
}
