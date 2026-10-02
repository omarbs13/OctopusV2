using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Domain.Suppliers;
using Pos.Domain.Users;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Suppliers;

/// <summary>020, Historia 1: integridad del catálogo de proveedores sobre SQLite real con permisos reales.</summary>
public sealed class SupplierUseCaseTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private SupplierTestSupport _suppliers = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _suppliers = new SupplierTestSupport(_db, await ShiftTestSupport.CreateAsync(_db));
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RucDuplicado_SeRechazaConElNombreDelExistente_TambienSiEstaInactivo()
    {
        var norte = await _suppliers.CreateOkAsync("Distribuidora del Norte", "abc123 ");
        Assert.True((await _suppliers.SetActiveAsync(norte, active: false)).IsSuccess);

        var duplicate = await _suppliers.CreateAsync("Otro", "ABC123");

        Assert.Equal(new SupplierTaxIdInUse(norte, "Distribuidora del Norte"), duplicate.Error);
        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.Suppliers.CountAsync(Ct));
        Assert.True((await _suppliers.CreateAsync("Sin RUC 1")).IsSuccess);
        Assert.True((await _suppliers.CreateAsync("Sin RUC 2", "  ")).IsSuccess);
    }

    [Fact]
    public async Task RucDuplicado_AlEditarSeRechazaSinCambios()
    {
        var norte = await _suppliers.CreateOkAsync("Norte", "RUC-1");
        var sur = await _suppliers.CreateOkAsync("Sur", "RUC-2");

        var result = await _suppliers.UpdateAsync(sur, "Sur", "ruc-1");

        Assert.Equal(new SupplierTaxIdInUse(norte, "Norte"), result.Error);
        Assert.Equal("RUC-2", (await _suppliers.LoadAsync(sur)).TaxId);
        Assert.True((await _suppliers.UpdateAsync(sur, "Sur", "ruc-2")).IsSuccess);
    }

    [Fact]
    public async Task Buscar_PorNombreORuc_ExcluyeInactivosSalvoQueSePidan()
    {
        var norte = await _suppliers.CreateOkAsync("Distribuidora del Norte", "ABC123");
        await _suppliers.CreateOkAsync("Abarrotes Sur");
        Assert.True((await _suppliers.SetActiveAsync(norte, active: false)).IsSuccess);

        Assert.Empty((await _suppliers.SearchAsync("norte")).Value.Items);
        Assert.Equal([norte], (await _suppliers.SearchAsync("norte", includeInactive: true)).Value.Items.Select(i => i.Id));
        Assert.Equal([norte], (await _suppliers.SearchAsync("abc123", includeInactive: true)).Value.Items.Select(i => i.Id));
        Assert.Single((await _suppliers.SearchAsync(null)).Value.Items);
    }

    [Fact]
    public async Task Editar_AuditaSoloLosCamposCambiados_YNadaSiNoCambio()
    {
        var norte = await _suppliers.CreateOkAsync("Norte");

        Assert.True((await _suppliers.UpdateAsync(norte, "Norte", phone: "555-1234", terms: PaymentTerms.Credit, creditDays: "30")).IsSuccess);
        Assert.True((await _suppliers.UpdateAsync(norte, "Norte", phone: "555-1234", terms: PaymentTerms.Credit, creditDays: "30")).IsSuccess);

        await using var check = _db.CreateDbContext();
        var updates = await check.AuditEntries.AsNoTracking().Where(e => e.Action == AuditActions.SupplierUpdated).ToListAsync(Ct);
        var entry = Assert.Single(updates);
        Assert.Equal(["Teléfono", "Condiciones de pago"], entry.Changes.Select(c => c.Field));
        Assert.Equal("Crédito a 30 días", entry.Changes[1].After);
        Assert.Equal(2, (await _suppliers.LoadAsync(norte)).Version);
    }

    [Fact]
    public async Task Credito_SinDias_EsErrorDeCampo()
    {
        var result = await _suppliers.CreateAsync("Norte", terms: PaymentTerms.Credit, creditDays: "0");

        var failed = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Contains(failed.Errors, e => e.Field == "CreditDays");
    }

    [Fact]
    public async Task Editar_ConVersionVieja_EsConflicto()
    {
        var norte = await _suppliers.CreateOkAsync("Norte");
        Assert.True((await _suppliers.UpdateAsync(norte, "Norte 2")).IsSuccess);

        var result = await _suppliers.UpdateAsync(norte, "Norte 3", expectedVersion: 1);

        Assert.IsType<Conflict>(result.Error);
    }

    [Fact]
    public async Task Cajero_RecibeForbidden()
    {
        _suppliers.Users.As(_suppliers.Users.Cashier);

        var result = await _suppliers.CreateAsync("Norte");

        Assert.Equal(new Forbidden(Permission.ManageSuppliers, CanBeAuthorized: false), result.Error);
    }
}
