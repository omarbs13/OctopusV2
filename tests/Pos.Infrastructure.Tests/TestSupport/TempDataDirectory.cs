using Microsoft.Data.Sqlite;
using Pos.Infrastructure.Platform;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>Carpeta de datos temporal y única por prueba; se elimina al hacer Dispose.</summary>
public sealed class TempDataDirectory : IDisposable
{
    public TempDataDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "pos-tests", Guid.NewGuid().ToString("N"));
        Paths = new AppPaths(Root);
        Paths.EnsureDirectories();
    }

    public string Root { get; }

    public AppPaths Paths { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Un archivo aún abierto en Windows no debe hacer fallar la prueba.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
