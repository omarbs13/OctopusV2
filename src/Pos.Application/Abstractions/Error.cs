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
