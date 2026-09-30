using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Tests.TestSupport;

public static class DatabaseTestHelpers
{
    public static async Task<IReadOnlyList<Product>> SeedProductsAsync(
        IDbContextFactory<PosDbContext> factory,
        int count,
        string prefix = "P")
    {
        await using var context = factory.CreateDbContext();
        var products = Enumerable.Range(1, count)
            .Select(i => Product.Create($"Producto {prefix}{i} Café", $"{prefix}-{i:000}", null, Money.FromCents(i * 100)))
            .ToList();
        context.Products.AddRange(products);
        await context.SaveChangesAsync();
        return products;
    }

    /// <summary>Filas de Products leídas directamente, para comparar contenidos.</summary>
    public static IReadOnlyList<string> ReadProductRows(string databaseFile)
    {
        using var connection = new SqliteConnection($"Data Source={databaseFile};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, NameSearch, Sku, IFNULL(Barcode, ''), PriceCents, IsActive, Version, IFNULL(DeletedAt, '')
            FROM Products ORDER BY Id
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(string.Join('|', values));
        }

        return rows;
    }

    public static IReadOnlyList<string> ReadColumns(string databaseFile, string table)
    {
        using var connection = new SqliteConnection($"Data Source={databaseFile};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM pragma_table_info('{table}')";
        using var reader = command.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    public static string QuickCheck(string databaseFile)
    {
        using var connection = new SqliteConnection($"Data Source={databaseFile};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        return (string)command.ExecuteScalar()!;
    }

    public static void Execute(string databaseFile, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={databaseFile};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Sobrescribe la cabecera del archivo para simular una base dañada.</summary>
    public static void CorruptHeader(string databaseFile)
    {
        SqliteConnection.ClearAllPools();
        using var stream = new FileStream(databaseFile, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        stream.Write(Enumerable.Repeat((byte)0xAB, 100).ToArray());
    }

    public static string Hash(string file)
    {
        SqliteConnection.ClearAllPools();
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
