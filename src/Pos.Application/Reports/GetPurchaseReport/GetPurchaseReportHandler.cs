using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Reports.GetPurchaseReport;

/// <summary>
/// "Reportes > Compras" (<c>ViewPurchaseReport</c>): compras de 100 en 100, de la más reciente a la más antigua,
/// con los acumulados de todo el filtro. Un filtro inválido es un error por campo y no consulta nada (FR-023).
/// </summary>
public sealed class GetPurchaseReportHandler
{
    private readonly IAccessControl _access;
    private readonly IPurchaseReportReader _reader;

    public GetPurchaseReportHandler(IAccessControl access, IPurchaseReportReader reader)
    {
        _access = access;
        _reader = reader;
    }

    public async Task<Result<PurchaseReportPage>> HandleAsync(GetPurchaseReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ViewPurchaseReport, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<PurchaseReportPage>(access.Error!);
        }

        var errors = new List<FieldError>();
        if (query.FromDate is { } from && query.ToDate is { } to && from > to)
        {
            errors.Add(new FieldError(ReportFields.ToDate, ReportMessages.EndBeforeStart));
        }

        var min = ParseAmount(query.MinTotalText, ReportFields.MinTotal, errors);
        var max = ParseAmount(query.MaxTotalText, ReportFields.MaxTotal, errors);
        if (min is { } low && max is { } high && low > high)
        {
            errors.Add(new FieldError(ReportFields.MaxTotal, ReportMessages.MinAboveMax));
        }

        if (errors.Count > 0)
        {
            return Result.Failure<PurchaseReportPage>(new ValidationFailed(errors));
        }

        var filter = new PurchaseReportFilter(query.SupplierId, query.FromDate, query.ToDate, min, max, query.IncludeVoided, Math.Max(query.Page, 1));
        return Result.Success(await _reader.SearchAsync(filter, cancellationToken));
    }

    /// <summary>Importe opcional ≥ 0; vacío = sin límite. Un negativo o mal escrito es error del campo.</summary>
    private static long? ParseAmount(string? text, string field, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parsed = Money.Parse(text);
        if (parsed.Value is { } money)
        {
            return money.Cents;
        }

        errors.Add(new FieldError(field, ReportMessages.AmountInvalid));
        return null;
    }
}
