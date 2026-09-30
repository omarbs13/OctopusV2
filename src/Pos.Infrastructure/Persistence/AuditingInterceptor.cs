using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pos.Application.Abstractions;

namespace Pos.Infrastructure.Persistence;

/// <summary>
/// Asigna fecha y usuario de creación y de última modificación, e incrementa la versión de
/// concurrencia de toda entidad modificada.
/// </summary>
public sealed class AuditingInterceptor : SaveChangesInterceptor
{
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AuditingInterceptor(IClock clock, ICurrentUser currentUser)
    {
        _clock = clock;
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var user = _currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    SetIfPresent(entry, "CreatedAt", now);
                    SetIfPresent(entry, "CreatedBy", user);
                    SetIfPresent(entry, "UpdatedAt", now);
                    SetIfPresent(entry, "UpdatedBy", user);
                    break;

                case EntityState.Modified:
                    SetIfPresent(entry, "UpdatedAt", now);
                    SetIfPresent(entry, "UpdatedBy", user);
                    if (entry.Metadata.FindProperty("Version") is not null)
                    {
                        var version = entry.Property("Version");
                        version.CurrentValue = (int)version.OriginalValue! + 1;
                    }

                    break;
            }
        }
    }

    private static void SetIfPresent(EntityEntry entry, string property, object value)
    {
        if (entry.Metadata.FindProperty(property) is not null)
        {
            entry.Property(property).CurrentValue = value;
        }
    }
}
