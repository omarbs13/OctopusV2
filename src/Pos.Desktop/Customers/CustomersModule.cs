using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Customers;

public static class CustomersModule
{
    public const string GroupId = "customers";
    public const string ListPageId = "customers.list";

    /// <summary>
    /// Clientes y crédito (014): grupo entre Ventas (5) y Reportes (7). Sus opciones exigen permisos del
    /// módulo <c>CreditAndCustomers</c>, así que desaparece del menú sin licencia (contracts/ui.md "Navegación").
    /// </summary>
    public static IServiceCollection AddCustomersModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Customers, "Icon.Users", 6);
        services.AddPage<CustomerListViewModel, CustomerListView>(ListPageId, Strings.Customer_ListTitle, "Icon.Users", 0, GroupId, permission: Permission.ManageCustomers);

        services.AddTransient<CustomerFormViewModel>();
        services.AddScoped<Func<CustomerFormViewModel>>(sp => sp.GetRequiredService<CustomerFormViewModel>);
        services.AddComponentView<CustomerFormViewModel, CustomerFormView>();

        services.AddTransient<CustomerDetailViewModel>();
        services.AddScoped<Func<CustomerDetailViewModel>>(sp => sp.GetRequiredService<CustomerDetailViewModel>);
        services.AddComponentView<CustomerDetailViewModel, CustomerDetailView>();
        services.AddComponentView<RegisterPaymentViewModel, RegisterPaymentView>();
        services.AddComponentView<VoidPaymentViewModel, VoidPaymentView>();
        return services;
    }
}
