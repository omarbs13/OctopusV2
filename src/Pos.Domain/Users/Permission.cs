using System.Diagnostics.CodeAnalysis;

namespace Pos.Domain.Users;

/// <summary>Capacidad puntual que un rol puede tener (FR-010). La asignación vive en <see cref="RolePermissions"/>.</summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "El nombre del dominio es Permiso (Permission).")]
public enum Permission
{
    Sell,
    ViewOwnSales,
    ViewAllSales,
    ViewProducts,
    ManageProducts,
    ViewInventory,
    RegisterMovements,
    CancelSales,
    OpenDrawerWithoutSale,
    ManageUsers,
    ViewAuditLog,
    ManageSettings,
    ExportDiagnostics,
    OperateShift,
    WithdrawCash,
    ManageShifts,
    ViewReports,

    /// <summary>Importar la licencia y exportar la solicitud (011); solo el Administrador, nunca bloqueado por la licencia.</summary>
    ManageLicense,

    /// <summary>Iniciar cancelaciones y devoluciones (013); Cajero y Administrador.</summary>
    ProcessReturns,

    /// <summary>Autorizar cancelaciones y devoluciones (013); solo el Administrador, siempre exigida.</summary>
    ApproveReturns,

    /// <summary>Listar notas de crédito y reintegros pendientes, marcarlos y configurar el plazo (013); solo el Administrador.</summary>
    ManageCreditNotes,

    /// <summary>Alta, edición de datos y consulta de clientes (014); Cajero y Administrador.</summary>
    ManageCustomers,

    /// <summary>Vender a crédito a un cliente con crédito disponible (014); Cajero y Administrador.</summary>
    SellOnCredit,

    /// <summary>Registrar abonos de clientes (014); Cajero y Administrador.</summary>
    RegisterCustomerPayments,

    /// <summary>Asignar límite y modalidad, desactivar clientes y configurar el plazo de pago (014); solo el Administrador.</summary>
    ManageCustomerCredit,

    /// <summary>Autorizar una venta a crédito que excede el límite (014); solo el Administrador.</summary>
    ApproveCreditOverLimit,

    /// <summary>Autorizar la anulación de un abono (014); solo el Administrador, siempre exigida.</summary>
    VoidCustomerPayments,

    /// <summary>Ver "Reportes > Créditos" (014); solo el Administrador.</summary>
    ViewReceivables,

    /// <summary>Aplicar descuentos de línea, globales y cupones en la venta (015); Cajero y Administrador.</summary>
    ApplyDiscounts,

    /// <summary>Autorizar un descuento que supera el límite (015); solo el Administrador, autorizable.</summary>
    ApproveDiscounts,

    /// <summary>Gestionar cupones y el límite de descuento (015); solo el Administrador.</summary>
    ManageDiscounts,

    /// <summary>Ver "Descuentos > Reporte" (015); solo el Administrador.</summary>
    ViewDiscountReport,
}
