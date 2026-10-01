using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Discounts;

public static class DiscountsModule
{
    public const string GroupId = "discounts";
    public const string CouponsPageId = "discounts.coupons";
    public const string SettingsPageId = "discounts.settings";
    public const string ReportPageId = "discounts.report";

    /// <summary>
    /// Descuentos y promociones (015): grupo "Descuentos" después de Reportes con Cupones, Configuración y
    /// Reporte. Sus permisos pertenecen al módulo <c>Discounts</c>, así que desaparece del menú sin licencia.
    /// </summary>
    public static IServiceCollection AddDiscountsModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Discounts, "Icon.Sales", 8);
        services.AddPage<CouponListViewModel, CouponListView>(CouponsPageId, Strings.Nav_DiscountCoupons, "Icon.Catalog", 0, GroupId, permission: Permission.ManageDiscounts);
        services.AddPage<DiscountSettingsViewModel, DiscountSettingsView>(SettingsPageId, Strings.Nav_DiscountSettings, "Icon.Catalog", 10, GroupId, permission: Permission.ManageDiscounts);
        services.AddPage<DiscountReportViewModel, DiscountReportView>(ReportPageId, Strings.Nav_DiscountReport, "Icon.Chart", 20, GroupId, permission: Permission.ViewDiscountReport);

        services.AddTransient<CouponFormViewModel>();
        services.AddScoped<Func<CouponFormViewModel>>(sp => sp.GetRequiredService<CouponFormViewModel>);
        services.AddComponentView<CouponFormViewModel, CouponFormView>();
        return services;
    }
}
