using FluentValidation;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Audit;
using Pos.Domain.Users;

namespace Pos.Application.Returns.SaveReturnsSettings;

/// <summary>Guarda el plazo de devoluciones (1 a 3650 días); solo <c>ManageCreditNotes</c>. Se audita.</summary>
public sealed class SaveReturnsSettingsHandler
{
    private readonly IAccessControl _access;
    private readonly IReturnsSettingsStore _store;
    private readonly IAuditLog _audit;
    private readonly IValidator<SaveReturnsSettingsCommand> _validator;

    public SaveReturnsSettingsHandler(
        IAccessControl access,
        IReturnsSettingsStore store,
        IAuditLog audit,
        IValidator<SaveReturnsSettingsCommand> validator)
    {
        _access = access;
        _store = store;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<ReturnsSettings>> HandleAsync(SaveReturnsSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageCreditNotes, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ReturnsSettings>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ReturnsSettings>(ProductRules.ToError(validation));
        }

        var previous = _store.Load();
        var settings = new ReturnsSettings { ReturnWindowDays = command.ReturnWindowDays };
        _store.Save(settings);

        // Sin cambio no se registra nada (018, FR-002).
        if (previous.ReturnWindowDays != settings.ReturnWindowDays)
        {
            _audit.Add(new AuditRecord(
                AuditActions.ReturnSettingsChanged,
                AuditActions.ReturnSettingsEntity,
                Guid.CreateVersion7(),
                Details: $"Plazo de devoluciones: {previous.ReturnWindowDays} -> {settings.ReturnWindowDays} días",
                Changes: [new AuditFieldChange("Plazo de devoluciones", Days(previous.ReturnWindowDays), Days(settings.ReturnWindowDays))],
                AuthorizedBy: access.AuthorizedBy));
            await _audit.SaveAsync(cancellationToken);
        }

        return Result.Success(settings);
    }

    private static string Days(int days) => $"{days} días";
}
