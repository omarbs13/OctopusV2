using System.Globalization;

namespace Pos.Infrastructure.Diagnostics;

/// <summary>
/// Elimina los archivos de log (<c>pos-AAAAMMDD.log</c>) con más de 30 días, según la fecha de su nombre
/// (FR-006). Se hace por fecha y no por cantidad de archivos para que equivalga a 30 días aunque la
/// aplicación no se use algunos días.
/// </summary>
public static class LogRetention
{
    public const int RetentionDays = 30;

    /// <summary>Borra los logs anteriores a <paramref name="today"/> menos <paramref name="days"/> días; devuelve cuántos borró.</summary>
    public static int Clean(string logsDirectory, DateOnly today, int days = RetentionDays)
    {
        if (!Directory.Exists(logsDirectory))
        {
            return 0;
        }

        var limit = today.AddDays(-days);
        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(logsDirectory, "pos-*.log"))
        {
            var stem = Path.GetFileNameWithoutExtension(file)["pos-".Length..];
            if (!DateOnly.TryParseExact(stem, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || date >= limit)
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Un log que no se puede borrar (en uso, sin permisos) se reintenta en la próxima limpieza.
            }
        }

        return deleted;
    }
}
