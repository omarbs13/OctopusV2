using System.Collections.Frozen;

namespace Pos.Application.Audit;

/// <summary>Catálogo de eventos de la bitácora de auditoría, con su texto en español para la interfaz (007, FR-024).</summary>
public static class AuditActions
{
    public const string LoginSucceeded = "LOGIN_SUCCEEDED";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string UserLockedOut = "USER_LOCKED_OUT";
    public const string Logout = "LOGOUT";
    public const string UserCreated = "USER_CREATED";
    public const string UserUpdated = "USER_UPDATED";
    public const string UserDeactivated = "USER_DEACTIVATED";
    public const string UserActivated = "USER_ACTIVATED";
    public const string PasswordReset = "PASSWORD_RESET";
    public const string PasswordChanged = "PASSWORD_CHANGED";
    public const string AdminAuthorizationGranted = "ADMIN_AUTHORIZATION_GRANTED";
    public const string AdminAuthorizationDenied = "ADMIN_AUTHORIZATION_DENIED";
    public const string HeldSaleDiscarded = "HELD_SALE_DISCARDED";
    public const string SaleCancelled = "SALE_CANCELLED";
    public const string DrawerOpened = "DRAWER_OPENED";
    public const string ShiftOpened = "SHIFT_OPENED";
    public const string CashDeposit = "CASH_DEPOSIT";
    public const string CashWithdrawal = "CASH_WITHDRAWAL";
    public const string ShiftCashCounted = "SHIFT_CASH_COUNTED";
    public const string ShiftClosed = "SHIFT_CLOSED";
    public const string ShiftClosedByAdmin = "SHIFT_CLOSED_BY_ADMIN";

    public const string UserEntity = "User";
    public const string CashShiftEntity = "CashShift";

    /// <summary>Texto en español de cada evento, en el orden en que se ofrece en el filtro.</summary>
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        [LoginSucceeded] = "Inicio de sesión",
        [LoginFailed] = "Intento de acceso fallido",
        [UserLockedOut] = "Usuario bloqueado",
        [Logout] = "Cierre de sesión",
        [UserCreated] = "Usuario creado",
        [UserUpdated] = "Usuario modificado",
        [UserDeactivated] = "Usuario desactivado",
        [UserActivated] = "Usuario activado",
        [PasswordReset] = "Contraseña restablecida",
        [PasswordChanged] = "Contraseña cambiada",
        [AdminAuthorizationGranted] = "Autorización de administrador concedida",
        [AdminAuthorizationDenied] = "Autorización de administrador rechazada",
        [HeldSaleDiscarded] = "Venta conservada descartada",
        [SaleCancelled] = "Venta cancelada",
        [DrawerOpened] = "Cajón abierto sin venta",
        [ShiftOpened] = "Turno abierto",
        [CashDeposit] = "Ingreso de efectivo",
        [CashWithdrawal] = "Retiro de efectivo",
        [ShiftCashCounted] = "Conteo de caja",
        [ShiftClosed] = "Turno cerrado",
        [ShiftClosedByAdmin] = "Turno cerrado por administrador",
    }.ToFrozenDictionary();

    /// <summary>Texto en español del evento; el código tal cual si no está en el catálogo.</summary>
    public static string Describe(string action) => All.TryGetValue(action, out var text) ? text : action;
}
