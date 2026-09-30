namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>Simula carpetas sin permisos de escritura. Solo tiene efecto fuera de Windows.</summary>
public static class UnixPermissions
{
    public static void MakeReadOnly(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
    }

    public static void MakeWritable(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
