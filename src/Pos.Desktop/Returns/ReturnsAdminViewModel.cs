using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CreditNotes;
using Pos.Application.CreditNotes.GetCreditNoteDetail;
using Pos.Application.CreditNotes.SearchCreditNotes;
using Pos.Application.Printing.PrintTicket;
using Pos.Application.Returns;
using Pos.Application.Returns.GetReturnsSettings;
using Pos.Application.Returns.MarkReversalDone;
using Pos.Application.Returns.SaveReturnsSettings;
using Pos.Application.Returns.SearchPendingReversals;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Desktop.Settings;
using Pos.Domain.CreditNotes;
using Pos.Domain.Returns;

namespace Pos.Desktop.Returns;

/// <summary>Opción del filtro de reintegros.</summary>
public sealed record ReversalFilterOption(ReversalFilter Filter, string Label);

/// <summary>Nota de crédito en la lista del Administrador.</summary>
public sealed record CreditNoteRow(CreditNoteListItemDto Item)
{
    public Guid Id => Item.Id;

    public string Folio => Item.Folio;

    public string DateText => Item.IssuedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string InitialText => MoneyConverter.Format(Item.InitialCents);

    public string BalanceText => MoneyConverter.Format(Item.BalanceCents);

    public string SaleFolio => Item.SaleFolio;
}

/// <summary>Movimiento del saldo de una nota.</summary>
public sealed record CreditNoteMovementRow(CreditNoteMovementDto Item)
{
    public string TypeText => Item.Type switch
    {
        CreditNoteMovementType.Issue => Strings.ReturnsAdmin_MovementIssue,
        CreditNoteMovementType.Redeem => Strings.ReturnsAdmin_MovementRedeem,
        _ => Strings.ReturnsAdmin_MovementRestore,
    };

    public string DateText => Item.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string AmountText => (Item.Type == CreditNoteMovementType.Redeem ? "-" : "+") + MoneyConverter.Format(Item.AmountCents);

    public string RelatedText => string.Join(" · ", new[] { Item.SaleFolio, Item.ReturnFolio }.Where(t => t is not null));

    public string UserName => Item.CreatedByName;
}

/// <summary>Reintegro de tarjeta o transferencia pendiente (o ya reversado).</summary>
public sealed record ReversalRow(PendingReversalDto Item)
{
    public Guid Id => Item.RefundId;

    public string ReturnFolio => Item.ReturnFolio;

    public string SaleFolio => Item.SaleFolio;

    public string MethodText => PaymentMethodLabels.Of(Item.Method);

    public string AmountText => MoneyConverter.Format(Item.AmountCents);

    public string DateText => Item.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public bool IsPending => Item.Status == RefundStatus.PendingReversal;

    public string StatusText => IsPending ? Strings.ReturnsAdmin_StatusPending : Strings.ReturnsAdmin_StatusReversed;
}

/// <summary>
/// Página "Devoluciones y vales" del Administrador (contracts/ui.md §4): notas de crédito con su
/// detalle y reimpresión, reintegros pendientes de reversa manual y el plazo de devoluciones. Solo
/// presenta: las reglas y los permisos viven en los casos de uso.
/// </summary>
public sealed partial class ReturnsAdminViewModel : PageViewModel
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly TicketPrintingService _printing;

    private bool _active;

    public ReturnsAdminViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs, TicketPrintingService printing)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _printing = printing;
        FilterOptions =
        [
            new(ReversalFilter.Pending, Strings.ReturnsAdmin_FilterPending),
            new(ReversalFilter.Reversed, Strings.ReturnsAdmin_FilterReversed),
            new(ReversalFilter.All, Strings.ReturnsAdmin_FilterAll),
        ];
        SelectedFilter = FilterOptions[0];
    }

    public override string Title => Strings.Nav_Returns;

    public IReadOnlyList<ReversalFilterOption> FilterOptions { get; }

    // ---- Notas de crédito
    public ObservableCollection<CreditNoteRow> Notes { get; } = [];

    public ObservableCollection<CreditNoteMovementRow> Movements { get; } = [];

    [ObservableProperty]
    public partial string FolioFilter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool OnlyWithBalance { get; set; }

    [ObservableProperty]
    public partial string? FolioError { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReprintCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelectedNote))]
    public partial CreditNoteRow? SelectedNote { get; set; }

    [ObservableProperty]
    public partial bool NotesEmpty { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotesPageSummary))]
    [NotifyCanExecuteChangedFor(nameof(PreviousNotesPageCommand))]
    public partial int NotesPage { get; private set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotesPageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextNotesPageCommand))]
    public partial int NotesPages { get; private set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotesPageSummary))]
    public partial long NotesCount { get; private set; }

    public bool HasSelectedNote => SelectedNote is not null;

    public string NotesPageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.Products_PageSummary, NotesCount, NotesPage, NotesPages);

    // ---- Reintegros pendientes
    public ObservableCollection<ReversalRow> Reversals { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MarkReversedCommand))]
    public partial ReversalRow? SelectedReversal { get; set; }

    [ObservableProperty]
    public partial ReversalFilterOption SelectedFilter { get; set; }

    [ObservableProperty]
    public partial bool ReversalsEmpty { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReversalsPageSummary))]
    [NotifyCanExecuteChangedFor(nameof(PreviousReversalsPageCommand))]
    public partial int ReversalsPage { get; private set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReversalsPageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextReversalsPageCommand))]
    public partial int ReversalsPages { get; private set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReversalsPageSummary))]
    public partial long ReversalsCount { get; private set; }

    public string ReversalsPageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.Products_PageSummary, ReversalsCount, ReversalsPage, ReversalsPages);

    // ---- Configuración
    [ObservableProperty]
    public partial string WindowDaysText { get; set; } = ReturnsSettings.DefaultReturnWindowDays.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial string? SettingsError { get; private set; }

    [ObservableProperty]
    public partial string? SettingsMessage { get; private set; }

    public override async Task OnActivatedAsync()
    {
        _active = false;
        await LoadSettingsAsync();
        await SearchNotesAsync(1);
        await SearchReversalsAsync(1);
        _active = true;
    }

    partial void OnSelectedFilterChanged(ReversalFilterOption value)
    {
        if (_active)
        {
            _ = SearchReversalsAsync(1);
        }
    }

    partial void OnSelectedNoteChanged(CreditNoteRow? value) => _ = LoadMovementsAsync(value);

    [RelayCommand]
    private Task SearchNotesFromFilterAsync() => SearchNotesAsync(1);

    [RelayCommand(CanExecute = nameof(HasPreviousNotes))]
    private Task PreviousNotesPageAsync() => SearchNotesAsync(NotesPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextNotes))]
    private Task NextNotesPageAsync() => SearchNotesAsync(NotesPage + 1);

    [RelayCommand(CanExecute = nameof(HasPreviousReversals))]
    private Task PreviousReversalsPageAsync() => SearchReversalsAsync(ReversalsPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextReversals))]
    private Task NextReversalsPageAsync() => SearchReversalsAsync(ReversalsPage + 1);

    private bool HasPreviousNotes() => NotesPage > 1;

    private bool HasNextNotes() => NotesPage < NotesPages;

    private bool HasPreviousReversals() => ReversalsPage > 1;

    private bool HasNextReversals() => ReversalsPage < ReversalsPages;

    /// <summary>Reimprime el ticket de la nota seleccionada (<c>ManageCreditNotes</c>).</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedNote))]
    private async Task ReprintAsync()
    {
        if (SelectedNote is { } note)
        {
            await _printing.PrintAsync(PrintSource.CreditNote(note.Id), isReprint: true, note.Folio, automatic: false);
        }
    }

    /// <summary>Marca como reversado un reintegro pendiente, con confirmación; el cambio queda en la bitácora (FR-018).</summary>
    [RelayCommand(CanExecute = nameof(CanMarkReversed))]
    private async Task MarkReversedAsync()
    {
        if (SelectedReversal is not { IsPending: true } row)
        {
            return;
        }

        if (!await _dialogs.ConfirmAsync(
                Strings.ReturnsAdmin_MarkTitle,
                Strings.ReturnsAdmin_MarkQuestion,
                Strings.ReturnsAdmin_MarkConfirm))
        {
            return;
        }

        var (completed, result) = await _runner.RunAsync(
            "MarcarReversaRealizada",
            () => _useCases.RunAsync<MarkReversalDoneHandler, Result>(h => h.HandleAsync(row.Id, CancellationToken.None)),
            new Dictionary<string, object?> { ["RefundId"] = row.Id });
        if (!completed || result is null)
        {
            return;
        }

        switch (result.Error)
        {
            case null:
                await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.ReturnsAdmin_Marked);
                break;
            case InvalidState invalid:
                await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, invalid.Message);
                break;
            default:
                await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Common_UnexpectedError);
                break;
        }

        await SearchReversalsAsync(ReversalsPage);
    }

    private bool CanMarkReversed() => SelectedReversal is { IsPending: true };

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        SettingsError = null;
        SettingsMessage = null;
        if (!int.TryParse(WindowDaysText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var days))
        {
            SettingsError = ReturnMessages.WindowRange;
            return;
        }

        var (completed, result) = await _runner.RunAsync(
            "GuardarPlazoDevoluciones",
            () => _useCases.RunAsync<SaveReturnsSettingsHandler, Result<ReturnsSettings>>(
                h => h.HandleAsync(new SaveReturnsSettingsCommand(days), CancellationToken.None)),
            new Dictionary<string, object?> { ["Days"] = days });
        if (!completed || result is null)
        {
            return;
        }

        if (result.Error is ValidationFailed validation)
        {
            SettingsError = validation.Errors.Count > 0 ? validation.Errors[0].Message : ReturnMessages.WindowRange;
        }
        else if (!result.IsSuccess)
        {
            SettingsError = Strings.Common_Forbidden;
        }
        else
        {
            SettingsMessage = Strings.ReturnsAdmin_Saved;
        }
    }

    private async Task LoadSettingsAsync()
    {
        var (completed, result) = await _runner.RunQuietlyResultAsync(
            "LeerPlazoDevoluciones",
            () => _useCases.RunAsync<GetReturnsSettingsHandler, Result<ReturnsSettings>>(h => h.HandleAsync(CancellationToken.None)));
        if (completed && result is { IsSuccess: true })
        {
            WindowDaysText = result.Value.ReturnWindowDays.ToString(CultureInfo.InvariantCulture);
        }
    }

    private async Task SearchNotesAsync(int page)
    {
        var query = new SearchCreditNotesQuery(FolioFilter, OnlyWithBalance, page);
        var (completed, result) = await _runner.RunAsync(
            "BuscarNotasDeCredito",
            () => _useCases.RunAsync<SearchCreditNotesHandler, Result<CreditNotePage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["Page"] = page });
        if (!completed || result is null)
        {
            return;
        }

        if (result.Error is ValidationFailed validation)
        {
            FolioError = validation.Errors.Count > 0 ? validation.Errors[0].Message : null;
            return;
        }

        if (!result.IsSuccess)
        {
            return;
        }

        FolioError = null;
        Notes.Clear();
        foreach (var item in result.Value.Items)
        {
            Notes.Add(new CreditNoteRow(item));
        }

        NotesCount = result.Value.TotalCount;
        NotesPages = result.Value.TotalPages;
        NotesPage = result.Value.Page;
        NotesEmpty = Notes.Count == 0;
        SelectedNote = null;
        Movements.Clear();
    }

    private async Task LoadMovementsAsync(CreditNoteRow? note)
    {
        Movements.Clear();
        if (note is null)
        {
            return;
        }

        var (completed, result) = await _runner.RunQuietlyResultAsync(
            "DetalleNotaDeCredito",
            () => _useCases.RunAsync<GetCreditNoteDetailHandler, Result<CreditNoteDetailDto>>(h => h.HandleAsync(note.Id, CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true } || SelectedNote?.Id != note.Id)
        {
            return;
        }

        foreach (var movement in result.Value.Movements)
        {
            Movements.Add(new CreditNoteMovementRow(movement));
        }
    }

    private async Task SearchReversalsAsync(int page)
    {
        var query = new SearchPendingReversalsQuery(SelectedFilter.Filter, page);
        var (completed, result) = await _runner.RunAsync(
            "BuscarReintegrosPendientes",
            () => _useCases.RunAsync<SearchPendingReversalsHandler, Result<ReversalPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["Filter"] = query.Filter.ToString(), ["Page"] = page });
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        Reversals.Clear();
        foreach (var item in result.Value.Items)
        {
            Reversals.Add(new ReversalRow(item));
        }

        ReversalsCount = result.Value.TotalCount;
        ReversalsPages = result.Value.TotalPages;
        ReversalsPage = result.Value.Page;
        ReversalsEmpty = Reversals.Count == 0;
        SelectedReversal = null;
    }
}
