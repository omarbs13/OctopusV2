using Microsoft.EntityFrameworkCore;
using Pos.Application.Licensing;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Licensing;

/// <summary>Fecha de creación del primer usuario real: evidencia de uso previo al regenerar el <c>.lic</c> (011, research §5).</summary>
public sealed class InstallationAgeReader : IInstallationAgeReader
{
    private readonly IDbContextFactory<PosDbContext> _contexts;

    public InstallationAgeReader(IDbContextFactory<PosDbContext> contexts) => _contexts = contexts;

    public async Task<DateTime?> GetFirstUserCreatedUtcAsync(CancellationToken cancellationToken)
    {
        await using var db = await _contexts.CreateDbContextAsync(cancellationToken);
        var dates = db.Users.AsNoTracking().Where(u => !u.IsSystem).Select(u => (DateTime?)u.CreatedAt);
        return await dates.MinAsync(cancellationToken);
    }
}
