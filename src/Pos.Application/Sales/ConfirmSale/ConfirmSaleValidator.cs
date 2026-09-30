using FluentValidation;
using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Application.Sales.ConfirmSale;

/// <summary>Forma de la entrada; las reglas de negocio las aplican el manejador y los objetos de dominio.</summary>
public sealed class ConfirmSaleValidator : AbstractValidator<ConfirmSaleCommand>
{
    public const int MaxLines = 500;

    public ConfirmSaleValidator()
    {
        RuleFor(c => c.DraftId)
            .NotEqual(Guid.Empty).WithMessage(SaleMessages.LinesRequired)
            .OverridePropertyName(SaleFields.DraftId);

        RuleFor(c => c.Lines)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(SaleMessages.LinesRequired)
            .Must(lines => lines.Count <= MaxLines).WithMessage(SaleMessages.TooManyLines)
            .Must(lines => lines.Select(l => l.ProductId).Distinct().Count() == lines.Count)
            .WithMessage(SaleMessages.ProductRepeated)
            .OverridePropertyName(SaleFields.Lines);

        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.QuantityThousandths)
                .InclusiveBetween(1, Quantity.MaxCaptureThousandths).WithMessage(SaleMessages.QuantityInvalid);
            line.RuleFor(l => l.ExpectedUnitPriceCents)
                .InclusiveBetween(0, Money.MaxCents).WithMessage(SaleMessages.PriceInvalid);
        }).OverridePropertyName(SaleFields.Lines);

        RuleFor(c => c.Payments)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(SaleMessages.PaymentsRequired)
            .Must(payments => payments.Count(p => p.Method == PaymentMethod.Cash) <= 1)
            .WithMessage(SaleMessages.PaymentInvalid)
            .Must(payments => payments.All(IsWellFormed)).WithMessage(SaleMessages.PaymentInvalid)
            .Must(payments => payments.All(p => NormalizeReference(p.Reference) is not { Length: > SalePayment.ReferenceMaxLength }))
            .WithMessage(SaleMessages.ReferenceTooLong)
            .OverridePropertyName(SaleFields.Payments);
    }

    private static string? NormalizeReference(string? reference) =>
        string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();

    private static bool IsWellFormed(PaymentInput payment) => payment.Method == PaymentMethod.Cash
        ? payment.ReceivedCents is > 0 and <= Money.MaxCents
        : payment.AmountCents is > 0 and <= Money.MaxCents && payment.ReceivedCents is null;
}
