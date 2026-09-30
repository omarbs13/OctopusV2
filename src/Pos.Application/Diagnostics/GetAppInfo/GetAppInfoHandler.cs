using Pos.Application.Abstractions;

namespace Pos.Application.Diagnostics.GetAppInfo;

public sealed record AppInfoDto(string Version, string DataDirectory, string OperatingSystem);

/// <summary>Versión, carpeta de datos y sistema operativo para la pantalla "Acerca de" (FR-027).</summary>
public sealed class GetAppInfoHandler
{
    private readonly IAppInfo _appInfo;
    private readonly IAppPaths _paths;

    public GetAppInfoHandler(IAppInfo appInfo, IAppPaths paths)
    {
        _appInfo = appInfo;
        _paths = paths;
    }

    public AppInfoDto Handle() => new(_appInfo.Version, _paths.DataDirectory, _appInfo.OperatingSystem);
}
