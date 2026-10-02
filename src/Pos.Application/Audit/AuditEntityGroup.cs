namespace Pos.Application.Audit;

/// <summary>Grupos del filtro "Entidad" de la bitácora (018, FR-017, research §9).</summary>
public enum AuditEntityGroup
{
    Product,
    Sale,
    User,
    Session,
    Category,
    Customer,
    Coupon,
    Purchases,
    CashShift,
    Settings,
    Reports,
}

/// <summary>
/// Criterio de un grupo: una entrada pertenece si su <c>EntityType</c> está en <see cref="EntityTypes"/> y su
/// evento no está en <see cref="ExcludedActions"/>, o si su evento está en <see cref="IncludedActions"/>.
/// </summary>
public sealed record AuditEntityGroupCriteria(
    IReadOnlyList<string> EntityTypes,
    IReadOnlyList<string> IncludedActions,
    IReadOnlyList<string> ExcludedActions);

/// <summary>Texto en español y criterio de cada grupo de entidad (research §9).</summary>
public static class AuditEntityGroups
{
    /// <summary>Eventos de sesión: van en "Sesión" y no en "Usuario", aunque su entidad es <c>User</c>.</summary>
    public static readonly IReadOnlyList<string> SessionActions =
    [
        AuditActions.LoginSucceeded,
        AuditActions.LoginFailed,
        AuditActions.UserLockedOut,
        AuditActions.Logout,
    ];

    /// <summary>Grupos en el orden en que se ofrecen en el filtro.</summary>
    public static readonly IReadOnlyList<AuditEntityGroup> All = Enum.GetValues<AuditEntityGroup>();

    public static string Describe(AuditEntityGroup group) => group switch
    {
        AuditEntityGroup.Product => "Producto",
        AuditEntityGroup.Sale => "Venta",
        AuditEntityGroup.User => "Usuario",
        AuditEntityGroup.Session => "Sesión",
        AuditEntityGroup.Category => "Categoría",
        AuditEntityGroup.Customer => "Cliente",
        AuditEntityGroup.Coupon => "Cupón",
        AuditEntityGroup.Purchases => "Proveedores y compras",
        AuditEntityGroup.CashShift => "Caja/Turno",
        AuditEntityGroup.Settings => "Configuración",
        AuditEntityGroup.Reports => "Reportes y exportaciones",
        _ => throw new ArgumentOutOfRangeException(nameof(group)),
    };

    /// <summary>Texto del grupo al que pertenece una entrada, para la columna "Entidad"; el código tal cual si ninguno aplica.</summary>
    public static string DescribeEntry(string entityType, string action)
    {
        foreach (var group in All)
        {
            var criteria = CriteriaFor(group);
            if (criteria.IncludedActions.Contains(action)
                || (criteria.EntityTypes.Contains(entityType) && !criteria.ExcludedActions.Contains(action)))
            {
                return Describe(group);
            }
        }

        return entityType;
    }

    public static AuditEntityGroupCriteria CriteriaFor(AuditEntityGroup group) => group switch
    {
        AuditEntityGroup.Product => Types(AuditActions.ProductEntity),
        AuditEntityGroup.Sale => Types(AuditActions.SaleEntity, AuditActions.SaleReturnEntity, AuditActions.CreditNoteEntity, AuditActions.DiscountApprovalEntity),
        AuditEntityGroup.User => new([AuditActions.UserEntity], [], SessionActions),
        AuditEntityGroup.Session => new([], SessionActions, []),
        AuditEntityGroup.Category => Types(AuditActions.CategoryEntity),
        AuditEntityGroup.Customer => Types(AuditActions.CustomerEntity, AuditActions.CustomerPaymentEntity),
        AuditEntityGroup.Coupon => Types(AuditActions.CouponEntity),
        AuditEntityGroup.Purchases => Types(AuditActions.SupplierEntity, AuditActions.PurchaseEntity),
        AuditEntityGroup.CashShift => Types(AuditActions.CashShiftEntity, AuditActions.ShiftCutEntity, AuditActions.CashDrawerEntity),
        AuditEntityGroup.Settings => new(
            [AuditActions.ReturnSettingsEntity, AuditActions.ReceivablesSettingsEntity, AuditActions.DiscountSettingsEntity, AuditActions.LicenseEntity],
            [AuditActions.ReportSettingsChanged],
            []),
        AuditEntityGroup.Reports => new([AuditActions.ReportEntity, AuditActions.AuditLogEntity], [], [AuditActions.ReportSettingsChanged]),
        _ => throw new ArgumentOutOfRangeException(nameof(group)),
    };

    private static AuditEntityGroupCriteria Types(params string[] entityTypes) => new(entityTypes, [], []);
}
