using Avalonia.Controls;
using Pos.Desktop.Common;

namespace Pos.Desktop.Tests.Navigation;

/// <summary>Pantalla de prueba que cuenta activaciones y puede negarse a salir.</summary>
public class TestPageViewModel : PageViewModel
{
    public override string Title => "Prueba";

    public int Activations { get; private set; }

    public bool AllowLeave { get; set; } = true;

    public int LeaveQuestions { get; private set; }

    public override Task OnActivatedAsync()
    {
        Activations++;
        return Task.CompletedTask;
    }

    public override Task<bool> CanLeaveAsync()
    {
        LeaveQuestions++;
        return Task.FromResult(AllowLeave);
    }
}

public sealed class OtherTestPageViewModel : TestPageViewModel;

public sealed class TestPageView : Border;
