using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Suppliers;
using Pos.Application.Suppliers.CreateSupplier;
using Pos.Application.Suppliers.SearchSuppliers;
using Pos.Application.Suppliers.SetSupplierActive;
using Pos.Application.Suppliers.UpdateSupplier;
using Pos.Domain.Suppliers;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Suppliers;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>
/// Arma los casos de uso de proveedores (020) sobre SQLite real con usuarios y permisos reales; un ámbito
/// (contexto) por operación como la composición.
/// </summary>
public sealed class SupplierTestSupport
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TestDb _db;

    public SupplierTestSupport(TestDb db, ShiftTestSupport users)
    {
        _db = db;
        Users = users;
    }

    public ShiftTestSupport Users { get; }

    public async Task<Result<Guid>> CreateAsync(
        string name,
        string? taxId = null,
        PaymentTerms terms = PaymentTerms.Cash,
        string? creditDays = null,
        string? phone = null)
    {
        await using var context = _db.CreateDbContext();
        return await new CreateSupplierHandler(
                Users.Access(context),
                new SupplierRepository(context),
                new AuditLog(context),
                new WriteTransactions(context),
                new CreateSupplierValidator(),
                NullLogger<CreateSupplierHandler>.Instance)
            .HandleAsync(new CreateSupplierCommand(name, taxId, phone, null, null, terms, creditDays), Ct);
    }

    /// <summary>Proveedor de contado que debe crearse.</summary>
    public async Task<Guid> CreateOkAsync(string name, string? taxId = null)
    {
        var result = await CreateAsync(name, taxId);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    public async Task<Result> UpdateAsync(
        Guid id,
        string name,
        string? taxId = null,
        string? phone = null,
        PaymentTerms terms = PaymentTerms.Cash,
        string? creditDays = null,
        int? expectedVersion = null)
    {
        var version = expectedVersion ?? (await LoadAsync(id)).Version;
        await using var context = _db.CreateDbContext();
        return await new UpdateSupplierHandler(
                Users.Access(context),
                new SupplierRepository(context),
                new AuditLog(context),
                new WriteTransactions(context),
                new UpdateSupplierValidator(),
                NullLogger<UpdateSupplierHandler>.Instance)
            .HandleAsync(new UpdateSupplierCommand(id, version, name, taxId, phone, null, null, terms, creditDays), Ct);
    }

    public async Task<Result> SetActiveAsync(Guid id, bool active)
    {
        var version = (await LoadAsync(id)).Version;
        await using var context = _db.CreateDbContext();
        return await new SetSupplierActiveHandler(
                Users.Access(context),
                new SupplierRepository(context),
                new AuditLog(context),
                new WriteTransactions(context),
                NullLogger<SetSupplierActiveHandler>.Instance)
            .HandleAsync(new SetSupplierActiveCommand(id, version, active), Ct);
    }

    public async Task<Result<SupplierPage>> SearchAsync(string? text, bool includeInactive = false)
    {
        await using var context = _db.CreateDbContext();
        return await new SearchSuppliersHandler(Users.Access(context), new SupplierRepository(context))
            .HandleAsync(new SearchSuppliersQuery(text, includeInactive), Ct);
    }

    public async Task<Supplier> LoadAsync(Guid id)
    {
        await using var context = _db.CreateDbContext();
        return await context.Suppliers.AsNoTracking().SingleAsync(s => s.Id == id, Ct);
    }

}
