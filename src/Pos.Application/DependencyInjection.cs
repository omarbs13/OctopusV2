using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Diagnostics.ExportDiagnostics;
using Pos.Application.Diagnostics.GetAppInfo;
using Pos.Application.Inventory.GetStockAlerts;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Inventory.SearchMovements;
using Pos.Application.Inventory.SearchStock;
using Pos.Application.Products.CountActiveProducts;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.DeleteProduct;
using Pos.Application.Products.GetProduct;
using Pos.Application.Products.ListUnitsOfMeasure;
using Pos.Application.Products.PrepareProductImage;
using Pos.Application.Products.SearchProducts;
using Pos.Application.Products.UpdateProduct;
using Pos.Application.Sales.CancelSale;
using Pos.Application.Sales.ConfirmSale;
using Pos.Application.Sales.DiscardSaleDraft;
using Pos.Application.Sales.FindProductsForSale;
using Pos.Application.Sales.GetSale;
using Pos.Application.Sales.GetSaleDraft;
using Pos.Application.Sales.GetSalesDashboard;
using Pos.Application.Sales.ReviewSale;
using Pos.Application.Sales.SaveSaleDraft;
using Pos.Application.Sales.SearchSales;
using Pos.Application.Startup;

namespace Pos.Application;

public static class DependencyInjection
{
    /// <summary>Registra los casos de uso y sus validadores. Cada funcionalidad agrega los suyos.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseStartup, DatabaseStartup>();

        // Productos: un ámbito por operación (ver Pos.Desktop.Common.UseCases).
        services.AddSingleton<IValidator<CreateProductCommand>, CreateProductValidator>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<SearchProductsHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddSingleton<IValidator<UpdateProductCommand>, UpdateProductValidator>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<DeleteProductHandler>();
        services.AddScoped<CountActiveProductsHandler>();
        services.AddSingleton<ListUnitsOfMeasureHandler>();
        services.AddScoped<PrepareProductImageHandler>();

        // Inventario
        services.AddSingleton<IValidator<RegisterMovementCommand>, RegisterMovementValidator>();
        services.AddScoped<RegisterMovementHandler>();
        services.AddScoped<SearchStockHandler>();
        services.AddScoped<SearchMovementsHandler>();
        services.AddScoped<GetStockAlertsHandler>();

        // Ventas
        services.AddScoped<FindProductsForSaleHandler>();
        services.AddScoped<SaveSaleDraftHandler>();
        services.AddScoped<GetSaleDraftHandler>();
        services.AddScoped<DiscardSaleDraftHandler>();
        services.AddScoped<ReviewSaleHandler>();
        services.AddSingleton<IValidator<ConfirmSaleCommand>, ConfirmSaleValidator>();
        services.AddScoped<ConfirmSaleHandler>();
        services.AddScoped<SearchSalesHandler>();
        services.AddScoped<GetSaleHandler>();
        services.AddSingleton<IValidator<CancelSaleCommand>, CancelSaleValidator>();
        services.AddScoped<CancelSaleHandler>();
        services.AddScoped<GetSalesDashboardHandler>();

        // Diagnóstico
        services.AddScoped<GetAppInfoHandler>();
        services.AddScoped<ExportDiagnosticsHandler>();

        return services;
    }
}
