using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Application.Startup;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Splash;

/// <summary>Pantalla de carga: logotipo, nombre, versión y paso en curso del arranque (FR-001, FR-002).</summary>
public sealed partial class SplashViewModel : ViewModelBase, IProgress<StartupStep>
{
    /// <summary>Tiempo mínimo visible, contado desde que aparece la pantalla de carga (FR-007).</summary>
    public static readonly TimeSpan MinimumVisible = TimeSpan.FromSeconds(3);

    private readonly Func<TimeSpan> _elapsed;
    private readonly Func<TimeSpan, Task> _delay;

    public SplashViewModel(IAppInfo appInfo, IBrandingAssets branding)
        : this(appInfo, branding, StartWatch(), delay => Task.Delay(delay))
    {
    }

    internal SplashViewModel(IAppInfo appInfo, IBrandingAssets branding, Func<TimeSpan> elapsed, Func<TimeSpan, Task> delay)
    {
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(branding);
        _elapsed = elapsed;
        _delay = delay;
        Version = appInfo.Version;
        Logo = branding.Logo;
    }

    public string AppName { get; } = Strings.AppTitle;

    public string Version { get; }

    public Avalonia.Media.IImage? Logo { get; }

    [ObservableProperty]
    public partial string StepText { get; private set; } = Strings.Splash_Starting;

    void IProgress<StartupStep>.Report(StartupStep value) => StepText = value switch
    {
        StartupStep.CheckingDatabase => Strings.Splash_CheckingDatabase,
        StartupStep.BackingUp => Strings.Splash_BackingUp,
        StartupStep.Migrating => Strings.Splash_Migrating,
        StartupStep.Restoring => Strings.Splash_Restoring,
        _ => Strings.Splash_Finishing,
    };

    /// <summary>Completa el tiempo mínimo visible sin retrasar un arranque que ya tardó más.</summary>
    public Task WaitMinimumAsync()
    {
        var remaining = MinimumVisible - _elapsed();
        return remaining > TimeSpan.Zero ? _delay(remaining) : Task.CompletedTask;
    }

    private static Func<TimeSpan> StartWatch()
    {
        var watch = Stopwatch.StartNew();
        return () => watch.Elapsed;
    }
}
