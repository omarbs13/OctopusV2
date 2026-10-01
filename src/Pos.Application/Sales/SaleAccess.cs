using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Sales;

/// <summary>Regla de propiedad de las ventas: un cajero solo accede a las suyas (007, FR-026).</summary>
internal static class SaleAccess
{
    /// <summary>Nulo si el usuario puede acceder a la venta creada por <paramref name="createdBy"/>; si no, el rechazo.</summary>
    public static async Task<Error?> CheckOwnershipAsync(
        IAccessControl access,
        ICurrentUser currentUser,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        if (createdBy == currentUser.UserId)
        {
            return null;
        }

        var all = await access.CheckAsync(Permission.ViewAllSales, cancellationToken);
        return all.Allowed ? null : all.Error;
    }
}
