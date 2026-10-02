using System.Diagnostics;
using Pos.Application.Audit;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Audit;

/// <summary>
/// 018, SC-002: con 1,000,000 de entradas, la primera página de cualquier búsqueda responde en menos de 1 s
/// (meta interna: 300 ms). Es explícita: no corre en la suite normal; se ejecuta antes de publicar
/// (quickstart §6).
/// </summary>
public sealed class AuditLogPerformanceTests
{
    private const int Entries = 1_000_000;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Target = TimeSpan.FromMilliseconds(300);
    private static readonly Guid Admin = Guid.Parse("01900000-0000-7000-8000-00000000000a");
    private static readonly Guid Cashier = Guid.Parse("01900000-0000-7000-8000-00000000000c");
    private static readonly Guid Product = Guid.Parse("01900000-0000-7000-8000-0000000000f1");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact(Explicit = true)]
    public async Task PrimeraPagina_ConCualquierFiltro_EnMenosDeUnSegundo()
    {
        using var db = await TestDb.CreateAsync();
        Seed(db);

        // Las entradas cubren del 01/10/2025 en adelante, una cada 30 s (unos 347 días).
        var lastWeek = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var none = new AuditFilter(null, null, null, null, null, null);
        var searches = new (string Name, AuditFilter Filter)[]
        {
            ("Sin filtros", none),
            ("Fechas", none with { FromUtc = lastWeek, ToUtcExclusive = lastWeek.AddDays(7) }),
            ("Usuario", none with { UserId = Admin }),
            ("Evento", none with { Action = AuditActions.ProductUpdated }),
            ("Entidad", none with { Entity = AuditEntityGroup.Session }),
            ("Historial", none with { Record = new AuditRecordRef(AuditActions.ProductEntity, Product) }),
            ("Todos", new AuditFilter(lastWeek, lastWeek.AddDays(7), Cashier, AuditActions.SaleCancelled, AuditEntityGroup.Sale, null)),
        };

        // Calentamiento: la primera consulta del proceso construye el modelo de EF y abre la conexión.
        await using (var warmup = db.CreateDbContext())
        {
            await new AuditLogReader(warmup).SearchAsync(new AuditSearch(none with { Action = AuditActions.Logout }, 1, 1), Ct);
        }

        var slow = new List<string>();
        var failures = new List<string>();
        foreach (var (name, filter) in searches)
        {
            await using var context = db.CreateDbContext();
            var reader = new AuditLogReader(context);
            var watch = Stopwatch.StartNew();
            var page = await reader.SearchAsync(new AuditSearch(filter, 1, AuditPage.DefaultPageSize), Ct);
            var elapsed = watch.Elapsed;

            TestContext.Current.TestOutputHelper?.WriteLine($"{name}: {elapsed.TotalMilliseconds:N0} ms, {page.TotalCount:N0} entradas");
            if (elapsed > Target)
            {
                slow.Add($"{name} ({elapsed.TotalMilliseconds:N0} ms)");
            }

            if (elapsed >= Limit)
            {
                failures.Add($"{name} tardó {elapsed.TotalMilliseconds:N0} ms");
            }

            Assert.NotEmpty(page.Items);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(slow.Count == 0 ? "Todas debajo de 300 ms." : $"Arriba de 300 ms: {string.Join(", ", slow)}");
        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    /// <summary>
    /// Siembra con SQL (EF tardaría minutos): mezcla de eventos, dos autores, un autorizador cada 50 entradas,
    /// un producto con historial cada 1,000 y cambios de campo en la mitad.
    /// </summary>
    private static void Seed(TestDb db)
    {
        static string Text(Guid id) => id.ToString().ToUpperInvariant();
        DatabaseTestHelpers.Execute(db.Directory.Paths.DatabaseFile, $"""
            WITH RECURSIVE n(i) AS (SELECT 0 UNION ALL SELECT i + 1 FROM n WHERE i < {Entries - 1})
            INSERT INTO AuditEntries (Id, Action, EntityType, EntityId, EntityName, Details, Reason, Changes, CreatedAt, CreatedBy, AuthorizedBy)
            SELECT
                upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6))),
                CASE i % 5 WHEN 0 THEN 'PRODUCT_UPDATED' WHEN 1 THEN 'LOGIN_SUCCEEDED' WHEN 2 THEN 'SALE_CANCELLED' WHEN 3 THEN 'CUSTOMER_UPDATED' ELSE 'USER_UPDATED' END,
                CASE i % 5 WHEN 0 THEN 'Product' WHEN 1 THEN 'User' WHEN 2 THEN 'Sale' WHEN 3 THEN 'Customer' ELSE 'User' END,
                CASE WHEN i % 1000 = 0 THEN '{Text(Product)}'
                     ELSE upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6))) END,
                'Registro ' || i,
                'detalle ' || i,
                CASE WHEN i % 5 = 2 THEN 'Error de captura' END,
                CASE WHEN i % 2 = 0 THEN '[{"{"}"Field":"Precio","Before":"$1.00","After":"$2.00"{"}"}]' END,
                datetime('2025-10-01', '+' || (i * 30) || ' seconds'),
                CASE WHEN i % 3 = 0 THEN '{Text(Admin)}' ELSE '{Text(Cashier)}' END,
                CASE WHEN i % 50 = 0 THEN '{Text(Admin)}' END
            FROM n;
            ANALYZE;
            """);
    }
}
