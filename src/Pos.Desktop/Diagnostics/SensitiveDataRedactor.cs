using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Pos.Desktop.Diagnostics;

/// <summary>
/// Sustituye por <c>***</c> el valor de las propiedades sensibles (contraseñas, tarjetas, tokens) antes
/// de escribir la entrada (FR-012). Debe ejecutarse después de los demás enriquecedores.
/// </summary>
internal sealed partial class SensitiveDataRedactor : ILogEventEnricher
{
    public const string Mask = "***";

    // "card" no debe coincidir con "Discard" (por ejemplo DiscardSaleDraft); "pin" solo como palabra propia.
    [GeneratedRegex(@"password|passwd|contrase|(?<!dis)card|tarjeta|token|secret|cvv|(?<![a-z])pin(?![a-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveName();

    public static bool IsSensitive(string name) => SensitiveName().IsMatch(name);

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            if (IsSensitive(name))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Mask)));
                continue;
            }

            var redacted = Redact(value);
            if (!ReferenceEquals(redacted, value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, redacted));
            }
        }
    }

    private static LogEventPropertyValue Redact(LogEventPropertyValue value)
    {
        switch (value)
        {
            case StructureValue structure:
                var changed = false;
                var properties = new List<LogEventProperty>();
                foreach (var property in structure.Properties)
                {
                    var next = IsSensitive(property.Name) ? new ScalarValue(Mask) : Redact(property.Value);
                    changed |= !ReferenceEquals(next, property.Value);
                    properties.Add(new LogEventProperty(property.Name, next));
                }

                return changed ? new StructureValue(properties, structure.TypeTag) : value;

            case DictionaryValue dictionary:
                var dictionaryChanged = false;
                var items = new List<KeyValuePair<ScalarValue, LogEventPropertyValue>>();
                foreach (var (key, item) in dictionary.Elements)
                {
                    var next = key.Value is string name && IsSensitive(name) ? new ScalarValue(Mask) : Redact(item);
                    dictionaryChanged |= !ReferenceEquals(next, item);
                    items.Add(new KeyValuePair<ScalarValue, LogEventPropertyValue>(key, next));
                }

                return dictionaryChanged ? new DictionaryValue(items) : value;

            default:
                return value;
        }
    }
}
