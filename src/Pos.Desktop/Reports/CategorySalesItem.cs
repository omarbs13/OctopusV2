using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Categories;
using Pos.Application.Reports.GetSalesReport;
using Pos.Desktop.Common;

namespace Pos.Desktop.Reports;

/// <summary>Producto de una categoría en "Ventas por categoría", con sus textos ya formateados.</summary>
public sealed record ProductSalesItem(ProductSales Product)
{
    public string Name => Product.Name;

    public string UnitsText => $"{QuantityConverter.Format(Product.UnitsThousandths, Product.DecimalPlaces)} {Product.UnitName}";

    public string AmountText => MoneyConverter.Format(Product.AmountCents);
}

/// <summary>
/// Fila expandible de "Ventas por categoría" (016, FR-016, FR-017). Las cifras vienen del reporte; aquí solo
/// se formatean (Principio III).
/// </summary>
public sealed partial class CategorySalesItem : ObservableObject
{
    /// <summary>Las unidades de una categoría mezclan unidades distintas; se muestran con 3 decimales (research §9).</summary>
    private const int UnitsDecimals = 3;

    public CategorySalesItem(CategorySales category)
    {
        ArgumentNullException.ThrowIfNull(category);
        Name = CategoryMessages.Display(category.CategoryId is null ? null : category.Name, category.IsActive);
        UnitsText = QuantityConverter.Format(category.UnitsThousandths, UnitsDecimals);
        AmountText = MoneyConverter.Format(category.AmountCents);
        ShareText = FormatShare(category.ShareBasisPoints);
        Products = [.. category.Products.Select(p => new ProductSalesItem(p))];
    }

    public string Name { get; }

    public string UnitsText { get; }

    public string AmountText { get; }

    public string ShareText { get; }

    public IReadOnlyList<ProductSalesItem> Products { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    public partial bool IsExpanded { get; set; }

    public string Glyph => IsExpanded ? "▾" : "▸";

    /// <summary>Puntos base con un decimal: 5,000 → "50.0 %" (spec, casos límite: redondeo).</summary>
    internal static string FormatShare(long basisPoints) =>
        string.Create(CultureInfo.InvariantCulture, $"{basisPoints / 100m:0.0} %");

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}
