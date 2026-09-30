using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Diagnostics.ExportDiagnostics;
using Pos.Application.Diagnostics.GetAppInfo;
using Pos.Application.Products.CountActiveProducts;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.DeleteProduct;
using Pos.Application.Products.GetProduct;
using Pos.Application.Products.ListUnitsOfMeasure;
using Pos.Application.Products.PrepareProductImage;
using Pos.Application.Products.SearchProducts;
using Pos.Application.Products.UpdateProduct;
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

        // Diagnóstico
        services.AddScoped<GetAppInfoHandler>();
        services.AddScoped<ExportDiagnosticsHandler>();

        return services;
    }
}
