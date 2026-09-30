using Pos.Application.Abstractions;

namespace Pos.Application.Security.GetSecuritySettings;

/// <summary>Lee la configuración de seguridad; la usa el bloqueo por inactividad de cualquier sesión.</summary>
public sealed class GetSecuritySettingsHandler
{
    private readonly ISecuritySettingsStore _store;

    public GetSecuritySettingsHandler(ISecuritySettingsStore store) => _store = store;

    public Result<SecuritySettings> Handle() => Result.Success(_store.Load());
}
