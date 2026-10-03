using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.Categories;
using Pos.Application.Discounts;
using Pos.Application.Discounts.GetDiscountReport;
using Pos.Application.Inventory;
using Pos.Application.Reports.GetCashCountReport;
using Pos.Application.Reports.GetInventoryReport;
using Pos.Application.Reports.GetMyShiftSummary;
using Pos.Application.Reports.GetSalesReport;
using Pos.Application.Users.Session;
using Pos.Domain.CashShifts;
using Pos.Domain.Inventory;

namespace Pos.Application.Reports.Export;

/// <summary>
/// Arma el <see cref="ReportDocument"/> de cada reporte a partir de los mismos casos de uso que la pantalla
/// (así se hereda la verificación de permisos), sin paginar. Aquí se omiten efectivo esperado, contado y
/// diferencia de turnos abiertos (FR-017) y se incluyen todos los registros del filtro (FR-018).
/// </summary>
public sealed class ReportDocumentBuilder
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private readonly GetSalesReportHandler _sales;
    private readonly GetCashCountReportHandler _cashCount;
    private readonly GetInventoryReportHandler _inventory;
    private readonly GetMyShiftSummaryHandler _myShift;
    private readonly IBusinessProfileRepository _business;
    private readonly IUserSession _session;
    private readonly IClock _clock;
    private readonly GetDiscountReportHandler? _discounts;
    private readonly ICategoryRepository? _categories;

    public ReportDocumentBuilder(
        GetSalesReportHandler sales,
        GetCashCountReportHandler cashCount,
        GetInventoryReportHandler inventory,
        GetMyShiftSummaryHandler myShift,
        IBusinessProfileRepository business,
        IUserSession session,
        IClock clock,
        GetDiscountReportHandler? discounts = null,
        ICategoryRepository? categories = null)
    {
        _discounts = discounts;
        _categories = categories;
        _sales = sales;
        _cashCount = cashCount;
        _inventory = inventory;
        _myShift = myShift;
        _business = business;
        _session = session;
        _clock = clock;
    }

    /// <summary>Documento listo para escribir, o el error (sin permiso, o "Sin datos en este período").</summary>
    public async Task<Result<BuiltReport>> BuildAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var built = request.Kind switch
        {
            ReportKind.Sales => await BuildSalesAsync(request, cancellationToken),
            ReportKind.CashCount => await BuildCashCountAsync(request, cancellationToken),
            ReportKind.Inventory => await BuildInventoryAsync(request, cancellationToken),
            ReportKind.MyShift => await BuildMyShiftAsync(request, cancellationToken),
            ReportKind.Discounts => await BuildDiscountsAsync(request, cancellationToken),
            _ => Result.Failure<BuiltReport>(new InvalidState("El reporte no se puede exportar.")),
        };
        return built;
    }

    /// <summary>Completa encabezado (negocio), fecha y usuario de un documento.</summary>
    public async Task<ReportDocument> CompleteAsync(PartialDocument partial, CancellationToken cancellationToken)
    {
        var profile = await _business.GetAsync(cancellationToken);
        var business = BusinessHeader.From(profile is null ? null : BusinessProfileDto.From(profile));
        return new ReportDocument(
            partial.Title,
            partial.PeriodText,
            partial.FilterTexts,
            partial.Metrics,
            partial.Tables,
            partial.Charts,
            business,
            _clock.UtcNow,
            _session.User?.FullName ?? string.Empty);
    }

    private async Task<Result<BuiltReport>> BuildSalesAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        var query = (request.Sales ?? throw new ArgumentException("Falta el reporte de ventas.", nameof(request))) with
        {
            Page = 1,
            PageSize = ReportPaging.All,
        };
        var result = await _sales.HandleAsync(query, cancellationToken);
        if (!result.IsSuccess)
        {
            return Result.Failure<BuiltReport>(result.Error);
        }

        var report = result.Value;
        if (report.Totals.SalesCount == 0)
        {
            return Result.Failure<BuiltReport>(NoData());
        }

        var filters = new List<string>();
        if (query.CashierId is not null)
        {
            filters.Add($"{ReportTexts.Cashier}: {(report.Rows.Count > 0 ? report.Rows[0].CashierName : string.Empty)}");
        }

        if (query.Compare)
        {
            filters.Add($"{ReportTexts.CompareFilter}: {ReportTexts.Yes}");
        }

        if (await CategoryFilterTextAsync(query.Category, cancellationToken) is { } categoryFilter)
        {
            filters.Add(categoryFilter);
        }

        filters.Add($"{ReportTexts.SortFilter}: {SalesSortText(query.Sort)} ({(query.Descending ? ReportTexts.SortDesc : ReportTexts.SortAsc)})");

        // Con filtro de categoría las formas de pago no se desglosan: un pago cubre la venta completa (016, research §11).
        var totals = report.Totals;
        ReportCell Payment(long cents) => totals.PaymentsBreakdownAvailable ? new MoneyCell(cents) : new TextCell(ReportTexts.NotSplitByCategory);
        if (!totals.PaymentsBreakdownAvailable)
        {
            filters.Add(ReportTexts.PaymentsNotSplitNote);
        }

        List<ReportMetric> metrics =
        [
            new(ReportTexts.TotalSold, new MoneyCell(totals.TotalCents)),
            new(ReportTexts.SalesCount, new CountCell(totals.SalesCount)),
            new(ReportTexts.AverageTicket, new MoneyCell(totals.AverageTicketCents)),
            new(ReportTexts.Cash, Payment(totals.CashCents)),
            new(ReportTexts.Card, Payment(totals.CardCents)),
            new(ReportTexts.Transfer, Payment(totals.TransferCents)),
            new(ReportTexts.OnAccount, Payment(totals.OnAccountCents)),
            new(ReportTexts.TotalDiscounted, new MoneyCell(totals.DiscountCents)),
        ];
        if (report.Comparison is { } comparison)
        {
            metrics.Add(new(ReportTexts.PreviousTotal, new MoneyCell(comparison.Previous.TotalCents)));
            metrics.Add(new(
                ReportTexts.Variation,
                comparison.VariationBasisPoints is { } bp ? new PercentCell(bp) : new TextCell(ReportTexts.NotCalculable)));
        }

        var table = new ReportTable(
            ReportTexts.SalesTableTitle,
            [
                new(ReportTexts.ColFolio, ReportColumnType.Text),
                new(ReportTexts.ColDate, ReportColumnType.Date),
                new(ReportTexts.ColCashier, ReportColumnType.Text),
                new(ReportTexts.ColTotal, ReportColumnType.Money),
            ],
            [.. report.Rows.Select(r => (IReadOnlyList<ReportCell>)[new TextCell(r.FolioText), new DateCell(r.CreatedAtUtc), new TextCell(r.CashierName), new MoneyCell(r.TotalCents)])]);
        var chart = new ChartSpec(
            ChartKind.Line,
            ReportTexts.SalesChartTitle,
            [.. report.Days.Select(d => new ChartPoint(d.LocalDate.ToString("dd/MM", Culture), d.TotalCents))],
            ChartValueFormat.Money);

        var period = PeriodText(query.Period.FromDate, query.Period.ToDate);
        var document = await CompleteAsync(
            new PartialDocument(ReportTexts.SalesTitle, period, filters, metrics, [CategoriesTable(report), table], [chart]),
            cancellationToken);
        return Result.Success(new BuiltReport(
            document,
            $"ventas_{query.Period.FromDate:yyyy-MM-dd}_{query.Period.ToDate:yyyy-MM-dd}",
            $"Reporte: Ventas; Período: {period}; {string.Join("; ", filters)}"));
    }

    private async Task<Result<BuiltReport>> BuildCashCountAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        var query = request.CashCount ?? throw new ArgumentException("Falta el reporte de arqueo.", nameof(request));
        var result = await _cashCount.HandleAsync(query, cancellationToken);
        if (!result.IsSuccess)
        {
            return Result.Failure<BuiltReport>(result.Error);
        }

        var report = result.Value;
        if (report.Rows.Count == 0)
        {
            return Result.Failure<BuiltReport>(NoData());
        }

        var filters = new List<string>();
        if (query.CashierId is not null)
        {
            filters.Add($"{ReportTexts.Cashier}: {report.Rows[0].CashierName}");
        }

        filters.Add($"{ReportTexts.ThresholdFilter}: {(report.ThresholdBasisPoints / 100m).ToString("0.##", Culture)} %");

        List<ReportMetric> metrics =
        [
            new(ReportTexts.ClosedShifts, new CountCell(report.Totals.ClosedShifts)),
            new(ReportTexts.TotalSold, new MoneyCell(report.Totals.TotalSoldCents)),
            new(ReportTexts.AccumulatedDifference, new MoneyCell(report.Totals.AccumulatedDifferenceCents)),
            new(ReportTexts.ShiftsInAlert, new CountCell(report.AlertCount)),
        ];

        // Los turnos abiertos van "En curso" sin efectivo esperado, contado ni diferencia (FR-017).
        var rows = report.Rows.Select(r => (IReadOnlyList<ReportCell>)
        [
            new TextCell(r.FolioText),
            new TextCell(r.CashierName),
            new DateCell(r.OpenedAtUtc),
            r.IsOpen || r.ClosedAtUtc is null ? new TextCell(ReportTexts.InProgress) : new DateCell(r.ClosedAtUtc.Value),
            new MoneyCell(r.OpeningFloatCents),
            new MoneyCell(r.TotalSoldCents),
            new MoneyCell(r.DepositsCents),
            new MoneyCell(r.WithdrawalsCents),
            r.IsOpen || r.ExpectedCashCents is null ? EmptyCell.Instance : new MoneyCell(r.ExpectedCashCents.Value),
            r.IsOpen || r.CountedCashCents is null ? EmptyCell.Instance : new MoneyCell(r.CountedCashCents.Value),
            r.IsOpen || r.DifferenceCents is null ? EmptyCell.Instance : new MoneyCell(r.DifferenceCents.Value),
            r.IsOpen ? EmptyCell.Instance : r.DifferenceBasisPoints is { } bp ? new PercentCell(bp) : new TextCell(ReportTexts.NotCalculable),
        ]).ToList();

        var table = new ReportTable(
            ReportTexts.CashCountTableTitle,
            [
                new(ReportTexts.ColShift, ReportColumnType.Text),
                new(ReportTexts.ColCashier, ReportColumnType.Text),
                new(ReportTexts.ColOpened, ReportColumnType.Date),
                new(ReportTexts.ColClosed, ReportColumnType.Date),
                new(ReportTexts.ColFloat, ReportColumnType.Money),
                new(ReportTexts.ColSold, ReportColumnType.Money),
                new(ReportTexts.ColDeposits, ReportColumnType.Money),
                new(ReportTexts.ColWithdrawals, ReportColumnType.Money),
                new(ReportTexts.ColExpected, ReportColumnType.Money),
                new(ReportTexts.ColCounted, ReportColumnType.Money),
                new(ReportTexts.ColDifference, ReportColumnType.Money),
                new(ReportTexts.ColPercent, ReportColumnType.Percent),
            ],
            rows);

        List<ChartSpec> charts = [];
        var closed = report.Rows.Where(r => !r.IsOpen && r.DifferenceCents is not null).ToList();
        if (closed.Count > 0)
        {
            charts.Add(new ChartSpec(
                ChartKind.Bars,
                ReportTexts.CashCountChartTitle,
                [.. closed.Select(r => new ChartPoint(r.FolioText, r.DifferenceCents!.Value))],
                ChartValueFormat.Money));
        }

        var period = PeriodText(query.Period.FromDate, query.Period.ToDate);
        var document = await CompleteAsync(
            new PartialDocument(ReportTexts.CashCountTitle, period, filters, metrics, [table], charts),
            cancellationToken);
        return Result.Success(new BuiltReport(
            document,
            $"arqueo_{query.Period.FromDate:yyyy-MM-dd}_{query.Period.ToDate:yyyy-MM-dd}",
            $"Reporte: Arqueo; Período: {period}; {string.Join("; ", filters)}"));
    }

    private async Task<Result<BuiltReport>> BuildInventoryAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        var query = (request.Inventory ?? throw new ArgumentException("Falta el reporte de inventario.", nameof(request))) with
        {
            Page = 1,
            PageSize = ReportPaging.All,
        };
        var result = await _inventory.HandleAsync(query, cancellationToken);
        if (!result.IsSuccess)
        {
            return Result.Failure<BuiltReport>(result.Error);
        }

        var report = result.Value;
        if (report.Counts.Total == 0)
        {
            return Result.Failure<BuiltReport>(NoData());
        }

        var filters = new List<string>();
        if (query.Filter != StockFilter.All)
        {
            filters.Add($"{ReportTexts.StatusFilter}: {StatusFilterText(query.Filter)}");
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            filters.Add($"{ReportTexts.SearchFilter}: {query.SearchText}");
        }

        if (await CategoryFilterTextAsync(query.Category, cancellationToken) is { } categoryFilter)
        {
            filters.Add(categoryFilter);
        }

        filters.Add(ReportTexts.InventoryNote);

        var counts = report.Counts;
        List<ReportMetric> metrics =
        [
            new(ReportTexts.TotalProducts, new CountCell(counts.Total)),
            new(ReportTexts.ActiveProducts, new CountCell(counts.Active)),
            new(ReportTexts.LowStock, new CountCell(counts.Low)),
            new(ReportTexts.OutOfStock, new CountCell(counts.Out)),
        ];

        var table = new ReportTable(
            ReportTexts.InventoryTableTitle,
            [
                new(ReportTexts.ColName, ReportColumnType.Text),
                new(ReportTexts.ColSku, ReportColumnType.Text),
                new(ReportTexts.ColCategory, ReportColumnType.Text),
                new(ReportTexts.ColOnHand, ReportColumnType.Quantity),
                new(ReportTexts.ColMinimum, ReportColumnType.Quantity),
                new(ReportTexts.ColReorderPoint, ReportColumnType.Quantity),
                new(ReportTexts.ColUnit, ReportColumnType.Text),
                new(ReportTexts.ColStatus, ReportColumnType.Text),
            ],
            [.. report.Rows.Select(r => (IReadOnlyList<ReportCell>)
            [
                new TextCell(r.Name),
                new TextCell(r.Sku),
                new TextCell(CategoryMessages.Display(r.CategoryName, r.CategoryIsActive)),
                new QuantityCell(r.OnHandThousandths, r.DecimalPlaces),
                r.MinimumThousandths is { } minimum ? new QuantityCell(minimum, r.DecimalPlaces) : EmptyCell.Instance,
                r.ReorderPointThousandths is { } reorder ? new QuantityCell(reorder, r.DecimalPlaces) : EmptyCell.Instance,
                new TextCell(r.UnitName),
                new TextCell(StatusText(r.Status)),
            ])]);
        var chart = new ChartSpec(
            ChartKind.Pie,
            ReportTexts.InventoryChartTitle,
            [
                new ChartPoint(ReportTexts.NormalStock, counts.Normal, "#2E7D32"),
                new ChartPoint(ReportTexts.StatusLow, counts.Low, "#EF6C00"),
                new ChartPoint(ReportTexts.OutOfStock, counts.Out, "#C62828"),
            ],
            ChartValueFormat.Count);

        var period = $"Al cierre del {query.AsOfDate.ToString("dd/MM/yyyy", Culture)}";
        var document = await CompleteAsync(
            new PartialDocument(ReportTexts.InventoryTitle, period, filters, metrics, [table], [chart]),
            cancellationToken);
        return Result.Success(new BuiltReport(
            document,
            $"inventario_{query.AsOfDate:yyyy-MM-dd}",
            $"Reporte: Inventario; Período: {period}; {string.Join("; ", filters)}"));
    }

    private async Task<Result<BuiltReport>> BuildDiscountsAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        if (_discounts is null)
        {
            return Result.Failure<BuiltReport>(new InvalidState("El reporte no se puede exportar."));
        }

        var query = (request.Discounts ?? throw new ArgumentException("Falta el reporte de descuentos.", nameof(request))) with
        {
            Page = 1,
            PageSize = ReportPaging.All,
        };
        var result = await _discounts.HandleAsync(query, cancellationToken);
        if (!result.IsSuccess)
        {
            return Result.Failure<BuiltReport>(result.Error);
        }

        var report = result.Value;
        if (report.Count == 0)
        {
            return Result.Failure<BuiltReport>(NoData());
        }

        var filters = new List<string>();
        if (query.CashierId is not null)
        {
            filters.Add($"{ReportTexts.Cashier}: {report.Rows[0].CashierName}");
        }

        if (query.Kind is { } kind)
        {
            filters.Add($"{ReportTexts.KindFilter}: {DiscountMessages.KindText(kind)}");
        }

        List<ReportMetric> metrics =
        [
            new(ReportTexts.TotalDiscounted, new MoneyCell(report.TotalDiscountCents)),
            new(ReportTexts.DiscountsCount, new CountCell(report.Count)),
        ];
        var table = new ReportTable(
            ReportTexts.DiscountsTableTitle,
            [
                new(ReportTexts.ColFolio, ReportColumnType.Text),
                new(ReportTexts.ColDate, ReportColumnType.Date),
                new(ReportTexts.ColCashier, ReportColumnType.Text),
                new(ReportTexts.ColKind, ReportColumnType.Text),
                new(ReportTexts.ColDiscountValue, ReportColumnType.Text),
                new(ReportTexts.ColAmount, ReportColumnType.Money),
                new(ReportTexts.ColAuthorizedBy, ReportColumnType.Text),
                new(ReportTexts.ColCoupon, ReportColumnType.Text),
            ],
            [.. report.Rows.Select(r => (IReadOnlyList<ReportCell>)
            [
                new TextCell(r.Folio),
                new DateCell(r.CreatedAtUtc),
                new TextCell(r.CashierName),
                new TextCell(DiscountMessages.KindText(r.Kind)),
                new TextCell(Pos.Domain.Discounts.DiscountValue.Create(r.Mode, r.Value).ToString()),
                new MoneyCell(r.AmountCents),
                r.AuthorizedByName is { } authorizer ? new TextCell(authorizer) : EmptyCell.Instance,
                r.CouponCode is { } coupon ? new TextCell(coupon) : EmptyCell.Instance,
            ])]);

        var period = PeriodText(query.Period.FromDate, query.Period.ToDate);
        var document = await CompleteAsync(
            new PartialDocument(ReportTexts.DiscountsTitle, period, filters, metrics, [table], []),
            cancellationToken);
        return Result.Success(new BuiltReport(
            document,
            $"descuentos_{query.Period.FromDate:yyyy-MM-dd}_{query.Period.ToDate:yyyy-MM-dd}",
            $"Reporte: Descuentos; Período: {period}; {string.Join("; ", filters)}"));
    }

    private async Task<Result<BuiltReport>> BuildMyShiftAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        var result = await _myShift.HandleAsync(request.ShiftId, cancellationToken);
        if (!result.IsSuccess)
        {
            return Result.Failure<BuiltReport>(result.Error);
        }

        var shift = result.Value;
        List<ReportMetric> metrics =
        [
            new(ReportTexts.MyShiftOpeningFloat, new MoneyCell(shift.OpeningFloatCents)),
            new(ReportTexts.MyShiftSales, new CountCell(shift.SalesCount)),
            new(ReportTexts.MyShiftSalesTotal, new MoneyCell(shift.TotalSoldCents)),
            new(ReportTexts.MyShiftDeposits, new MoneyCell(shift.DepositsCents)),
            new(ReportTexts.MyShiftWithdrawals, new MoneyCell(shift.WithdrawalsCents)),
        ];

        // Con el turno abierto no se incluye efectivo esperado, contado ni diferencia (FR-017, FR-020).
        if (!shift.IsOpen && shift.ExpectedCashCents is { } expected && shift.CountedCashCents is { } counted && shift.DifferenceCents is { } difference)
        {
            metrics.Add(new(ReportTexts.MyShiftExpected, new MoneyCell(expected)));
            metrics.Add(new(ReportTexts.MyShiftCounted, new MoneyCell(counted)));
            metrics.Add(new(ReportTexts.MyShiftDifference, new MoneyCell(difference)));
        }

        var movements = new ReportTable(
            ReportTexts.MyShiftMovements,
            [
                new(ReportTexts.ColMovementDate, ReportColumnType.Date),
                new(ReportTexts.ColMovementType, ReportColumnType.Text),
                new(ReportTexts.ColAmount, ReportColumnType.Money),
                new(ReportTexts.ColReason, ReportColumnType.Text),
            ],
            [.. shift.Movements.Select(m => (IReadOnlyList<ReportCell>)
            [
                new DateCell(m.CreatedAtUtc),
                new TextCell(m.Type == CashMovementType.In ? ReportTexts.MovementIn : ReportTexts.MovementOut),
                new MoneyCell(m.AmountCents),
                new TextCell(m.Reason),
            ])]);

        var period = $"{shift.FolioText} · {ReportCellText(shift.OpenedAtUtc)}"
            + (shift.ClosedAtUtc is { } closed ? $" - {ReportCellText(closed)}" : $" - {ReportTexts.InProgress}");
        var document = await CompleteAsync(
            new PartialDocument(ReportTexts.MyShiftTitle, period, [], metrics, shift.Movements.Count > 0 ? [movements] : [], []),
            cancellationToken);
        return Result.Success(new BuiltReport(document, $"turno_{shift.FolioText}", $"Reporte: Mi turno; Turno: {shift.FolioText}"));
    }

    private static string ReportCellText(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", Culture);

    private static InvalidState NoData() => new(ReportMessages.NoData);

    /// <summary>
    /// "Ventas por categoría" (016, FR-021): una fila por categoría seguida de sus productos con sangría y la
    /// fila de total; mismas cifras que la pantalla porque salen del mismo <see cref="SalesReport"/>.
    /// </summary>
    private static ReportTable CategoriesTable(SalesReport report)
    {
        var categories = report.Categories;
        const int UnitsDecimals = 3;
        var rows = new List<IReadOnlyList<ReportCell>>();
        foreach (var category in categories)
        {
            rows.Add(
            [
                new TextCell(CategoryMessages.Display(category.CategoryId is null ? null : category.Name, category.IsActive)),
                new QuantityCell(category.UnitsThousandths, UnitsDecimals),
                new MoneyCell(category.AmountCents),
                new PercentCell(category.ShareBasisPoints),
            ]);
            foreach (var product in category.Products)
            {
                rows.Add(
                [
                    new TextCell($"    {product.Name} ({product.UnitName})"),
                    new QuantityCell(product.UnitsThousandths, product.DecimalPlaces),
                    new MoneyCell(product.AmountCents),
                    EmptyCell.Instance,
                ]);
            }
        }

        rows.Add(
        [
            new TextCell(ReportTexts.CategoriesTotal),
            new QuantityCell(report.CategoriesUnitsThousandths, UnitsDecimals),
            new MoneyCell(report.CategoriesAmountCents),
            EmptyCell.Instance,
        ]);

        return new ReportTable(
            ReportTexts.CategoriesTableTitle,
            [
                new(ReportTexts.ColCategory, ReportColumnType.Text),
                new(ReportTexts.ColUnits, ReportColumnType.Quantity),
                new(ReportTexts.ColAmount, ReportColumnType.Money),
                new(ReportTexts.ColShare, ReportColumnType.Percent),
            ],
            rows);
    }

    /// <summary>"Categoría: {nombre}" o "Categoría: Sin categoría"; nulo con "Todas" (016, contracts/ui.md).</summary>
    private async Task<string?> CategoryFilterTextAsync(CategoryFilter filter, CancellationToken cancellationToken)
    {
        switch (filter.Kind)
        {
            case CategoryFilterKind.Uncategorized:
                return $"{ReportTexts.CategoryFilter}: {CategoryMessages.Uncategorized}";
            case CategoryFilterKind.Only when filter.CategoryId is { } id:
                var category = _categories is null ? null : await _categories.GetAsync(id, cancellationToken);
                return $"{ReportTexts.CategoryFilter}: {(category is null ? string.Empty : CategoryMessages.Display(category.Name, category.IsActive))}";
            default:
                return null;
        }
    }

    private static string PeriodText(DateOnly from, DateOnly to) =>
        from == to
            ? from.ToString("dd/MM/yyyy", Culture)
            : $"{from.ToString("dd/MM/yyyy", Culture)} - {to.ToString("dd/MM/yyyy", Culture)}";

    private static string SalesSortText(SalesReportSort sort) => sort switch
    {
        SalesReportSort.Folio => ReportTexts.ColFolio,
        SalesReportSort.Cashier => ReportTexts.ColCashier,
        SalesReportSort.Total => ReportTexts.ColTotal,
        _ => ReportTexts.ColDate,
    };

    private static string StatusFilterText(StockFilter filter) => filter switch
    {
        StockFilter.Normal => ReportTexts.NormalStock,
        StockFilter.Low => ReportTexts.LowStock,
        StockFilter.Alert => ReportTexts.AlertLevel,
        StockFilter.Urgent => ReportTexts.UrgentLevel,
        _ => ReportTexts.OutOfStock,
    };

    private static string StatusText(StockStatus status) => status switch
    {
        StockStatus.Low => ReportTexts.StatusLow,
        StockStatus.Out => ReportTexts.OutOfStock,
        _ => ReportTexts.NormalStock,
    };
}

/// <summary>Contenido de un documento sin encabezado ni pie; <see cref="ReportDocumentBuilder.CompleteAsync"/> los agrega.</summary>
public sealed record PartialDocument(
    string Title,
    string PeriodText,
    IReadOnlyList<string> FilterTexts,
    IReadOnlyList<ReportMetric> Metrics,
    IReadOnlyList<ReportTable> Tables,
    IReadOnlyList<ChartSpec> Charts);

/// <summary>Documento y datos para nombrar el archivo y auditar la exportación.</summary>
public sealed record BuiltReport(ReportDocument Document, string FileBaseName, string AuditDetails);
