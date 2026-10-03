using Pos.Application.Licensing;

namespace Pos.Desktop.Composition;

/// <summary>
/// Reevalúa la licencia con la aplicación abierta (025, FR-034, research §9): en la próxima medianoche local + 5 s,
/// para activar o vencer módulos al cambiar de día sin reiniciar, y además cada hora, para avanzar la "última fecha
/// vista" (un reloj atrasado no reactiva nada). Una falla se ignora.
/// </summary>
internal sealed class LicenseClockScheduler : IDisposable
{
    private static readonly TimeSpan AfterMidnight = TimeSpan.FromSeconds(5);

    private readonly LicenseBootstrapper _bootstrapper;
    private readonly Timer _hourly;
    private readonly Timer _midnight;

    public LicenseClockScheduler(LicenseBootstrapper bootstrapper)
    {
        _bootstrapper = bootstrapper;
        _hourly = new Timer(_ => Touch(), null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        _midnight = new Timer(_ => OnMidnight(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        ScheduleMidnight();
    }

    public void Dispose()
    {
        _hourly.Dispose();
        _midnight.Dispose();
    }

    /// <summary>Tiempo hasta la próxima medianoche local más el margen.</summary>
    internal static TimeSpan UntilNextMidnight(DateTime nowLocal) =>
        nowLocal.Date.AddDays(1) + AfterMidnight - nowLocal;

    private void OnMidnight()
    {
        Touch();
        ScheduleMidnight();
    }

    private void ScheduleMidnight()
    {
        try
        {
            _midnight.Change(UntilNextMidnight(DateTime.Now), Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // La aplicación se está cerrando.
        }
    }

    private void Touch()
    {
        try
        {
            _bootstrapper.TouchAsync().GetAwaiter().GetResult();
        }
#pragma warning disable CA1031 // Mejor esfuerzo: nunca afecta la operación.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
