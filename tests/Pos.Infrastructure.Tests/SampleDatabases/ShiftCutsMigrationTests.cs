using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (017): la migración <c>ShiftCuts</c> solo crea la tabla de cortes y sus cinco índices, sin
/// reconstruir ninguna tabla ni insertar filas: los turnos anteriores no reciben Corte Z (FR-010a).
/// </summary>
public sealed class ShiftCutsMigrationTests
{
    private static readonly string[] Indexes =
    [
        "IX_ShiftCuts_Type_Number",
        "IX_ShiftCuts_ClosingPerShift",
        "IX_ShiftCuts_ShiftId",
        "IX_ShiftCuts_GeneratedAt",
        "IX_ShiftCuts_GeneratedBy_GeneratedAt",
    ];

    [Fact]
    public async Task ShiftCuts_SoloCreaLaTablaYSusIndices()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var cuts = migrations.Single(m => m.EndsWith("_ShiftCuts", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(cuts) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, cuts);

        Assert.Contains("CREATE TABLE \"ShiftCuts\"", script, StringComparison.Ordinal);
        Assert.Equal(1, script.Split('\n').Count(l => l.StartsWith("CREATE TABLE", StringComparison.Ordinal)));
        Assert.Equal(Indexes.Length, script.Split('\n').Count(l => l.Contains("INDEX \"IX_ShiftCuts_", StringComparison.Ordinal)));
        Assert.Contains("CREATE UNIQUE INDEX \"IX_ShiftCuts_ClosingPerShift\" ON \"ShiftCuts\" (\"ShiftId\") WHERE \"Type\" = 'Z'", script, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_ShiftCuts_Type_Number\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO \"ShiftCuts\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShiftCuts_EsLaUltimaMigracion()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();

        Assert.EndsWith("_ShiftCuts", context.Database.GetMigrations().Last(), StringComparison.Ordinal);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TurnosAnterioresA0120_SeConservanYNoRecibenCorteZ()
    {
        using var dir = new TempDataDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "SampleDatabases", "v0.11.0.db"), dir.Paths.DatabaseFile);
        var before = ReadShifts(dir.Paths.DatabaseFile);
        using (var db = TestDb.CreateUnmigrated(dir))
        await using (var context = db.CreateDbContext())
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(before);
        Assert.Equal(before, ReadShifts(dir.Paths.DatabaseFile));
        using var connection = Open(dir.Paths.DatabaseFile);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ShiftCuts";
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
        foreach (var index in Indexes)
        {
            command.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{index}'";
            Assert.Equal(1L, (long)command.ExecuteScalar()!);
        }
    }

    private static List<string> ReadShifts(string file)
    {
        using var connection = Open(file);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM CashShifts ORDER BY Id";
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add(string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "∅" : reader.GetValue(i).ToString())));
        }

        return rows;
    }

    private static SqliteConnection Open(string file)
    {
        var connection = new SqliteConnection($"Data Source={file};Pooling=False");
        connection.Open();
        return connection;
    }
}
