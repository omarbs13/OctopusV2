using Pos.Application.Abstractions;
using Pos.Application.Diagnostics.GetAppInfo;

namespace Pos.Application.Tests.Diagnostics;

public class GetAppInfoHandlerTests
{
    [Fact]
    public void Handle_CombinaVersionSistemaOperativoYCarpetaDeDatos()
    {
        var handler = new GetAppInfoHandler(new FixedAppInfo(), new FixedPaths());

        var info = handler.Handle();

        Assert.Equal(new AppInfoDto("1.2.3", "/datos/Pos", "Linux 7.2"), info);
    }

    private sealed class FixedAppInfo : IAppInfo
    {
        public string Version => "1.2.3";

        public string OperatingSystem => "Linux 7.2";
    }

    private sealed class FixedPaths : IAppPaths
    {
        public string DataDirectory => "/datos/Pos";

        public string DatabaseFile => "/datos/Pos/data/pos.db";

        public string AutoBackupsDirectory => string.Empty;

        public string PreMigrationBackupsDirectory => string.Empty;

        public string CorruptDirectory => string.Empty;

        public string LogsDirectory => string.Empty;

        public string LockFile => string.Empty;

        public string LogoFile => string.Empty;

        public string PreferencesDirectory => string.Empty;
    }
}
