using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Domain.Audit;

namespace Pos.Infrastructure.Persistence;

/// <summary>
/// Asigna fecha y usuario de creación y de última modificación, e incrementa la versión de
/// concurrencia de toda entidad modificada. Una entrada de bitácora nunca queda sin autor: sin
/// usuario identificado, su autor es "Sistema" (018, FR-010).
/// </summary>
public sealed partial class AuditingInterceptor : SaveChangesInterceptor
{
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuditingInterceptor> _logger;

    public AuditingInterceptor(IClock clock, ICurrentUser currentUser, ILogger<AuditingInterceptor>? logger = null)
    {
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger ?? NullLogger<AuditingInterceptor>.Instance;
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
                case EntityState.Added when entry.Entity is AuditEntry audit && user == Guid.Empty:
                    // No se rechaza el guardado: detendría la operación por un defecto de sesión (Principio I).
                    LogAuditWithoutUser(_logger, audit.Action, audit.EntityType);
                    SetIfPresent(entry, "CreatedAt", now);
                    SetIfPresent(entry, "CreatedBy", SystemUser.Id);
                    break;

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entrada de auditoría {Action} sobre {EntityType} sin usuario actual; se registra como Sistema")]
    private static partial void LogAuditWithoutUser(ILogger logger, string action, string entityType);
}
