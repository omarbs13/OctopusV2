using Avalonia;
using Avalonia.Controls;

namespace Pos.Desktop.Reports;

/// <summary>Tarjeta con una etiqueta y un valor ya formateado, para los indicadores de los reportes.</summary>
public partial class MetricCard : UserControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Value));

    public MetricCard() => InitializeComponent();

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            LabelText.Text = Label;
        }
        else if (change.Property == ValueProperty)
        {
            ValueText.Text = Value;
        }
    }
}
