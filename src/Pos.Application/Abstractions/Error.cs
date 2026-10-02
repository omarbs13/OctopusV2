using Pos.Domain.Discounts;
using Pos.Domain.Licensing;

namespace Pos.Application.Abstractions;

/// <summary>Error de negocio devuelto por un caso de uso. Las fallas inesperadas son excepciones.</summary>
public abstract record Error;

/// <summary>Datos de entrada inválidos; un mensaje en español por campo.</summary>
public sealed record ValidationFailed(IReadOnlyList<FieldError> Errors) : Error;

/// <summary>Otro producto no borrado ya usa el valor del campo indicado.</summary>
public sealed record Duplicate(string Field) : Error;

/// <summary>El registro no existe o está borrado.</summary>
public sealed record NotFound : Error;

/// <summary>Otra operación modificó el registro después de que se abrió.</summary>
public sealed record Conflict : Error;

/// <summary>El archivo no se puede usar como imagen de producto (003, FR-023).</summary>
public sealed record InvalidImage(InvalidImageReason Reason) : Error;

public enum InvalidImageReason
{
    /// <summary>Pesa más de 5 MB.</summary>
    TooLarge,

    /// <summary>No es JPG, PNG ni WEBP (según su contenido, no su extensión).</summary>
    UnsupportedFormat,

    /// <summary>Está dañado o no se puede decodificar.</summary>
    Corrupt,

    /// <summary>Sus dimensiones exceden lo que se puede procesar con seguridad.</summary>
    DimensionsTooLarge,
}

/// <summary>
/// Al confirmar la venta, un precio cambió o una línea dejó de ser vendible; no se guardó nada
/// (005, research §5). Trae la revisión vigente de cada línea.
/// </summary>
public sealed record SaleChanged(IReadOnlyList<Pos.Application.Sales.SaleLineReview> Lines) : Error;

/// <summary>El borrador ya tiene una venta registrada; la interfaz lo trata como éxito (005, FR-020).</summary>
public sealed record AlreadyRegistered(Guid SaleId, string Folio) : Error;

/// <summary>La operación no aplica al estado actual del registro, por ejemplo cancelar una venta cancelada.</summary>
public sealed record InvalidState(string Message) : Error;

/// <summary>La exportación de diagnóstico no pudo completarse.</summary>
public sealed record ExportFailed(string Message) : Error;

/// <summary>El usuario conectado no tiene el permiso; <c>CanBeAuthorized</c> indica si un administrador puede autorizarlo (007, FR-013).</summary>
public sealed record Forbidden(Pos.Domain.Users.Permission Permission, bool CanBeAuthorized) : Error;

/// <summary>Mensaje genérico "Usuario o contraseña incorrectos": no indica cuál dato falló (007, FR-004).</summary>
public sealed record InvalidCredentials : Error;

/// <summary>Bloqueo temporal por intentos fallidos hasta <c>UntilUtc</c> (007, FR-005).</summary>
public sealed record LockedOut(DateTime UntilUtc) : Error;

/// <summary>La operación dejaría el sistema sin administrador activo, o un administrador intenta desactivarse o quitarse el rol (007, FR-018).</summary>
public sealed record LastAdministrator : Error;

/// <summary>No hay turno abierto; se requiere uno para vender (008, FR-001).</summary>
public sealed record ShiftRequired : Error;

/// <summary>El turno abierto es de otro usuario; hay que cerrarlo antes de vender (008, FR-005).</summary>
public sealed record ShiftOwnedByOther(string OpenedByName) : Error;

/// <summary>Ya existe un turno abierto en la caja (008, FR-004).</summary>
public sealed record ShiftAlreadyOpen : Error;

/// <summary>El turno ya está cerrado y no admite cambios (008, FR-018).</summary>
public sealed record ShiftClosed : Error;

/// <summary>
/// El retiro o la devolución excede el efectivo esperado. <c>AvailableCents</c> solo se llena para
/// quien tiene <c>ManageShifts</c> y únicamente en retiros (008, FR-008, FR-010).
/// </summary>
public sealed record InsufficientCash(long? AvailableCents) : Error;

/// <summary>El dueño del turno tiene una venta en curso; hay que terminarla o cancelarla (008, FR-013).</summary>
public sealed record SaleInProgress : Error;

/// <summary>Un administrador cierra el turno de otro usuario que tiene una venta conservada; falta confirmar el descarte (008, FR-013).</summary>
public sealed record HeldSaleWillBeDiscarded(string OwnerName) : Error;

/// <summary>El efectivo esperado cambió entre el conteo y el cierre; hay que revisar las cifras (008, research §8).</summary>
public sealed record ShiftChanged : Error;

/// <summary>El módulo no está activo en la licencia (012, FR-013).</summary>
public sealed record ModuleNotLicensed(LicensedModule Module) : Error;

/// <summary>La venta excede el plazo máximo de devoluciones (013, FR-006a).</summary>
public sealed record ReturnWindowExpired(int Days) : Error;

/// <summary>Sin líneas, o la cantidad excede lo disponible para devolver (013, FR-009, FR-014).</summary>
public sealed record NothingToReturn : Error;

/// <summary>El folio de la nota de crédito no existe o no tiene saldo (013, Historia 4).</summary>
public sealed record CreditNoteNotFound : Error;

/// <summary>El monto excede el saldo de la nota de crédito (013, FR-008).</summary>
public sealed record InsufficientCreditNote(long AvailableCents) : Error;

/// <summary>
/// Saldo + venta exceden el límite de crédito del cliente y no hay concesión válida (014, FR-006).
/// Mensaje: "La venta excede el límite de crédito del cliente por {excedente}. Se requiere autorización de un Administrador".
/// </summary>
public sealed record CreditLimitExceeded(long ExcessCents) : Error;

/// <summary>El cliente está inactivo o es "solo efectivo" (014, Historia 2, escenario 4). Mensaje: "Este cliente no tiene crédito disponible".</summary>
public sealed record CustomerNotEligibleForCredit : Error;

/// <summary>
/// Se intenta desactivar un cliente con saldo (014, FR-004).
/// Mensaje: "El cliente tiene un saldo pendiente de {saldo}; no se puede desactivar".
/// </summary>
public sealed record CustomerHasBalance(long BalanceCents) : Error;

/// <summary>
/// El abono es ≤ 0 o mayor que el saldo del cliente (014, FR-010).
/// Mensaje: "El monto debe ser mayor que 0 y no exceder el saldo de {saldo}".
/// </summary>
public sealed record PaymentExceedsBalance(long MaxCents) : Error;

/// <summary>El descuento no supera el límite: no se guarda ninguna aprobación (015, ApproveDiscount).</summary>
public sealed record ApprovalNotNeeded : Error;

/// <summary>
/// Al cobrar, un descuento manual supera el límite vigente y no tiene una aprobación que lo cubra (015, research §7).
/// La interfaz señala la línea (<c>ProductId</c>) o la venta y pide la autorización.
/// </summary>
public sealed record DiscountApprovalRequired(DiscountScope Scope, Guid? ProductId) : Error;

/// <summary>
/// El cupón ya no se puede aplicar (015, FR-012): <c>Status</c> nulo si el código no existe. La venta no se
/// registró; la interfaz retira el cupón, recalcula y avisa.
/// </summary>
public sealed record CouponNotValid(string Code, CouponStatus? Status, DateOnly StartsOn, DateOnly EndsOn) : Error;

/// <summary>El descuento global de monto fijo supera el subtotal; la interfaz lo retira y avisa (015, Historia 2, escenario 5).</summary>
public sealed record OrderDiscountRemoved(long DiscountCents) : Error;

/// <summary>El código del cupón coincide con el código de barras o la clave de un producto (015, casos límite).</summary>
public sealed record CodeCollidesWithProduct : Error;

/// <summary>Un cupón con usos no admite cambiar código, modalidad ni valor (015, FR-010).</summary>
public sealed record CouponHasUses : Error;

/// <summary>
/// Se desactiva una categoría con productos sin confirmar (016, FR-005). Mensaje: "La categoría tiene {n}
/// productos. Seguirán asignados a ella, pero no podrá asignarse a productos nuevos".
/// </summary>
public sealed record ConfirmationRequired(int ProductCount) : Error;

/// <summary>
/// Se intenta eliminar una categoría con productos no borrados (016, FR-007). Mensaje: "No se puede
/// eliminar: la categoría tiene {n} productos. Reasígnalos o desactívala".
/// </summary>
public sealed record CategoryInUse(int ProductCount) : Error;

/// <summary>
/// La categoría elegida para un producto está inactiva, borrada o no existe (016, FR-011). Mensaje: "La
/// categoría elegida ya no está disponible. Elige otra".
/// </summary>
public sealed record CategoryNotAssignable : Error;

/// <summary>El archivo de licencia importado se rechazó; la licencia vigente no cambió (011, FR-012).</summary>
public sealed record InvalidLicense(LicenseImportRejection Reason) : Error;

public enum LicenseImportRejection
{
    /// <summary>El archivo no se pudo leer o no tiene el formato esperado.</summary>
    Unreadable,

    /// <summary>La firma del proveedor no es válida.</summary>
    BadSignature,

    /// <summary>La licencia corresponde a otra máquina.</summary>
    OtherMachine,
}
