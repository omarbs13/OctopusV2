using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Domain.Users;

namespace Pos.Desktop.Returns;

public static class ReturnsModule
{
    public const string PageId = "sales.returns";

    /// <summary>
    /// Devoluciones y vales (013): la página del Administrador dentro del grupo Ventas. Pertenece al
    /// módulo Devoluciones, así que desaparece del menú sin licencia (<c>ManageCreditNotes</c>).
    /// </summary>
    public static IServiceCollection AddReturnsModule(this IServiceCollection services)
    {
        services.AddPage<ReturnsAdminViewModel, ReturnsAdminView>(
            PageId,
            Strings.Nav_Returns,
            "Icon.CashRefund",
            30,
            SalesModule.GroupId,
            permission: Permission.ManageCreditNotes);
        return services;
    }
}
