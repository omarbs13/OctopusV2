using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Pos.Application.Business.GetBusinessProfile;
using Pos.Application.Business.SaveBusinessProfile;
using Pos.Application.Diagnostics.ExportDiagnostics;
using Pos.Application.Diagnostics.GetAppInfo;
using Pos.Application.Inventory.GetStockAlerts;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Inventory.SearchMovements;
using Pos.Application.Inventory.SearchStock;
using Pos.Application.Printing.GetPrintingSettings;
using Pos.Application.Printing.ListPrinters;
using Pos.Application.Printing.OpenCashDrawer;
using Pos.Application.Printing.PrintTicket;
using Pos.Application.Printing.SavePrintingSettings;
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
using Pos.Application.Audit.SearchAuditLog;
using Pos.Application.Security.GetSecuritySettings;
using Pos.Application.Security.SaveSecuritySettings;
using Pos.Application.Users.AuthorizeAdmin;
using Pos.Application.Users.ChangeOwnPassword;
using Pos.Application.Users.CreateFirstAdmin;
using Pos.Application.Users.CreateUser;
using Pos.Application.Users.EndSession;
using Pos.Application.Users.GetSetupState;
using Pos.Application.Users.GetUser;
using Pos.Application.Users.ListCashiers;
using Pos.Application.Users.ResetUserPassword;
using Pos.Application.Users.SearchUsers;
using Pos.Application.Users.SignIn;
using Pos.Application.Users.StartSession;
using Pos.Application.Users.UpdateUser;
using Pos.Application.Users.VerifySessionPassword;
using Pos.Application.Users.Access;
using Pos.Application.Users.Session;

namespace Pos.Application;

public static class DependencyInjection
{
    /// <summary>Registra los casos de uso y sus validadores. Cada funcionalidad agrega los suyos.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseStartup, DatabaseStartup>();

        // Usuarios y permisos (007): la sesión es el punto único del usuario conectado (Principio IV).
        services.AddSingleton<UserSession>();
        services.AddSingleton<IUserSession>(sp => sp.GetRequiredService<UserSession>());
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<UserSession>());
        services.AddSingleton<IAuthorizationGrants, AuthorizationGrants>();
        services.AddSingleton<LoginThrottle>();
        services.AddScoped<IAccessControl, AccessControl>();
        services.AddScoped<CredentialVerifier>();

        services.AddScoped<GetSetupStateHandler>();
        services.AddSingleton<IValidator<CreateFirstAdminCommand>, CreateFirstAdminValidator>();
        services.AddScoped<CreateFirstAdminHandler>();
        services.AddSingleton<IValidator<SignInCommand>, SignInValidator>();
        services.AddScoped<SignInHandler>();
        services.AddScoped<StartSessionHandler>();
        services.AddScoped<EndSessionHandler>();
        services.AddScoped<VerifySessionPasswordHandler>();
        services.AddSingleton<IValidator<ChangeOwnPasswordCommand>, ChangeOwnPasswordValidator>();
        services.AddScoped<ChangeOwnPasswordHandler>();
        services.AddSingleton<IValidator<AuthorizeAdminCommand>, AuthorizeAdminValidator>();
        services.AddScoped<AuthorizeAdminHandler>();
        services.AddScoped<SearchUsersHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddSingleton<IValidator<CreateUserCommand>, CreateUserValidator>();
        services.AddScoped<CreateUserHandler>();
        services.AddSingleton<IValidator<UpdateUserCommand>, UpdateUserValidator>();
        services.AddScoped<UpdateUserHandler>();
        services.AddSingleton<IValidator<ResetUserPasswordCommand>, ResetUserPasswordValidator>();
        services.AddScoped<ResetUserPasswordHandler>();
        services.AddScoped<ListCashiersHandler>();

        // Bitácora y seguridad
        services.AddSingleton<IValidator<SearchAuditLogQuery>, SearchAuditLogValidator>();
        services.AddScoped<SearchAuditLogHandler>();
        services.AddSingleton<GetSecuritySettingsHandler>();
        services.AddScoped<SaveSecuritySettingsHandler>();

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

        // Datos del negocio
        services.AddScoped<GetBusinessProfileHandler>();
        services.AddSingleton<IValidator<SaveBusinessProfileCommand>, SaveBusinessProfileValidator>();
        services.AddScoped<SaveBusinessProfileHandler>();

        // Impresión y cajón
        services.AddSingleton<GetPrintingSettingsHandler>();
        services.AddScoped<SavePrintingSettingsHandler>();
        services.AddScoped<ListPrintersHandler>();
        services.AddScoped<PrintTicketHandler>();
        services.AddSingleton<IValidator<OpenCashDrawerCommand>, OpenCashDrawerValidator>();
        services.AddScoped<OpenCashDrawerHandler>();

        // Diagnóstico
        services.AddScoped<GetAppInfoHandler>();
        services.AddScoped<ExportDiagnosticsHandler>();

        return services;
    }
}
