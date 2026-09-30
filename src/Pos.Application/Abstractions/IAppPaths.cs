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

    /// <summary>Logotipo del cliente (opcional): reemplaza al predeterminado.</summary>
    string LogoFile { get; }

    /// <summary>Preferencias locales de la máquina (por ejemplo, el menú).</summary>
    string PreferencesDirectory { get; }

    /// <summary>Tickets de la impresora virtual (un archivo de texto por ticket).</summary>
    string TicketsDirectory { get; }
}
