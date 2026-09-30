namespace Pos.Application.Abstractions;

public interface IAppInfo
{
    string Version { get; }

    string OperatingSystem { get; }
}
