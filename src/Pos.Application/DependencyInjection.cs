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
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.CloseShift;
using Pos.Application.CashShifts.CountShiftCash;
using Pos.Application.CashShifts.GenerateShiftReadout;
using Pos.Application.CashShifts.GetCurrentShift;
using Pos.Application.CashShifts.GetShiftCut;
using Pos.Application.CashShifts.GetShiftDetail;
using Pos.Application.CashShifts.OpenShift;
using Pos.Application.CashShifts.RegisterCashMovement;
using Pos.Application.CashShifts.SearchShiftCuts;
using Pos.Application.CashShifts.SearchShifts;
using Pos.Application.CreditNotes.GetCreditNoteBalance;
using Pos.Application.CreditNotes.GetCreditNoteDetail;
using Pos.Application.CreditNotes.SearchCreditNotes;
using Pos.Application.Categories.CreateCategory;
using Pos.Application.Categories.DeleteCategory;
using Pos.Application.Categories.GetCategory;
using Pos.Application.Categories.ListCategoryOptions;
using Pos.Application.Categories.SearchCategories;
using Pos.Application.Categories.SetCategoryActive;
using Pos.Application.Categories.UpdateCategory;
using Pos.Application.Customers.CreateCustomer;
using Pos.Application.Customers.FindCustomersForSale;
using Pos.Application.Customers.GetCustomerCreditStatus;
using Pos.Application.Customers.GetCustomer;
using Pos.Application.Customers.SearchCustomers;
using Pos.Application.Customers.SetCustomerActive;
using Pos.Application.Customers.UpdateCustomer;
using Pos.Application.Discounts;
using Pos.Application.Discounts.ApproveDiscount;
using Pos.Application.Discounts.Coupons.GetCoupon;
using Pos.Application.Discounts.Coupons.SaveCoupon;
using Pos.Application.Discounts.Coupons.SearchCoupons;
using Pos.Application.Discounts.Coupons.SetCouponActive;
using Pos.Application.Discounts.GetDiscountReport;
using Pos.Application.Discounts.ResolveCoupon;
using Pos.Application.Discounts.Settings.GetDiscountSettings;
using Pos.Application.Discounts.Settings.SaveDiscountSettings;
using Pos.Application.Receivables;
using Pos.Application.Receivables.GetReceivablesSettings;
using Pos.Application.Receivables.ListCustomerPayments;
using Pos.Application.Receivables.ListCustomerReceivables;
using Pos.Application.Receivables.RegisterCustomerPayment;
using Pos.Application.Receivables.SaveReceivablesSettings;
using Pos.Application.Receivables.VoidCustomerPayment;
using Pos.Application.Returns;
using Pos.Application.Returns.GetReturnsSettings;
using Pos.Application.Returns.MarkReversalDone;
using Pos.Application.Returns.PreviewReturn;
using Pos.Application.Returns.ReturnSaleItems;
using Pos.Application.Returns.SaveReturnsSettings;
using Pos.Application.Returns.SearchPendingReversals;
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
using Pos.Application.Licensing;
using Pos.Application.Licensing.ExportLicenseRequest;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Application.Licensing.ImportLicense;
using Pos.Application.Startup;
using Pos.Application.Audit.ConfirmAuditExport;
using Pos.Application.Audit.ExportAuditLog;
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
using Pos.Application.Reports;
using Pos.Application.Reports.Export;
using Pos.Application.Reports.GetCashCountReport;
using Pos.Application.Reports.GetInventoryReport;
using Pos.Application.Reports.GetMyShiftSummary;
using Pos.Application.Reports.GetReportAlerts;
using Pos.Application.Reports.GetReceivablesReport;
using Pos.Application.Reports.GetReportSettings;
using Pos.Application.Reports.GetSalesReport;
using Pos.Application.Reports.ListMyShifts;
using Pos.Application.Reports.SaveReportSettings;
using Pos.Application.Reports.SetProductCritical;
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
        services.AddSingleton<IValidator<ExportAuditLogCommand>, ExportAuditLogValidator>();
        services.AddScoped<AuditLogDocumentBuilder>();
        services.AddScoped<ExportAuditLogHandler>();
        services.AddScoped<ConfirmAuditExportHandler>();
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

        // Devoluciones y notas de crédito (013)
        services.AddScoped<ReturnCashGate>();
        services.AddScoped<SaleReturnProcessor>();
        services.AddScoped<PreviewReturnHandler>();
        services.AddSingleton<IValidator<ReturnSaleItemsCommand>, ReturnSaleItemsValidator>();
        services.AddScoped<ReturnSaleItemsHandler>();
        services.AddScoped<SearchPendingReversalsHandler>();
        services.AddScoped<MarkReversalDoneHandler>();
        services.AddScoped<GetReturnsSettingsHandler>();
        services.AddSingleton<IValidator<SaveReturnsSettingsCommand>, SaveReturnsSettingsValidator>();
        services.AddScoped<SaveReturnsSettingsHandler>();
        services.AddScoped<GetCreditNoteBalanceHandler>();
        services.AddScoped<SearchCreditNotesHandler>();
        services.AddScoped<GetCreditNoteDetailHandler>();
        services.AddScoped<GetSalesDashboardHandler>();

        // Clientes y crédito (014)
        services.AddScoped<CreditAging>();
        services.AddScoped<SearchCategoriesHandler>();
        services.AddScoped<GetCategoryHandler>();
        services.AddSingleton<IValidator<CreateCategoryCommand>, CreateCategoryValidator>();
        services.AddScoped<CreateCategoryHandler>();
        services.AddSingleton<IValidator<UpdateCategoryCommand>, UpdateCategoryValidator>();
        services.AddScoped<UpdateCategoryHandler>();
        services.AddScoped<SetCategoryActiveHandler>();
        services.AddScoped<DeleteCategoryHandler>();
        services.AddScoped<ListCategoryOptionsHandler>();

        services.AddSingleton<IValidator<CreateCustomerCommand>, CreateCustomerValidator>();
        services.AddScoped<CreateCustomerHandler>();
        services.AddSingleton<IValidator<UpdateCustomerCommand>, UpdateCustomerValidator>();
        services.AddScoped<UpdateCustomerHandler>();
        services.AddScoped<SetCustomerActiveHandler>();
        services.AddScoped<SearchCustomersHandler>();
        services.AddScoped<GetCustomerHandler>();
        services.AddScoped<FindCustomersForSaleHandler>();
        services.AddScoped<GetCustomerCreditStatusHandler>();
        services.AddScoped<ListCustomerReceivablesHandler>();
        services.AddScoped<PaymentShiftGate>();
        services.AddScoped<CreditSettlementService>();
        services.AddSingleton<IValidator<RegisterCustomerPaymentCommand>, RegisterCustomerPaymentValidator>();
        services.AddScoped<RegisterCustomerPaymentHandler>();
        services.AddSingleton<IValidator<VoidCustomerPaymentCommand>, VoidCustomerPaymentValidator>();
        services.AddScoped<VoidCustomerPaymentHandler>();
        services.AddScoped<ListCustomerPaymentsHandler>();
        services.AddScoped<GetReceivablesSettingsHandler>();
        services.AddSingleton<IValidator<SaveReceivablesSettingsCommand>, SaveReceivablesSettingsValidator>();
        services.AddScoped<SaveReceivablesSettingsHandler>();
        services.AddScoped<GetReceivablesReportHandler>();

        // Turnos de caja (008)
        services.AddScoped<ShiftGuard>();
        services.AddScoped<GetCurrentShiftHandler>();
        services.AddSingleton<IValidator<OpenShiftCommand>, OpenShiftValidator>();
        services.AddScoped<OpenShiftHandler>();
        services.AddSingleton<IValidator<RegisterCashMovementCommand>, RegisterCashMovementValidator>();
        services.AddScoped<RegisterCashMovementHandler>();
        services.AddScoped<CountShiftCashHandler>();
        services.AddScoped<CloseShiftHandler>();
        services.AddScoped<SearchShiftsHandler>();
        services.AddScoped<GetShiftDetailHandler>();
        services.AddScoped<GenerateShiftReadoutHandler>();
        services.AddScoped<GetShiftCutHandler>();
        services.AddScoped<SearchShiftCutsHandler>();

        // Reportes y análisis (009)
        services.AddSingleton(_ => ReportPeriodResolver.ForLocalZone());
        services.AddScoped<GetSalesReportHandler>();
        services.AddScoped<GetCashCountReportHandler>();
        services.AddScoped<GetReportAlertsHandler>();
        services.AddScoped<SetProductCriticalHandler>();
        services.AddScoped<GetMyShiftSummaryHandler>();
        services.AddScoped<ListMyShiftsHandler>();
        services.AddScoped<ReportDocumentBuilder>();
        services.AddScoped<ExportReportHandler>();
        services.AddScoped<GetInventoryReportHandler>();
        services.AddScoped<GetReportSettingsHandler>();
        services.AddScoped<SaveReportSettingsHandler>();

        // Descuentos y promociones (015)
        services.AddSingleton<IValidator<ApproveDiscountCommand>, ApproveDiscountValidator>();
        services.AddScoped<ApproveDiscountHandler>();
        services.AddScoped<GetDiscountSettingsHandler>();
        services.AddSingleton<IValidator<SaveDiscountSettingsCommand>, SaveDiscountSettingsValidator>();
        services.AddScoped<SaveDiscountSettingsHandler>();
        services.AddScoped<ResolveCouponHandler>();
        services.AddSingleton<IValidator<SaveCouponCommand>, SaveCouponValidator>();
        services.AddScoped<SaveCouponHandler>();
        services.AddScoped<SearchCouponsHandler>();
        services.AddScoped<GetCouponHandler>();
        services.AddScoped<SetCouponActiveHandler>();
        services.AddScoped<CouponUseRelease>();
        services.AddScoped<GetDiscountReportHandler>();

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

        // Licencia local (011)
        services.AddSingleton<ILicenseState, LicenseState>();
        services.AddSingleton(VendorContact.Default);
        services.AddSingleton<LicenseBootstrapper>();
        services.AddSingleton<GetLicenseStatusHandler>();
        services.AddScoped<ImportLicenseHandler>();
        services.AddScoped<ExportLicenseRequestHandler>();

        // Diagnóstico
        services.AddScoped<GetAppInfoHandler>();
        services.AddScoped<ExportDiagnosticsHandler>();

        return services;
    }
}
