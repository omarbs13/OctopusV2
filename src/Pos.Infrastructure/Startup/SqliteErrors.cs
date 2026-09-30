using Microsoft.Data.Sqlite;
using Pos.Application.Startup;

namespace Pos.Infrastructure.Startup;

/// <summary>Traduce errores de SQLite y de E/S a problemas de acceso del dominio de arranque.</summary>
internal static class SqliteErrors
{
    // Códigos primarios de SQLite: https://www.sqlite.org/rescode.html
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;
    private const int SqliteReadOnly = 8;
    private const int SqliteCorrupt = 11;
    private const int SqliteFull = 13;
    private const int SqliteCantOpen = 14;
    private const int SqliteNotADatabase = 26;

    // Disco lleno: ERROR_DISK_FULL / ERROR_HANDLE_DISK_FULL en Windows, ENOSPC en Unix.
    private const int WindowsDiskFull = 0x70;
    private const int WindowsHandleDiskFull = 0x27;
    private const int UnixNoSpace = 28;

    public static DatabaseProblem? Classify(Exception exception) => exception switch
    {
        SqliteException { SqliteErrorCode: SqliteCorrupt or SqliteNotADatabase } => DatabaseProblem.Corrupted,
        SqliteException { SqliteErrorCode: SqliteBusy or SqliteLocked } => DatabaseProblem.Locked,
        SqliteException { SqliteErrorCode: SqliteReadOnly or SqliteCantOpen } => DatabaseProblem.PermissionDenied,
        SqliteException { SqliteErrorCode: SqliteFull } => DatabaseProblem.DiskFull,
        UnauthorizedAccessException => DatabaseProblem.PermissionDenied,
        IOException io when IsDiskFull(io) => DatabaseProblem.DiskFull,
        _ => null,
    };

    /// <summary>Envuelve la excepción si es un problema de acceso conocido; si no, devuelve nulo.</summary>
    public static DatabaseAccessException? Translate(Exception exception) =>
        Classify(exception) is { } problem ? new DatabaseAccessException(problem, exception) : null;

    private static bool IsDiskFull(IOException exception)
    {
        var code = exception.HResult & 0xFFFF;
        return code is WindowsDiskFull or WindowsHandleDiskFull || exception.HResult == UnixNoSpace;
    }
}
