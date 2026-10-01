using Pos.Application.Abstractions;

namespace Pos.Infrastructure.Platform;

/// <summary>
/// Estructura de la carpeta de datos. La raíz es LocalApplicationData/Pos
/// (%LOCALAPPDATA%\Pos en Windows, ~/.local/share/Pos en Linux) o la variable POS_DATA_DIR.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public const string DataDirectoryVariable = "POS_DATA_DIR";

    public AppPaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = Path.GetFullPath(dataDirectory);
        DatabaseFile = Path.Combine(DataDirectory, "data", "pos.db");
        AutoBackupsDirectory = Path.Combine(DataDirectory, "backups", "auto");
        PreMigrationBackupsDirectory = Path.Combine(DataDirectory, "backups", "pre-migration");
        CorruptDirectory = Path.Combine(DataDirectory, "backups", "corrupt");
        LogsDirectory = Path.Combine(DataDirectory, "logs");
        LockFile = Path.Combine(DataDirectory, "app.lock");
        LogoFile = Path.Combine(DataDirectory, "logo.png");
        PreferencesDirectory = Path.Combine(DataDirectory, "preferences");
        TicketsDirectory = Path.Combine(DataDirectory, "tickets");
        LicenseFile = Path.Combine(DataDirectory, "license.lic");
    }

    public string DataDirectory { get; }

    public string DatabaseFile { get; }

    public string AutoBackupsDirectory { get; }

    public string PreMigrationBackupsDirectory { get; }

    public string CorruptDirectory { get; }

    public string LogsDirectory { get; }

    public string LockFile { get; }

    public string LogoFile { get; }

    public string PreferencesDirectory { get; }

    public string TicketsDirectory { get; }

    public string LicenseFile { get; }

    public static AppPaths FromEnvironment()
    {
        var overridden = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        var root = string.IsNullOrWhiteSpace(overridden)
            ? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolderOption.Create),
                "Pos")
            : overridden;
        return new AppPaths(root);
    }

    /// <summary>Crea las carpetas de la estructura si no existen.</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabaseFile)!);
        Directory.CreateDirectory(AutoBackupsDirectory);
        Directory.CreateDirectory(PreMigrationBackupsDirectory);
        Directory.CreateDirectory(CorruptDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(PreferencesDirectory);
    }
}
