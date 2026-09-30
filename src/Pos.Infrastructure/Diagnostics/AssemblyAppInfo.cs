using System.Reflection;
using System.Runtime.InteropServices;
using Pos.Application.Abstractions;

namespace Pos.Infrastructure.Diagnostics;

public sealed class AssemblyAppInfo : IAppInfo
{
    public AssemblyAppInfo()
        : this(Assembly.GetEntryAssembly() ?? typeof(AssemblyAppInfo).Assembly)
    {
    }

    public AssemblyAppInfo(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Se quita el sufijo "+<commit>" que agrega el SDK.
        Version = informational?.Split('+')[0] ?? assembly.GetName().Version?.ToString() ?? "0.0.0";
        OperatingSystem = RuntimeInformation.OSDescription;
    }

    public string Version { get; }

    public string OperatingSystem { get; }
}
