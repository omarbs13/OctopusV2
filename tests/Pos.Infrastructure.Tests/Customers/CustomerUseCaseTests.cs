using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Customers;
using Pos.Domain.Customers;
using Pos.Domain.Users;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Customers;

/// <summary>014, Historia 1: integridad del catálogo de clientes sobre SQLite real con permisos reales.</summary>
public sealed class CustomerUseCaseTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private CreditTestSupport _credit = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _credit = new CreditTestSupport(_db, await ShiftTestSupport.CreateAsync(_db));
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RucDuplicado_SeRechazaTambienContraUnInactivoYConOtrasMayusculasOEspacios()
    {
        var first = await _credit.CreateAsync("Ana", taxId: "abc-123");
        Assert.True(first.IsSuccess, first.Error?.ToString());
        Assert.True((await _credit.SetActiveAsync(first.Value, active: false)).IsSuccess);

        var duplicate = await _credit.CreateAsync("Beto", taxId: "  ABC-123 ");

        Assert.Equal(new Duplicate(CustomerFields.TaxId), duplicate.Error);
        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.Customers.CountAsync(Ct));
        Assert.True((await _credit.CreateAsync("Carla", taxId: null)).IsSuccess);
        Assert.True((await _credit.CreateAsync("Dora", taxId: "  ")).IsSuccess);
    }

    [Fact]
    public async Task RucDuplicado_AlEditarSeRechazaSinCambios()
    {
        await _credit.CreateAsync("Ana", taxId: "RUC-1");
        var beto = (await _credit.CreateAsync("Beto", taxId: "RUC-2")).Value;

        var result = await _credit.UpdateAsync(beto, "Beto", taxId: "ruc-1");

        Assert.Equal(new Duplicate(CustomerFields.TaxId), result.Error);
        Assert.Equal("RUC-2", (await _credit.LoadAsync(beto)).TaxId);
    }

    [Fact]
    public async Task Desactivar_ConSaldoPendiente_SeRechazaSinCambios()
    {
        var ana = await _credit.CreditCustomerAsync();
        await _credit.InsertPendingReceivableAsync(ana, 20_000);
        var before = await _credit.LoadAsync(ana);

        var result = await _credit.SetActiveAsync(ana, active: false);

        Assert.Equal(new CustomerHasBalance(20_000), result.Error);
        var after = await _credit.LoadAsync(ana);
        Assert.True(after.IsActive);
        Assert.Equal(before.Version, after.Version);
        await using var check = _db.CreateDbContext();
        Assert.False(await check.AuditEntries.AnyAsync(e => e.Action == AuditActions.CustomerDeactivated, Ct));
    }

    [Fact]
    public async Task Cajero_QueEnviaLimiteYCredito_CreaClienteSoloEfectivoConLimiteCero()
    {
        _credit.Users.As(_credit.Users.Cashier);

        var result = await _credit.CreateAsync("Ana", limitCents: 500_000, mode: CreditMode.Credit);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var ana = await _credit.LoadAsync(result.Value);
        Assert.Equal((CreditMode.CashOnly, 0L), (ana.CreditMode, ana.CreditLimitCents));
        await using var check = _db.CreateDbContext();
        Assert.True(await check.AuditEntries.AnyAsync(e => e.Action == AuditActions.CustomerCreated && e.EntityId == ana.Id, Ct));
    }

    [Fact]
    public async Task Cajero_QueCambiaLimiteOModalidad_RecibeForbidden_YElAdministradorPuedeYSeAudita()
    {
        _credit.Users.As(_credit.Users.Cashier);
        var ana = (await _credit.CreateAsync("Ana")).Value;

        var forbidden = await _credit.UpdateAsync(ana, limitCents: 100_000, mode: CreditMode.Credit);
        Assert.Equal(new Forbidden(Permission.ManageCustomerCredit, CanBeAuthorized: false), forbidden.Error);

        // Editar solo los datos de contacto sí lo puede hacer el Cajero.
        Assert.True((await _credit.UpdateAsync(ana, "Ana María")).IsSuccess);

        _credit.Users.As(_credit.Users.Admin);
        Assert.True((await _credit.UpdateAsync(ana, "Ana María", limitCents: 100_000, mode: CreditMode.Credit)).IsSuccess);
        var updated = await _credit.LoadAsync(ana);
        Assert.Equal((CreditMode.Credit, 100_000L, "Ana María"), (updated.CreditMode, updated.CreditLimitCents, updated.Name));
        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.AuditEntries.CountAsync(e => e.Action == AuditActions.CustomerCreditChanged, Ct));
    }

    [Fact]
    public async Task Editar_ConVersionVieja_EsConflicto()
    {
        var ana = (await _credit.CreateAsync("Ana")).Value;
        Assert.True((await _credit.UpdateAsync(ana, "Ana 2")).IsSuccess);

        var result = await _credit.UpdateAsync(ana, "Ana 3", expectedVersion: 1);

        Assert.IsType<Conflict>(result.Error);
    }
}
