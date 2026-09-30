using Pos.Application.Abstractions;

namespace Pos.Infrastructure.Platform;

/// <summary>
/// Usuario de sistema fijo mientras no exista autenticación. Se sustituirá por el usuario real
/// registrando otra implementación de <see cref="ICurrentUser"/>, sin cambiar los casos de uso.
/// </summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public Guid UserId => SystemUser.Id;
}
