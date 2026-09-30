using Microsoft.Data.Sqlite;

namespace Pos.Infrastructure.Persistence;

public static class SqliteConnectionStrings
{
    /// <summary>
    /// Cadena de conexión de la base principal. El tiempo de espera corto evita que la UI quede
    /// bloqueada si otro programa retiene la base.
    /// </summary>
    public static string For(string databaseFile, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = mode,
            Pooling = true,
            DefaultTimeout = 5,
        }.ToString();
}
