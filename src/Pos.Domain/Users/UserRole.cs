using Pos.Domain.Common;

namespace Pos.Domain.Users;

public enum UserRole
{
    Admin,
    Cashier,
}

public static class UserRoleExtensions
{
    public static string ToCode(this UserRole role) => role switch
    {
        UserRole.Admin => "ADMIN",
        UserRole.Cashier => "CASHIER",
        _ => throw new DomainException("El rol del usuario no es válido."),
    };

    public static UserRole FromCode(string code) => code switch
    {
        "ADMIN" => UserRole.Admin,
        "CASHIER" => UserRole.Cashier,
        _ => throw new DomainException("El rol del usuario no es válido."),
    };
}
