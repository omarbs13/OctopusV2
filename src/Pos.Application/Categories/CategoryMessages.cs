using System.Globalization;
using Pos.Domain.Categories;

namespace Pos.Application.Categories;

/// <summary>Mensajes de categorías al operador, en español (contracts/application-ports.md).</summary>
public static class CategoryMessages
{
    public const string NameRequired = "El nombre es obligatorio.";
    public const string NameDuplicate = "Ya existe una categoría con ese nombre";
    public const string NotAssignable = "La categoría elegida ya no está disponible. Elige otra";
    public const string Conflict = "La categoría cambió desde que la abriste. Vuelve a abrirla";
    public const string Uncategorized = "Sin categoría";
    public const string InactiveSuffix = "(inactiva)";

    public static readonly string NameTooLong = $"El nombre admite hasta {Category.NameMaxLength} caracteres.";
    public static readonly string DescriptionTooLong = $"La descripción admite hasta {Category.DescriptionMaxLength} caracteres.";

    public static string ConfirmDeactivate(int productCount) => string.Format(
        CultureInfo.CurrentCulture,
        "La categoría tiene {0} productos. Seguirán asignados a ella, pero no podrá asignarse a productos nuevos",
        productCount);

    public static string InUse(int productCount) => string.Format(
        CultureInfo.CurrentCulture,
        "No se puede eliminar: la categoría tiene {0} productos. Reasígnalos o desactívala",
        productCount);

    /// <summary>Nombre para mostrar: "Sin categoría" si es nulo, y "(inactiva)" si corresponde.</summary>
    public static string Display(string? name, bool isActive) =>
        name is null ? Uncategorized : isActive ? name : $"{name} {InactiveSuffix}";
}
