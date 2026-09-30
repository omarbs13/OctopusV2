using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Shell;

public sealed partial class MainViewModel : ViewModelBase
{
    public MainViewModel(IEnumerable<PageViewModel> pages, IAppInfo appInfo)
    {
        ArgumentNullException.ThrowIfNull(appInfo);
        Pages = [.. pages];
        WindowTitle = $"{Strings.AppTitle} {appInfo.Version}";
        SelectedPage = Pages.Count > 0 ? Pages[0] : null;
    }

    public IReadOnlyList<PageViewModel> Pages { get; }

    public string WindowTitle { get; }

    [ObservableProperty]
    public partial PageViewModel? SelectedPage { get; set; }

    partial void OnSelectedPageChanged(PageViewModel? value)
    {
        if (value is not null)
        {
            _ = value.OnActivatedAsync();
        }
    }
}
