using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pos.Domain.Products;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Startup;

/// <summary>
/// Paso de arranque de la funcionalidad 026: después de migrar, calcula las claves normalizadas de los
/// productos anteriores (la normalización con acentos no se puede expresar en SQL), deja con clave nula y
/// "para revisión" a los duplicados (el más antiguo conserva la clave) y escribe el resumen de la migración.
/// Solo toca productos sin procesar (<c>ModelKey</c> vacío), así que es repetible: un respaldo anterior que
/// se restaura recibe el mismo tratamiento. No cambia la versión ni la fecha de los productos.
/// </summary>
public static class ProductIdentityBackfill
{
    /// <summary>Migración que crea las columnas y la tabla del resumen; sin ella no hay nada que calcular.</summary>
    private const string RequiredMigrationSuffix = "_TileSupplierFields";

    public static async Task<ProductBackfillResult?> RunAsync(PosDbContext context, DateTime utcNow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        if (!applied.Any(m => m.EndsWith(RequiredMigrationSuffix, StringComparison.Ordinal)))
        {
            return null;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var connection = context.Database.GetDbConnection();
        var dbTransaction = transaction.GetDbTransaction();

        var pending = await ReadPendingAsync(connection, dbTransaction, cancellationToken);
        if (pending.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var taken = await ReadTakenKeysAsync(connection, dbTransaction, cancellationToken);
        var duplicates = 0;
        var withoutModel = 0;
        var withoutPackaging = 0;

        await using var update = connection.CreateCommand();
        update.Transaction = dbTransaction;
        update.CommandText = """
            UPDATE "Products"
            SET "ModelKey" = $modelKey, "MeasuresKey" = $measuresKey, "BrandSearch" = $brandSearch,
                "IdentityKey" = $identityKey, "NeedsReview" = $needsReview
            WHERE "Id" = $id
            """;
        var id = Parameter(update, "$id");
        var modelKey = Parameter(update, "$modelKey");
        var measuresKey = Parameter(update, "$measuresKey");
        var brandSearch = Parameter(update, "$brandSearch");
        var identityKey = Parameter(update, "$identityKey");
        var needsReview = Parameter(update, "$needsReview");

        foreach (var row in pending.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id, StringComparer.Ordinal))
        {
            var review = row.NeedsReview;
            string? identity = null;
            if (!row.IsDeleted)
            {
                if (string.IsNullOrWhiteSpace(row.Model))
                {
                    withoutModel++;
                    review = true;
                }

                if (UnitOfMeasure.IsTile(row.UnitCode) && row.PackagingThousandths is null)
                {
                    withoutPackaging++;
                    review = true;
                }

                var key = ProductText.BuildIdentityKey(row.SupplierId, row.Model, row.Measures);
                if (taken.Add(key))
                {
                    identity = key;
                }
                else
                {
                    duplicates++;
                    review = true;
                }
            }

            id.Value = row.Id;
            modelKey.Value = ProductText.NormalizeModel(row.Model);
            measuresKey.Value = (object?)ProductText.NormalizeMeasures(row.Measures) ?? DBNull.Value;
            brandSearch.Value = (object?)ProductText.NormalizeBrand(row.Brand) ?? DBNull.Value;
            identityKey.Value = (object?)identity ?? DBNull.Value;
            needsReview.Value = review ? 1 : 0;
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        var result = new ProductBackfillResult(pending.Count(p => !p.IsDeleted), withoutModel, withoutPackaging, duplicates);
        await InsertSummaryIfMissingAsync(connection, dbTransaction, result, utcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task<List<PendingProduct>> ReadPendingAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT "Id", "Model", "Measures", "Brand", "SupplierId", "UnitCode", "PackagingThousandths",
                   "NeedsReview", "DeletedAt" IS NOT NULL, "CreatedAt"
            FROM "Products"
            WHERE "ModelKey" = ''
            """;
        var rows = new List<PendingProduct>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PendingProduct(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt64(6),
                reader.GetInt64(7) != 0,
                reader.GetInt64(8) != 0,
                reader.GetString(9)));
        }

        return rows;
    }

    private static async Task<HashSet<string>> ReadTakenKeysAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """SELECT "IdentityKey" FROM "Products" WHERE "IdentityKey" IS NOT NULL AND "DeletedAt" IS NULL""";
        var keys = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            keys.Add(reader.GetString(0));
        }

        return keys;
    }

    private static async Task InsertSummaryIfMissingAsync(
        DbConnection connection,
        DbTransaction transaction,
        ProductBackfillResult result,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO "ProductMigrationSummaries"
                ("Id", "MigratedCount", "WithoutModelCount", "WithoutPackagingCount", "DuplicateCount", "CreatedAtUtc", "Shown")
            SELECT $id, $migrated, $withoutModel, $withoutPackaging, $duplicates, $createdAt, 0
            WHERE NOT EXISTS (SELECT 1 FROM "ProductMigrationSummaries")
            """;
        Parameter(command, "$id").Value = ProductMigrationSummary.SingletonId;
        Parameter(command, "$migrated").Value = result.Migrated;
        Parameter(command, "$withoutModel").Value = result.WithoutModel;
        Parameter(command, "$withoutPackaging").Value = result.WithoutPackaging;
        Parameter(command, "$duplicates").Value = result.Duplicates;
        Parameter(command, "$createdAt").Value = utcNow.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbParameter Parameter(DbCommand command, string name)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        command.Parameters.Add(parameter);
        return parameter;
    }

    private sealed record PendingProduct(
        string Id,
        string Model,
        string? Measures,
        string? Brand,
        Guid? SupplierId,
        string UnitCode,
        long? PackagingThousandths,
        bool NeedsReview,
        bool IsDeleted,
        string CreatedAt);
}

/// <summary>Conteos del paso de migración de productos (026).</summary>
public sealed record ProductBackfillResult(int Migrated, int WithoutModel, int WithoutPackaging, int Duplicates);
