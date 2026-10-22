namespace Pos.Domain.Products;

/// <summary>
/// Resumen de la migración de productos (026): una sola fila que se muestra una vez tras actualizar.
/// </summary>
public sealed class ProductMigrationSummary
{
    public const int SingletonId = 1;

    private ProductMigrationSummary()
    {
    }

    public int Id { get; private set; }

    public int MigratedCount { get; private set; }

    public int WithoutModelCount { get; private set; }

    public int WithoutPackagingCount { get; private set; }

    public int DuplicateCount { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public bool Shown { get; private set; }

    public static ProductMigrationSummary Create(
        int migrated,
        int withoutModel,
        int withoutPackaging,
        int duplicates,
        DateTime utcNow) => new()
        {
            Id = SingletonId,
            MigratedCount = migrated,
            WithoutModelCount = withoutModel,
            WithoutPackagingCount = withoutPackaging,
            DuplicateCount = duplicates,
            CreatedAtUtc = utcNow,
        };

    public void MarkShown() => Shown = true;
}
