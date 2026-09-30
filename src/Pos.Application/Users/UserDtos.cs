using Pos.Domain.Users;

namespace Pos.Application.Users;

/// <summary>Usuario para edición; nunca incluye la credencial. <c>HasHeldSale</c>: tiene una venta conservada.</summary>
public sealed record UserDto(Guid Id, string FullName, string UserName, UserRole Role, bool IsActive, int Version, bool HasHeldSale = false);

public sealed record UserListItem(Guid Id, string FullName, string UserName, UserRole Role, bool IsActive, int Version);

/// <summary><c>NameText</c> va sin acentos y en minúsculas; nulo si no hay texto.</summary>
public sealed record UserSearch(string? NameText, bool IncludeInactive, int Page, int PageSize);

public sealed record UserPage(IReadOnlyList<UserListItem> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

/// <summary>Usuario como opción de un filtro.</summary>
public sealed record UserOption(Guid Id, string FullName);

public sealed record SignInOutcome(SessionUser User, bool MustChangePassword);

public sealed record SetupState(bool NeedsFirstAdmin);

/// <summary>Resultado de verificar un permiso.</summary>
public sealed record AccessDecision(bool Allowed, Guid? AuthorizedBy, Abstractions.Forbidden? Error)
{
    public static AccessDecision Allow(Guid? authorizedBy = null) => new(true, authorizedBy, null);

    public static AccessDecision Deny(Abstractions.Forbidden error) => new(false, null, error);
}
