using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Users;
using Pos.Application.Users.Access;
using Pos.Application.Users.Session;
using Pos.Domain.Users;

namespace Pos.Application.Tests.TestSupport;

/// <summary>Armado común de las pruebas de usuarios: repositorio, hasher, reloj, sesión y verificación de credenciales.</summary>
public sealed class AuthFixture
{
    public const string Password = "secreto123";

    public AuthFixture()
    {
        Throttle = new LoginThrottle(Clock);
        Grants = new AuthorizationGrants(Clock);
        Verifier = new CredentialVerifier(Users, Hasher, Throttle, Audit, Transactions, Clock, NullLogger<CredentialVerifier>.Instance);
        Access = new AccessControl(Session, Users, Grants, NullLogger<AccessControl>.Instance);
    }

    public InMemoryUserRepository Users { get; } = new();

    public FastPasswordHasher Hasher { get; } = new();

    public FakeClock Clock { get; } = new();

    public RecordingAuditLog Audit { get; } = new();

    public FakeWriteTransactions Transactions { get; } = new();

    public UserSession Session { get; } = new();

    public LoginThrottle Throttle { get; }

    public AuthorizationGrants Grants { get; }

    public CredentialVerifier Verifier { get; }

    public AccessControl Access { get; }

    public User AddUser(string userName, UserRole role, bool active = true, string password = Password, bool mustChange = false)
    {
        var user = User.Create($"Usuario {userName}", userName, role, Hasher.Hash(password));
        if (!active)
        {
            user.Deactivate();
        }

        if (!mustChange)
        {
            user.SetPassword(Hasher.Hash(password), mustChange: false);
        }

        return Users.Seed(user);
    }

    /// <summary>Abre la sesión del usuario como lo hace el inicio de sesión real.</summary>
    public void SignedIn(User user)
    {
        var sessionUser = SessionUser.From(user);
        Session.Seal(sessionUser);
        Assert.True(Session.TryStart(sessionUser));
    }
}
