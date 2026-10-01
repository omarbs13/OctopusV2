using Pos.Infrastructure.Diagnostics;

namespace Pos.Desktop.Composition;

/// <summary>
/// Aplica la retención de 30 días del log al iniciar y cada vez que cambia el día (la aplicación puede
/// quedar abierta varios días). Una falla de la limpieza se ignora: nunca afecta la operación.
/// </summary>
internal sealed class LogRetentionScheduler : IDisposable
{
    private readonly string _logsDirectory;
    private readonly Timer _timer;
    private DateOnly _lastCleaned;

    public LogRetentionScheduler(string logsDirectory)
    {
        _logsDirectory = logsDirectory;
        _timer = new Timer(_ => RunIfDayChanged(), null, TimeSpan.Zero, TimeSpan.FromHours(1));
    }

    public void Dispose() => _timer.Dispose();

    private void RunIfDayChanged()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (today == _lastCleaned)
            {
                return;
            }

            LogRetention.Clean(_logsDirectory, today);
            _lastCleaned = today;
        }
#pragma warning disable CA1031 // La limpieza es de mejor esfuerzo.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
