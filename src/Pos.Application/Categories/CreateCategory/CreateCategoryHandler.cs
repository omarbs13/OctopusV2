using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Categories;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Categories.CreateCategory;

/// <summary>
/// Alta de categoría: permiso <c>ManageProducts</c> → validación → unicidad del nombre sin mayúsculas ni
/// acentos (FR-003) → alta y bitácora en una transacción. El índice único cubre la carrera.
/// </summary>
public sealed partial class CreateCategoryHandler
{
    private readonly IAccessControl _access;
    private readonly ICategoryRepository _categories;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<CreateCategoryCommand> _validator;
    private readonly ILogger<CreateCategoryHandler> _logger;

    public CreateCategoryHandler(
        IAccessControl access,
        ICategoryRepository categories,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<CreateCategoryCommand> validator,
        ILogger<CreateCategoryHandler> logger)
    {
        _access = access;
        _categories = categories;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<Guid>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var nameKey = TextNormalizer.ForSearch(Category.NormalizeName(command.Name));
        if (await _categories.NameExistsAsync(nameKey, null, cancellationToken))
        {
            LogDuplicate(nameKey);
            return Result.Failure<Guid>(new Duplicate(CategoryFields.Name));
        }

        var category = Category.Create(command.Name, command.Description);
        _categories.Add(category);
        _audit.Add(new AuditRecord(
            AuditActions.CategoryCreated,
            AuditActions.CategoryEntity,
            category.Id,
            EntityName: category.Name,
            Details: CategoryRules.Describe(category),
            Changes: AuditChanges.Created(CategoryAuditFields.Snapshot(category))));

        var outcome = await _categories.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            LogDuplicate(nameKey);
            return Result.Failure<Guid>(outcome.Status == SaveStatus.Duplicate ? new Duplicate(CategoryFields.Name) : new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogCreated(category.Id);
        return Result.Success(category.Id);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Categoría creada. CategoryId={CategoryId}")]
    private partial void LogCreated(Guid categoryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Alta de categoría rechazada por nombre repetido. NameKey={NameKey}")]
    private partial void LogDuplicate(string nameKey);
}
