using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Domain.Suppliers;

namespace Pos.Application.Suppliers;

/// <summary>Pasos compartidos por el alta y la edición: RUC en uso y traducción del resultado del guardado.</summary>
internal static class SupplierSaving
{
    /// <summary>Otro proveedor (distinto de <paramref name="exceptId"/>) que ya usa el RUC, o nulo.</summary>
    public static async Task<SupplierTaxIdInUse?> TaxIdInUseAsync(
        ISupplierRepository suppliers,
        string? taxId,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        var normalized = Supplier.NormalizeTaxId(taxId);
        if (normalized is null)
        {
            return null;
        }

        var existing = await suppliers.FindByTaxIdAsync(normalized, cancellationToken);
        return existing is null || existing.Id == exceptId ? null : new SupplierTaxIdInUse(existing.Id, existing.Name);
    }

    /// <summary>Error de un guardado fallido; el índice de RUC violado vuelve a buscar al proveedor que lo tiene.</summary>
    public static async Task<Error> ToErrorAsync(
        ISupplierRepository suppliers,
        SaveOutcome outcome,
        string? taxId,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        if (outcome.Status != SaveStatus.Duplicate)
        {
            return new Conflict();
        }

        return await TaxIdInUseAsync(suppliers, taxId, exceptId, cancellationToken) ?? (Error)new Conflict();
    }
}
