namespace Pos.Application.Abstractions;

/// <summary>Rutas de la carpeta de datos de la aplicación.</summary>
public interface IAppPaths
{
    string DataDirectory { get; }

    string DatabaseFile { get; }

    string AutoBackupsDirectory { get; }

    string PreMigrationBackupsDirectory { get; }

    string CorruptDirectory { get; }

    string LogsDirectory { get; }

    string LockFile { get; }
}
