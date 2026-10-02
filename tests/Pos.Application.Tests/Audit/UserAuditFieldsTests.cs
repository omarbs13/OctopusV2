using Pos.Application.Users;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Audit;

/// <summary>018 (FR-006): la instantánea de auditoría de un usuario nunca contiene la contraseña ni su hash.</summary>
public sealed class UserAuditFieldsTests
{
    private const string Hash = "PBKDF2$hash-secreto-de-prueba";

    [Fact]
    public void Instantanea_SinContrasenaNiHash_ConLosCamposDeLaPantalla()
    {
        var user = User.Create("Ana López", "ana", UserRole.Cashier, Hash);

        var snapshot = UserAuditFields.Snapshot(user);

        Assert.Equal(["Nombre completo", "Usuario", "Rol", "Estado"], snapshot.Select(f => f.Field));
        Assert.Equal(["Ana López", "ana", "Cajero", "Activo"], snapshot.Select(f => f.Value));
        Assert.DoesNotContain(snapshot, f => f.Field.Contains("contraseña", StringComparison.OrdinalIgnoreCase)
            || f.Field.Contains("hash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(snapshot, f => f.Value is { } value && value.Contains(Hash, StringComparison.Ordinal));
    }
}
