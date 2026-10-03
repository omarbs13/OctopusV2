using Microsoft.EntityFrameworkCore;
using Pos.Application.Licensing;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Licensing;

/// <summary>Licencia importada en la tabla <c>InstalledLicenses</c> (025, research §3): una sola fila, reemplazo atómico.</summary>
public sealed class InstalledLicenseStore : IInstalledLicenseStore
{
    private readonly IDbContextFactory<PosDbContext> _contexts;

    public InstalledLicenseStore(IDbContextFactory<PosDbContext> contexts) => _contexts = contexts;

    public async Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        await using var db = await _contexts.CreateDbContextAsync(cancellationToken);
        return await db.InstalledLicenses.AsNoTracking()
            .OrderByDescending(e => e.ImportedAtUtc)
            .Select(e => e.Content)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ReplaceAsync(string content, DateTime importedAtUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using var db = await _contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.InstalledLicenses.RemoveRange(await db.InstalledLicenses.ToListAsync(cancellationToken));
        db.InstalledLicenses.Add(new InstalledLicenseEntity
        {
            Id = Guid.CreateVersion7(),
            Content = content,
            ImportedAtUtc = DateTime.SpecifyKind(importedAtUtc, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
