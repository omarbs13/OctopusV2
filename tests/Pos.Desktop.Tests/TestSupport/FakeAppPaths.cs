using Pos.Application.Abstractions;

namespace Pos.Desktop.Tests.TestSupport;

public sealed class FakeAppPaths : IAppPaths
{
    public string DataDirectory => "/datos/Pos";

    public string DatabaseFile => "/datos/Pos/data/pos.db";

    public string AutoBackupsDirectory => "/datos/Pos/backups/auto";

    public string PreMigrationBackupsDirectory => "/datos/Pos/backups/pre-migration";

    public string CorruptDirectory => "/datos/Pos/backups/corrupt";

    public string LogsDirectory => "/datos/Pos/logs";

    public string LockFile => "/datos/Pos/app.lock";
}
