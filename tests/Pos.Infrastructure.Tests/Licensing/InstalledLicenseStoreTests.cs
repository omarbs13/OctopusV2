using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>025, FR-020: la licencia importada se guarda exactamente como llegó, en una sola fila.</summary>
public sealed class InstalledLicenseStoreTests : IAsyncLifetime
{
    private static readonly DateTime Imported = new(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);

    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SinLicencia_DevuelveNulo() =>
        Assert.Null(await new InstalledLicenseStore(_db).ReadAsync(Ct));

    [Fact]
    public async Task Reemplazar_GuardaElTextoExacto_YDejaUnaSolaFila()
    {
        var store = new InstalledLicenseStore(_db);
        const string first = "{\"format\":3,\"payload\":\"ñ é\",\"signature\":\"x\"}\n";
        const string second = "  {\"format\": 3, \"payload\": \"otra\", \"signature\": \"y\"}\r\n";

        await store.ReplaceAsync(first, Imported, Ct);
        Assert.Equal(first, await store.ReadAsync(Ct));

        await store.ReplaceAsync(second, Imported.AddDays(1), Ct);

        Assert.Equal(second, await store.ReadAsync(Ct));
        await using var context = _db.CreateDbContext();
        var row = await context.InstalledLicenses.SingleAsync(Ct);
        Assert.Equal(Imported.AddDays(1), row.ImportedAtUtc);
    }
}
