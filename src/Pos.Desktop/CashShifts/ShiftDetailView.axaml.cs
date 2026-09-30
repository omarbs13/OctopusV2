using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Pos.Desktop.CashShifts;

/// <summary>Los renglones destacados del arqueo van en negritas.</summary>
public sealed class EmphasisWeightConverter : IValueConverter
{
    public static EmphasisWeightConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeight.Bold : FontWeight.Normal;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public partial class ShiftDetailView : UserControl
{
    public ShiftDetailView() => InitializeComponent();
}
