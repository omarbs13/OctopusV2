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
    public const string SaleReturned = "SALE_RETURNED";
    public const string CardReversalDone = "CARD_REVERSAL_DONE";
    public const string CreditNoteRedeemed = "CREDIT_NOTE_REDEEMED";
    public const string ReturnSettingsChanged = "RETURN_SETTINGS_CHANGED";
    public const string DrawerOpened = "DRAWER_OPENED";
    public const string ShiftOpened = "SHIFT_OPENED";
    public const string CashDeposit = "CASH_DEPOSIT";
    public const string CashWithdrawal = "CASH_WITHDRAWAL";
    public const string ShiftCashCounted = "SHIFT_CASH_COUNTED";
    public const string ShiftClosed = "SHIFT_CLOSED";
    public const string ShiftClosedByAdmin = "SHIFT_CLOSED_BY_ADMIN";
    public const string ReportExported = "REPORT_EXPORTED";
    public const string ReportSettingsChanged = "REPORT_SETTINGS_CHANGED";
    public const string LicenseImported = "LICENSE_IMPORTED";
    public const string LicenseRecovered = "LICENSE_RECOVERED";
    public const string CustomerCreated = "CUSTOMER_CREATED";
    public const string CustomerUpdated = "CUSTOMER_UPDATED";
    public const string CustomerCreditChanged = "CUSTOMER_CREDIT_CHANGED";
    public const string CustomerDeactivated = "CUSTOMER_DEACTIVATED";
    public const string CustomerActivated = "CUSTOMER_ACTIVATED";
    public const string CreditSaleRegistered = "CREDIT_SALE_REGISTERED";
    public const string CreditLimitOverride = "CREDIT_LIMIT_OVERRIDE";
    public const string CustomerPaymentRegistered = "CUSTOMER_PAYMENT_REGISTERED";
    public const string CustomerPaymentVoided = "CUSTOMER_PAYMENT_VOIDED";
    public const string CreditSettingsChanged = "CREDIT_SETTINGS_CHANGED";
    public const string DiscountAuthorized = "DISCOUNT_AUTHORIZED";
    public const string DiscountAppliedAuthorized = "DISCOUNT_APPLIED_AUTHORIZED";
    public const string CouponCreated = "COUPON_CREATED";
    public const string CouponUpdated = "COUPON_UPDATED";
    public const string CouponDeactivated = "COUPON_DEACTIVATED";
    public const string CouponUseReleased = "COUPON_USE_RELEASED";
    public const string DiscountLimitChanged = "DISCOUNT_LIMIT_CHANGED";

    public const string UserEntity = "User";
    public const string CashShiftEntity = "CashShift";
    public const string ReportEntity = "Report";
    public const string LicenseEntity = "License";
    public const string SaleEntity = "Sale";
    public const string SaleReturnEntity = "SaleReturn";
    public const string CreditNoteEntity = "CreditNote";
    public const string ReturnSettingsEntity = "ReturnSettings";
    public const string CustomerEntity = "Customer";
    public const string CustomerPaymentEntity = "CustomerPayment";
    public const string ReceivablesSettingsEntity = "ReceivablesSettings";
    public const string DiscountApprovalEntity = "DiscountApproval";
    public const string CouponEntity = "Coupon";
    public const string DiscountSettingsEntity = "DiscountSettings";

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
        [SaleReturned] = "Devolución parcial de venta",
        [CardReversalDone] = "Reversa de tarjeta realizada",
        [CreditNoteRedeemed] = "Nota de crédito usada",
        [ReturnSettingsChanged] = "Plazo de devoluciones modificado",
        [DrawerOpened] = "Cajón abierto sin venta",
        [ShiftOpened] = "Turno abierto",
        [CashDeposit] = "Ingreso de efectivo",
        [CashWithdrawal] = "Retiro de efectivo",
        [ShiftCashCounted] = "Conteo de caja",
        [ShiftClosed] = "Turno cerrado",
        [ShiftClosedByAdmin] = "Turno cerrado por administrador",
        [ReportExported] = "Reporte exportado",
        [ReportSettingsChanged] = "Configuración de reportes modificada",
        [LicenseImported] = "Licencia importada",
        [LicenseRecovered] = "Licencia regenerada",
        [CustomerCreated] = "Cliente creado",
        [CustomerUpdated] = "Cliente modificado",
        [CustomerCreditChanged] = "Límite o modalidad de crédito modificados",
        [CustomerDeactivated] = "Cliente desactivado",
        [CustomerActivated] = "Cliente activado",
        [CreditSaleRegistered] = "Venta a crédito",
        [CreditLimitOverride] = "Venta a crédito sobre el límite autorizada",
        [CustomerPaymentRegistered] = "Abono registrado",
        [CustomerPaymentVoided] = "Abono anulado",
        [CreditSettingsChanged] = "Plazo de pago modificado",
        [DiscountAuthorized] = "Descuento autorizado",
        [DiscountAppliedAuthorized] = "Venta con descuento autorizado",
        [CouponCreated] = "Cupón creado",
        [CouponUpdated] = "Cupón modificado",
        [CouponDeactivated] = "Cupón desactivado",
        [CouponUseReleased] = "Uso de cupón devuelto",
        [DiscountLimitChanged] = "Límite de descuento modificado",
    }.ToFrozenDictionary();

    /// <summary>Texto en español del evento; el código tal cual si no está en el catálogo.</summary>
    public static string Describe(string action) => All.TryGetValue(action, out var text) ? text : action;
}
