using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Reports;

namespace Pos.Desktop.Reports;

/// <summary>Opción de período predefinido del selector.</summary>
public sealed record PeriodPresetOption(ReportPreset Preset, string Label);

/// <summary>
/// Selector de período compartido por los reportes (contracts/ui.md): presets y rango personalizado.
/// Tras elegir un preset avisa de inmediato; el rango personalizado espera a "Consultar" y solo se
/// habilita si es válido, mostrando el motivo si no.
/// </summary>
public sealed partial class PeriodPickerViewModel : ViewModelBase
{
    private readonly Func<DateOnly> _today;
    private bool _suppress;

    public PeriodPickerViewModel(ReportPreset initial = ReportPreset.Today, Func<DateOnly>? today = null)
    {
        _today = today ?? (() => DateOnly.FromDateTime(DateTime.Today));
        Presets =
        [
            new(ReportPreset.Today, Strings.Reports_PresetToday),
            new(ReportPreset.Yesterday, Strings.Reports_PresetYesterday),
            new(ReportPreset.Last7Days, Strings.Reports_PresetLast7Days),
            new(ReportPreset.ThisMonth, Strings.Reports_PresetThisMonth),
            new(ReportPreset.PreviousMonth, Strings.Reports_PresetPreviousMonth),
            new(ReportPreset.Custom, Strings.Reports_PresetCustom),
        ];

        _suppress = true;
        SelectedPreset = Presets.First(p => p.Preset == initial);
        _suppress = false;
        Period = initial == ReportPreset.Custom
            ? ReportPeriod.Today(_today())
            : ReportPeriod.FromPreset(initial, _today());
        CustomFrom = Period.FromDate.ToDateTime(TimeOnly.MinValue);
        CustomTo = Period.ToDate.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>Se dispara al elegir un preset o consultar un rango personalizado válido.</summary>
    public event EventHandler? PeriodChanged;

    public IReadOnlyList<PeriodPresetOption> Presets { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    [NotifyPropertyChangedFor(nameof(ShowValidation))]
    public partial PeriodPresetOption SelectedPreset { get; set; }

    [ObservableProperty]
    public partial ReportPeriod Period { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    [NotifyPropertyChangedFor(nameof(CanQuery))]
    [NotifyPropertyChangedFor(nameof(ShowValidation))]
    public partial DateTime? CustomFrom { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    [NotifyPropertyChangedFor(nameof(CanQuery))]
    [NotifyPropertyChangedFor(nameof(ShowValidation))]
    public partial DateTime? CustomTo { get; set; }

    public bool IsCustom => SelectedPreset.Preset == ReportPreset.Custom;

    /// <summary>Motivo por el que el rango personalizado no es válido, o nulo si lo es.</summary>
    public string? ValidationMessage
    {
        get
        {
            if (CustomFrom is not { } from || CustomTo is not { } to)
            {
                return Strings.Reports_PeriodRequired;
            }

            return ReportPeriod.Validate(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to)) switch
            {
                ReportPeriodError.EndBeforeStart => Strings.Reports_PeriodEndBeforeStart,
                ReportPeriodError.TooLong => Strings.Reports_PeriodTooLong,
                _ => null,
            };
        }
    }

    public bool CanQuery => ValidationMessage is null;

    public bool ShowValidation => IsCustom && !CanQuery;

    /// <summary>Etiqueta del período para títulos y filtros: "01/09/2026 - 30/09/2026".</summary>
    public string PeriodText => Period.FromDate == Period.ToDate
        ? Period.FromDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)
        : $"{Period.FromDate:dd/MM/yyyy} - {Period.ToDate:dd/MM/yyyy}";

    partial void OnSelectedPresetChanged(PeriodPresetOption value)
    {
        if (_suppress || value.Preset == ReportPreset.Custom)
        {
            return;
        }

        Period = ReportPeriod.FromPreset(value.Preset, _today());
        OnPropertyChanged(nameof(PeriodText));
        PeriodChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Elige un preset sin disparar <see cref="PeriodChanged"/>: quien lo llama recarga después (por
    /// ejemplo, al recibir un filtro por navegación, 022).
    /// </summary>
    public void SelectPresetSilently(ReportPreset preset)
    {
        if (preset == ReportPreset.Custom)
        {
            throw new ArgumentOutOfRangeException(nameof(preset), preset, "El rango personalizado no se elige sin fechas.");
        }

        _suppress = true;
        try
        {
            SelectedPreset = Presets.First(p => p.Preset == preset);
        }
        finally
        {
            _suppress = false;
        }

        Period = ReportPeriod.FromPreset(preset, _today());
        OnPropertyChanged(nameof(PeriodText));
    }

    [RelayCommand(CanExecute = nameof(CanQuery))]
    private void QueryCustom()
    {
        if (CustomFrom is not { } from || CustomTo is not { } to)
        {
            return;
        }

        Period = ReportPeriod.Custom(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to));
        OnPropertyChanged(nameof(PeriodText));
        PeriodChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnCustomFromChanged(DateTime? value) => QueryCustomCommand.NotifyCanExecuteChanged();

    partial void OnCustomToChanged(DateTime? value) => QueryCustomCommand.NotifyCanExecuteChanged();
}
