using Pos.Domain.Common;

namespace Pos.Domain.Categories;

/// <summary>
/// Categoría de productos (agregado, 016). Un solo nivel; el nombre es único sin distinguir mayúsculas
/// ni acentos entre las no borradas (<see cref="NameKey"/>). Los campos de auditoría los asigna la
/// persistencia.
/// </summary>
public sealed class Category
{
    public const int NameMaxLength = 50;
    public const int DescriptionMaxLength = 200;

    private Category()
    {
        Name = string.Empty;
        NameKey = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Nombre sin acentos y en minúsculas; único entre las categorías no borradas (FR-003).</summary>
    public string NameKey { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Una categoría inactiva conserva sus productos, pero no se ofrece al asignar (FR-005, FR-006).</summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public int Version { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public static Category Create(string name, string? description)
    {
        var category = new Category
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            Version = 1,
        };
        category.Update(name, description);
        return category;
    }

    /// <summary>Cambia nombre y descripción con las mismas validaciones del alta (FR-004).</summary>
    public void Update(string name, string? description)
    {
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);

        if (!IsValidName(normalizedName))
        {
            throw new DomainException($"El nombre es obligatorio y admite hasta {NameMaxLength} caracteres.");
        }

        if (!IsValidDescription(normalizedDescription))
        {
            throw new DomainException($"La descripción admite hasta {DescriptionMaxLength} caracteres.");
        }

        Name = normalizedName;
        NameKey = TextNormalizer.ForSearch(normalizedName);
        Description = normalizedDescription;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void Delete(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("La fecha de borrado debe estar en UTC.");
        }

        DeletedAt ??= utcNow;
    }

    /// <summary>Recorta el nombre. Nulo o vacío se trata como vacío.</summary>
    public static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    /// <summary>Recorta; vacía o solo espacios se convierte en nula.</summary>
    public static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    public static bool IsValidName(string normalizedName) =>
        normalizedName.Length is > 0 and <= NameMaxLength;

    public static bool IsValidDescription(string? normalizedDescription) =>
        normalizedDescription is null || normalizedDescription.Length <= DescriptionMaxLength;
}
