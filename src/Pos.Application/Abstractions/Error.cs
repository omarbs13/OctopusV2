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

/// <summary>La exportación de diagnóstico no pudo completarse.</summary>
public sealed record ExportFailed(string Message) : Error;
