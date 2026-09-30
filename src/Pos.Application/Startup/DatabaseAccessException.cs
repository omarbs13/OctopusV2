namespace Pos.Application.Startup;

public enum DatabaseProblem
{
    Locked,
    PermissionDenied,
    DiskFull,
    Corrupted,
}

/// <summary>
/// Problema de acceso a la base o a sus respaldos, traducido por Infrastructure para que
/// Application no dependa del proveedor de base de datos.
/// </summary>
public sealed class DatabaseAccessException : Exception
{
    public DatabaseAccessException(DatabaseProblem problem, Exception? innerException = null)
        : base($"Problema de acceso a la base de datos: {problem}.", innerException)
    {
        Problem = problem;
    }

    public DatabaseAccessException()
    {
    }

    public DatabaseAccessException(string message)
        : base(message)
    {
    }

    public DatabaseAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DatabaseProblem Problem { get; }
}
