using Pos.Desktop.Resources;
using Pos.Domain.Receivables;

namespace Pos.Desktop.Sales;

/// <summary>Etiqueta del estado de la cuenta por cobrar de una venta a crédito (014).</summary>
internal static class CreditStatusLabels
{
    public static string Of(ReceivableStatus status) => status switch
    {
        ReceivableStatus.Pending => Strings.Credit_StatusPending,
        ReceivableStatus.Paid => Strings.Credit_StatusPaid,
        _ => Strings.Credit_StatusCancelled,
    };
}
