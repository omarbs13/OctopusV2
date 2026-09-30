using Pos.Application.Abstractions;

namespace Pos.Infrastructure.Platform;

/// <summary>
/// Usuario de sistema fijo mientras no exista autenticación. Se sustituirá por el usuario real
/// registrando otra implementación de <see cref="ICurrentUser"/>, sin cambiar los casos de uso.
/// </summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public static readonly Guid SystemUserId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    public Guid UserId => SystemUserId;
}
