using Avalonia.Controls;

namespace Pos.Desktop.Reports;

public partial class ChartImageView : UserControl
{
    public ChartImageView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    private async void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is ChartViewModel chart)
        {
            await chart.ResizeAsync((int)e.NewSize.Width, (int)e.NewSize.Height);
        }
    }
}
