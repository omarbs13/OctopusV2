using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Navigation;

/// <summary>Pantalla de una opción cuyo módulo aún no existe (FR-020a).</summary>
public sealed class ComingSoonViewModel : PageViewModel
{
    public ComingSoonViewModel(string title, string icon)
    {
        Title = title;
        Icon = icon;
    }

    public override string Title { get; }

    public string Icon { get; }

    public string Heading { get; } = Strings.ComingSoon_Title;

    public string Message { get; } = Strings.ComingSoon_Message;
}
