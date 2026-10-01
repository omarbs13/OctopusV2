using Serilog.Core;
using Serilog.Events;

namespace Pos.Desktop.Diagnostics;

/// <summary>
/// Agrega a cada entrada el usuario, la pantalla y la venta en curso (FR-010, FR-011). Nunca lanza:
/// una falla del contexto no debe afectar la operación (FR-014).
/// </summary>
internal sealed class DiagnosticContextEnricher : ILogEventEnricher
{
    private readonly DiagnosticContext _context;

    public DiagnosticContextEnricher(DiagnosticContext context) => _context = context;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        try
        {
            var snapshot = _context.Current;
            Add(logEvent, propertyFactory, "UserId", snapshot.UserId);
            Add(logEvent, propertyFactory, "UserName", snapshot.UserName);
            Add(logEvent, propertyFactory, "Screen", snapshot.Screen);
            Add(logEvent, propertyFactory, "SaleDraftId", snapshot.SaleDraftId);
            Add(logEvent, propertyFactory, "SaleLines", snapshot.SaleLines);
        }
#pragma warning disable CA1031 // El contexto es opcional: si falla, la entrada se escribe sin él.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private static void Add(LogEvent logEvent, ILogEventPropertyFactory factory, string name, object? value)
    {
        if (value is not null)
        {
            logEvent.AddPropertyIfAbsent(factory.CreateProperty(name, value));
        }
    }
}
