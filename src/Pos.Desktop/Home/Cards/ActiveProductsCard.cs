using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Application.Products.CountActiveProducts;
using Pos.Desktop.Common;
using Pos.Desktop.Products;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Home.Cards;

/// <summary>Productos activos: dato real y acceso directo a Productos (FR-006, FR-009).</summary>
public sealed class ActiveProductsCard(OperationRunner runner, UseCases useCases) : DashboardCard(runner)
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("es-MX");

    public override string Title => Strings.Card_ActiveProducts;

    public override string Icon => "Icon.Product";

    public override int Order => 10;

    public override Permission? RequiredPermission => Permission.ViewProducts;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;

    public override string? NavigateTo => ProductsModule.PageId;

    protected override async Task LoadCoreAsync()
    {
        var result = await useCases.RunAsync<CountActiveProductsHandler, Result<long>>(
            h => h.HandleAsync(CancellationToken.None));
        SetReady(result.Value.ToString("N0", DisplayCulture));
    }
}
