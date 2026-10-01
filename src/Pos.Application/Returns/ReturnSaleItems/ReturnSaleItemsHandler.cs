using FluentValidation;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Domain.Returns;

namespace Pos.Application.Returns.ReturnSaleItems;

/// <summary>Devolución parcial por líneas y cantidades (Historia 2); delega en <see cref="SaleReturnProcessor"/>.</summary>
public sealed class ReturnSaleItemsHandler
{
    private readonly SaleReturnProcessor _processor;
    private readonly IValidator<ReturnSaleItemsCommand> _validator;

    public ReturnSaleItemsHandler(SaleReturnProcessor processor, IValidator<ReturnSaleItemsCommand> validator)
    {
        _processor = processor;
        _validator = validator;
    }

    public async Task<Result<ReturnResult>> HandleAsync(ReturnSaleItemsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ReturnResult>(ProductRules.ToError(validation));
        }

        return await _processor.ProcessAsync(
            new ReturnRequest(
                command.SaleId,
                command.ExpectedVersion,
                ReturnKind.Partial,
                command.Lines,
                command.Reason,
                command.Compensation,
                command.AuthorizationGrantId),
            cancellationToken);
    }
}
