using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Home;

public enum DashboardCardKind
{
    Metric,
    Chart,
}

public enum DashboardCardState
{
    Loading,
    Ready,
    Empty,
    Error,
}

/// <summary>
/// Tarjeta de la pantalla de inicio. Cada módulo registra las suyas con
/// <c>AddDashboardCard&lt;T&gt;()</c>, sin modificar Inicio (contracts/dashboard.md).
/// </summary>
public abstract partial class DashboardCard : ViewModelBase
{
    private readonly OperationRunner _runner;

    protected DashboardCard(OperationRunner runner) => _runner = runner;

    public abstract string Title { get; }

    public abstract string Icon { get; }

    public abstract int Order { get; }

    public abstract DashboardCardKind Kind { get; }

    /// <summary>Barras de la gráfica; vacío en las tarjetas que no son gráficas (<see cref="ChartCard"/>).</summary>
    public virtual IReadOnlyList<ChartBar> Bars => [];

    /// <summary>Las barras se dibujan en horizontal (ranking) en lugar de en vertical (serie por día).</summary>
    public virtual bool IsHorizontal => false;

    /// <summary>Permiso que exige la tarjeta para mostrarse; nulo si la ven todos (007, FR-008).</summary>
    public virtual Pos.Domain.Users.Permission? RequiredPermission => null;

    /// <summary>Id de la opción de menú a la que lleva la tarjeta, si tiene.</summary>
    public virtual string? NavigateTo => null;

    /// <summary>Argumento que se entrega a la pantalla destino al navegar (por ejemplo, un filtro).</summary>
    public virtual object? NavigationArgument => null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNavigable), nameof(IsReady), nameof(IsEmpty), nameof(IsError), nameof(IsLoading), nameof(ShowValue), nameof(ShowSegments))]
    public partial DashboardCardState State { get; private set; } = DashboardCardState.Loading;

    /// <summary>
    /// Cifras navegables (022). Si la tarjeta tiene, se dibujan en lugar de <see cref="Value"/> y la
    /// tarjeta exterior no navega. Vacío por omisión: las demás tarjetas no cambian.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowValue), nameof(ShowSegments))]
    public partial IReadOnlyList<DashboardCardSegment> Segments { get; protected set; } = [];

    /// <summary>Valor ya formateado; solo en <see cref="DashboardCardState.Ready"/>. Puede faltar en una gráfica.</summary>
    [ObservableProperty]
    public partial string? Value { get; private set; }

    /// <summary>Mensaje del estado vacío o de error.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    public bool IsNavigable => State == DashboardCardState.Ready && NavigateTo is not null;

    public bool IsReady => State == DashboardCardState.Ready;

    public bool IsEmpty => State == DashboardCardState.Empty;

    public bool IsError => State == DashboardCardState.Error;

    public bool IsLoading => State == DashboardCardState.Loading;

    public bool ShowValue => IsReady && Segments.Count == 0;

    public bool ShowSegments => IsReady && Segments.Count > 0;

    /// <summary>Carga la tarjeta. Nunca lanza: un error la deja en estado de error y se registra.</summary>
    public async Task LoadAsync()
    {
        State = DashboardCardState.Loading;
        Value = null;
        Message = null;
        var ok = await _runner.RunQuietlyAsync(
            "CargarTarjeta",
            LoadCoreAsync,
            new Dictionary<string, object?> { ["Card"] = Title });

        if (!ok)
        {
            Value = null;
            Message = Strings.Card_Unavailable;
            State = DashboardCardState.Error;
        }
    }

    /// <summary>Obtiene los datos y llama a <see cref="SetReady"/> o <see cref="SetEmpty"/>.</summary>
    protected abstract Task LoadCoreAsync();

    protected void SetReady(string? value, string? message = null)
    {
        Value = value;
        Message = message;
        State = DashboardCardState.Ready;
    }

    protected void SetEmpty(string message)
    {
        Value = null;
        Message = message;
        State = DashboardCardState.Empty;
    }
}
