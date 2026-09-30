using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Users;
using Pos.Application.Users.SearchUsers;
using Pos.Application.Users.Session;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Desktop.Shell;
using Pos.Domain.Users;

namespace Pos.Desktop.Administration;

/// <summary>Fila del listado de usuarios con los textos ya formateados.</summary>
public sealed record UserRow(UserListItem Item)
{
    public Guid Id => Item.Id;

    public string FullName => Item.FullName;

    public string UserName => Item.UserName;

    public string RoleText => Item.Role == UserRole.Admin ? Strings.Role_Admin : Strings.Role_Cashier;

    public bool IsActive => Item.IsActive;

    public string StatusText => Item.IsActive ? Strings.Users_Active : Strings.Users_Inactive;
}

/// <summary>Administración de usuarios (FR-015): listado con búsqueda, filtro de inactivos, paginación y formulario.</summary>
public sealed partial class UsersViewModel : PageViewModel, IDisposable
{
    /// <summary>Espera tras la última tecla antes de buscar.</summary>
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IUserSession _session;
    private readonly ModalHost _modal;
    private readonly Func<UserEditorViewModel> _editorFactory;

    private CancellationTokenSource? _pendingSearch;
    private int _searchVersion;

    public UsersViewModel(
        UseCases useCases,
        OperationRunner runner,
        IUserSession session,
        ModalHost modal,
        Func<UserEditorViewModel> editorFactory)
    {
        _useCases = useCases;
        _runner = runner;
        _session = session;
        _modal = modal;
        _editorFactory = editorFactory;
    }

    public override string Title => Strings.Users_Title;

    public override FormHost Forms { get; } = new();

    public ObservableCollection<UserRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeInactive { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
    public partial UserRow? SelectedRow { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    public partial long TotalCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int TotalPages { get; private set; } = 1;

    public string PageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.Users_PageSummary, TotalCount, CurrentPage, TotalPages);

    public override Task OnActivatedAsync() => SearchAsync();

    public void Dispose()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
    }

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        _ = SearchAfterDelayAsync();
    }

    partial void OnIncludeInactiveChanged(bool value)
    {
        CurrentPage = 1;
        _ = SearchNowAsync();
    }

    [RelayCommand]
    private Task SearchNowAsync()
    {
        CancelPendingSearch();
        return SearchAsync();
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task FirstPageAsync() => GoToPageAsync(1);

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync() => GoToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync() => GoToPageAsync(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task LastPageAsync() => GoToPageAsync(TotalPages);

    [RelayCommand]
    private Task NewUserAsync() => OpenEditorAsync(_editorFactory());

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedRow is not { } selected)
        {
            return;
        }

        var editor = _editorFactory();
        if (await editor.LoadAsync(selected.Id))
        {
            await OpenEditorAsync(editor);
        }
        else
        {
            await SearchAsync();
        }
    }

    /// <summary>Restablece la contraseña de otro usuario: queda como temporal (FR-016). No aplica a uno mismo.</summary>
    [RelayCommand(CanExecute = nameof(CanResetPassword))]
    private void ResetPassword()
    {
        if (SelectedRow is not { } selected)
        {
            return;
        }

        _modal.Show(new ResetPasswordViewModel(_useCases, _runner, selected.Id, selected.UserName, _modal.Close));
    }

    private bool HasSelection() => SelectedRow is not null;

    private bool CanResetPassword() => SelectedRow is { } row && row.Id != _session.User?.Id;

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    private Task GoToPageAsync(int page)
    {
        CancelPendingSearch();
        CurrentPage = page;
        return SearchAsync();
    }

    private async Task OpenEditorAsync(UserEditorViewModel editor)
    {
        editor.Saved += (_, userId) => _ = OnEditorSavedAsync(userId);
        editor.Closed += (_, _) => _ = SearchAsync();
        await Forms.OpenAsync(editor, FormPresentation.SidePanel);
    }

    private async Task OnEditorSavedAsync(Guid userId)
    {
        await SearchAsync();
        SelectedRow = Rows.FirstOrDefault(r => r.Id == userId);
    }

    private async Task SearchAfterDelayAsync()
    {
        CancelPendingSearch();
        var pending = _pendingSearch = new CancellationTokenSource();
        try
        {
            await Task.Delay(SearchDelay, pending.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SearchAsync();
    }

    private void CancelPendingSearch()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
        _pendingSearch = null;
    }

    private async Task SearchAsync()
    {
        var version = Interlocked.Increment(ref _searchVersion);
        var query = new SearchUsersQuery(SearchText, IncludeInactive, CurrentPage);

        var (completed, result) = await _runner.RunAsync(
            "BuscarUsuarios",
            () => _useCases.RunAsync<SearchUsersHandler, Result<UserPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["IncludeInactive"] = query.IncludeInactive, ["Page"] = query.Page });

        if (!completed || result is not { IsSuccess: true } || version != _searchVersion)
        {
            return;
        }

        var selectedId = SelectedRow?.Id;
        Rows.Clear();
        foreach (var item in result.Value.Items)
        {
            Rows.Add(new UserRow(item));
        }

        var page = result.Value;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        IsEmpty = Rows.Count == 0;
        SelectedRow = Rows.FirstOrDefault(r => r.Id == selectedId);
    }
}
