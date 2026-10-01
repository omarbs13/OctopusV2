using Pos.Application.Licensing;

namespace Pos.Desktop.Composition;

/// <summary>
/// Avanza la "última fecha vista" de la licencia cada hora (011, research §4): un reloj atrasado no
/// devuelve días aunque la aplicación quede abierta varios días. Una falla se ignora.
/// </summary>
internal sealed class LicenseClockScheduler : IDisposable
{
    private readonly Timer _timer;

    public LicenseClockScheduler(LicenseBootstrapper bootstrapper) =>
        _timer = new Timer(_ => Touch(bootstrapper), null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

    public void Dispose() => _timer.Dispose();

    private static void Touch(LicenseBootstrapper bootstrapper)
    {
        try
        {
            bootstrapper.TouchAsync().GetAwaiter().GetResult();
        }
#pragma warning disable CA1031 // Mejor esfuerzo: nunca afecta la operación.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
