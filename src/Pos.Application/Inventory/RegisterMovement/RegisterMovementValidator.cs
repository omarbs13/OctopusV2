using FluentValidation;
using Pos.Domain.Inventory;

namespace Pos.Application.Inventory.RegisterMovement;

/// <summary>Forma de la entrada; las reglas del negocio las aplica el manejador con la existencia cargada.</summary>
public sealed class RegisterMovementValidator : AbstractValidator<RegisterMovementCommand>
{
    public RegisterMovementValidator()
    {
        RuleFor(c => c.Type)
            .IsInEnum().WithMessage(InventoryMessages.TypeInvalid)
            .OverridePropertyName(InventoryFields.Type);

        RuleFor(c => c.QuantityText)
            .NotEmpty().WithMessage(InventoryMessages.QuantityRequired)
            .OverridePropertyName(InventoryFields.Quantity);

        RuleFor(c => InventoryMovement.NormalizeText(c.Reason))
            .Must(r => r is null || r.Length <= InventoryMovement.ReasonMaxLength)
            .WithMessage(InventoryMessages.ReasonTooLong)
            .OverridePropertyName(InventoryFields.Reason);

        RuleFor(c => InventoryMovement.NormalizeText(c.Reference))
            .Must(r => r is null || r.Length <= InventoryMovement.ReferenceMaxLength)
            .WithMessage(InventoryMessages.ReferenceTooLong)
            .OverridePropertyName(InventoryFields.Reference);
    }
}
