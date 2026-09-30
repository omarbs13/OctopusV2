using Pos.Domain.Common;

namespace Pos.Domain.Sales;

public enum SaleStatus
{
    Completed,
    Cancelled,
}

public static class SaleStatusExtensions
{
    public static string ToCode(this SaleStatus status) => status switch
    {
        SaleStatus.Completed => "COMPLETED",
        SaleStatus.Cancelled => "CANCELLED",
        _ => throw new DomainException("El estado de la venta no es válido."),
    };

    public static SaleStatus FromCode(string code) => code switch
    {
        "COMPLETED" => SaleStatus.Completed,
        "CANCELLED" => SaleStatus.Cancelled,
        _ => throw new DomainException("El estado de la venta no es válido."),
    };
}
