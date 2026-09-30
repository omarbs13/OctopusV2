using Pos.Application.Abstractions;
using Pos.Application.Users;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Security.SaveSecuritySettings;

/// <summary>Guarda el tiempo de inactividad (0 = desactivado); solo quien tiene <c>ManageSettings</c>.</summary>
public sealed class SaveSecuritySettingsHandler
{
    private readonly IAccessControl _access;
    private readonly ISecuritySettingsStore _store;

    public SaveSecuritySettingsHandler(IAccessControl access, ISecuritySettingsStore store)
    {
        _access = access;
        _store = store;
    }

    public async Task<Result> HandleAsync(SecuritySettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var access = await _access.CheckAsync(Permission.ManageSettings, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        if (settings.IdleLockMinutes is < 0 or > SecuritySettings.MaxIdleLockMinutes)
        {
            return Result.Failure(new ValidationFailed([new FieldError(UserFields.IdleLockMinutes, UserMessages.IdleLockRange)]));
        }

        _store.Save(settings);
        return Result.Success();
    }
}
