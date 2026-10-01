using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Discounts;
using Pos.Domain.Users;

namespace Pos.Application.Discounts.Settings.SaveDiscountSettings;

/// <summary>Guarda el límite de descuento (0 a 100 %); solo <c>ManageDiscounts</c>. Registra <c>DISCOUNT_LIMIT_CHANGED</c> (FR-019).</summary>
public sealed partial class SaveDiscountSettingsHandler
{
    private readonly IAccessControl _access;
    private readonly IDiscountSettingsStore _store;
    private readonly IAuditLog _audit;
    private readonly IValidator<SaveDiscountSettingsCommand> _validator;
    private readonly ILogger<SaveDiscountSettingsHandler> _logger;

    public SaveDiscountSettingsHandler(
        IAccessControl access,
        IDiscountSettingsStore store,
        IAuditLog audit,
        IValidator<SaveDiscountSettingsCommand> validator,
        ILogger<SaveDiscountSettingsHandler> logger)
    {
        _access = access;
        _store = store;
        _audit = audit;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<DiscountSettings>> HandleAsync(SaveDiscountSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageDiscounts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<DiscountSettings>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<DiscountSettings>(ProductRules.ToError(validation));
        }

        var previous = _store.Load();
        var settings = new DiscountSettings { LimitBasisPoints = command.LimitBasisPoints };
        _store.Save(settings);
        _audit.Add(
            AuditActions.DiscountLimitChanged,
            AuditActions.DiscountSettingsEntity,
            Guid.CreateVersion7(),
            $"Límite de descuento sin autorización: {DiscountValue.FormatPercent(previous.LimitBasisPoints)} -> {DiscountValue.FormatPercent(settings.LimitBasisPoints)}",
            access.AuthorizedBy);
        await _audit.SaveAsync(cancellationToken);
        LogChanged(previous.LimitBasisPoints, settings.LimitBasisPoints);
        return Result.Success(settings);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Límite de descuento modificado. AnteriorPb={Previous} NuevoPb={Current}")]
    private partial void LogChanged(int previous, int current);
}
