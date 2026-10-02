using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (018): la migración <c>AuditTrail</c> solo agrega tres columnas nulas a la bitácora y sus
/// índices (dos se recrean con más columnas), sin reconstruir la tabla ni transformar datos: las entradas anteriores se conservan intactas.
/// </summary>
public sealed class AuditTrailMigrationTests
{
    private static readonly string[] Indexes =
    [
        "IX_AuditEntries_CreatedAt",
        "IX_AuditEntries_CreatedBy",
        "IX_AuditEntries_AuthorizedBy",
        "IX_AuditEntries_Action_CreatedAt",
        "IX_AuditEntries_EntityType_CreatedAt",
    ];

    [Fact]
    public async Task AuditTrail_SoloAgregaColumnasEIndices()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var trail = migrations.Single(m => m.EndsWith("_AuditTrail", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(trail) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, trail);

        Assert.Equal(3, script.Split('\n').Count(l => l.StartsWith("ALTER TABLE \"AuditEntries\" ADD", StringComparison.Ordinal)));
        Assert.Equal(Indexes.Length, script.Split('\n').Count(l => l.StartsWith("CREATE INDEX \"IX_AuditEntries_", StringComparison.Ordinal)));
        Assert.DoesNotContain("CREATE TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE ", script, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO \"AuditEntries\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntradasAnterioresA0130_SeConservanConLasColumnasNuevasNulas()
    {
        using var dir = new TempDataDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "SampleDatabases", "v0.12.0.db"), dir.Paths.DatabaseFile);
        const string Columns = "Id, Action, EntityType, EntityId, Details, CreatedAt, CreatedBy, AuthorizedBy";
        var before = Read(dir.Paths.DatabaseFile, $"SELECT {Columns} FROM AuditEntries ORDER BY Id");
        using (var db = TestDb.CreateUnmigrated(dir))
        await using (var context = db.CreateDbContext())
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(before);
        Assert.Equal(before, Read(dir.Paths.DatabaseFile, $"SELECT {Columns} FROM AuditEntries ORDER BY Id"));
        Assert.Equal(
            ["0"],
            Read(dir.Paths.DatabaseFile, "SELECT COUNT(*) FROM AuditEntries WHERE EntityName IS NOT NULL OR Reason IS NOT NULL OR Changes IS NOT NULL"));
        foreach (var index in Indexes)
        {
            Assert.Equal(["1"], Read(dir.Paths.DatabaseFile, $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{index}'"));
        }
    }

    private static List<string> Read(string file, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={file};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add(string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "∅" : reader.GetValue(i).ToString())));
        }

        return rows;
    }
}
