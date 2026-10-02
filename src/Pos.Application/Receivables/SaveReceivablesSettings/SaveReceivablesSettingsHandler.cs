using FluentValidation;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Audit;
using Pos.Domain.Users;

namespace Pos.Application.Receivables.SaveReceivablesSettings;

/// <summary>
/// Guarda el plazo de pago (1 a 3650 días); solo <c>ManageCustomerCredit</c>. Se audita. El vencimiento
/// no se guarda por venta, así que el cambio afecta a todas las cuentas (research §9).
/// </summary>
public sealed class SaveReceivablesSettingsHandler
{
    private readonly IAccessControl _access;
    private readonly IReceivablesSettingsStore _store;
    private readonly IAuditLog _audit;
    private readonly IValidator<SaveReceivablesSettingsCommand> _validator;

    public SaveReceivablesSettingsHandler(
        IAccessControl access,
        IReceivablesSettingsStore store,
        IAuditLog audit,
        IValidator<SaveReceivablesSettingsCommand> validator)
    {
        _access = access;
        _store = store;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<ReceivablesSettings>> HandleAsync(SaveReceivablesSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageCustomerCredit, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ReceivablesSettings>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ReceivablesSettings>(ProductRules.ToError(validation));
        }

        var previous = _store.Load();
        var settings = new ReceivablesSettings { PaymentTermDays = command.PaymentTermDays };
        _store.Save(settings);

        // Sin cambio no se registra nada (018, FR-002).
        if (previous.PaymentTermDays != settings.PaymentTermDays)
        {
            _audit.Add(new AuditRecord(
                AuditActions.CreditSettingsChanged,
                AuditActions.ReceivablesSettingsEntity,
                Guid.CreateVersion7(),
                Details: $"Plazo de pago: {previous.PaymentTermDays} -> {settings.PaymentTermDays} días",
                Changes: [new AuditFieldChange("Plazo de pago", Days(previous.PaymentTermDays), Days(settings.PaymentTermDays))],
                AuthorizedBy: access.AuthorizedBy));
            await _audit.SaveAsync(cancellationToken);
        }

        return Result.Success(settings);
    }

    private static string Days(int days) => $"{days} días";
}
