using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Domain.Common;
using Pos.Domain.Purchases;

namespace Pos.Application.Purchases;

/// <summary>Línea a calcular: decimales y nombre de su unidad, y los textos capturados.</summary>
internal sealed record PurchaseCalculationLine(int DecimalPlaces, string UnitName, string? QuantityText, string? UnitCostText);

/// <summary>Resultado de una línea: valores interpretados, importe y errores por campo.</summary>
internal sealed record PurchaseLineCalculation(
    long? QuantityThousandths,
    long? UnitCostCents,
    long? AmountCents,
    IReadOnlyList<FieldError> Errors)
{
    public bool IsBonus => UnitCostCents == 0;
}

internal sealed record PurchaseCalculation(
    IReadOnlyList<PurchaseLineCalculation> Lines,
    long SubtotalCents,
    long? TaxCents,
    long TotalCents,
    IReadOnlyList<FieldError> Errors)
{
    /// <summary>Todos los errores: los de cada línea y los generales.</summary>
    public IEnumerable<FieldError> AllErrors => Lines.SelectMany(l => l.Errors).Concat(Errors);

    public PurchaseTotalsDto ToDto() => new(
        [.. Lines.Select(l => new PurchaseLineTotalsDto(l.AmountCents, l.IsBonus, l.Errors))],
        SubtotalCents,
        TaxCents ?? 0,
        TotalCents,
        Errors);
}

/// <summary>
/// Núcleo de cálculo compartido por <c>CalculatePurchaseTotals</c> (captura en vivo) y <c>RegisterPurchase</c>
/// (al guardar), para que lo mostrado y lo guardado coincidan (research §4). No usa la base de datos.
/// </summary>
internal static class PurchaseCalculator
{
    public static PurchaseCalculation Calculate(IReadOnlyList<PurchaseCalculationLine> lines, string? taxText)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var results = new List<PurchaseLineCalculation>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            results.Add(CalculateLine(i, lines[i]));
        }

        var errors = new List<FieldError>();
        long? tax = 0;
        if (!string.IsNullOrWhiteSpace(taxText))
        {
            var parsed = Money.Parse(taxText);
            tax = parsed.Value?.Cents;
            if (parsed.Error is { } error)
            {
                errors.Add(new FieldError(PurchaseFields.Tax, PurchaseMessages.ForTax(error)));
            }
        }

        var subtotal = PurchaseMath.Subtotal(results.Where(r => r.AmountCents is not null).Select(r => r.AmountCents!.Value));
        var total = PurchaseMath.Total(subtotal, tax ?? 0);
        if (results.Count > 0 && results.All(r => r.Errors.Count == 0))
        {
            if (subtotal > Money.MaxCents)
            {
                errors.Add(new FieldError(PurchaseFields.Subtotal, PurchaseMessages.SubtotalTooLarge));
            }
            else if (subtotal == 0)
            {
                errors.Add(new FieldError(PurchaseFields.Subtotal, PurchaseMessages.SubtotalZero));
            }
            else if (tax is not null && total > Money.MaxCents)
            {
                errors.Add(new FieldError(PurchaseFields.Subtotal, PurchaseMessages.TotalTooLarge));
            }
        }

        return new PurchaseCalculation(results, subtotal, tax, total, errors);
    }

    private static PurchaseLineCalculation CalculateLine(int index, PurchaseCalculationLine line)
    {
        var errors = new List<FieldError>();

        var quantity = Quantity.Parse(line.QuantityText, line.DecimalPlaces);
        if (quantity.Error is { } quantityError)
        {
            errors.Add(new FieldError(
                PurchaseFields.LineQuantity(index),
                InventoryMessages.ForQuantity(quantityError, line.UnitName, line.DecimalPlaces)));
        }

        var cost = Money.Parse(line.UnitCostText);
        if (cost.Error is { } costError)
        {
            errors.Add(new FieldError(PurchaseFields.LineUnitCost(index), PurchaseMessages.ForCost(costError)));
        }

        var quantityThousandths = quantity.Value?.Thousandths;
        var costCents = cost.Value?.Cents;
        long? amount = null;
        if (quantityThousandths is { } q && costCents is { } c)
        {
            if (PurchaseMath.LineExceedsMaximum(q, c))
            {
                errors.Add(new FieldError(PurchaseFields.LineUnitCost(index), PurchaseMessages.LineAmountTooLarge));
            }
            else
            {
                amount = PurchaseMath.LineAmount(q, c);
            }
        }

        return new PurchaseLineCalculation(quantityThousandths, costCents, amount, errors);
    }
}
