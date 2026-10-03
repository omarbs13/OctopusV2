using System.Text.Json;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;

namespace Pos.Infrastructure.Licensing;

/// <summary>
/// Lee una sola vez el catálogo compartido embebido en el ensamblado (025, FR-005, research §5): versión,
/// nombre y descripción por GUID. Las reglas (GUID, orden, base) viven en <see cref="ModuleCatalog"/>.
/// </summary>
public sealed class EmbeddedModuleCatalogInfo : IModuleCatalogInfo
{
    public const string ResourceName = "Pos.ModuleCatalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Lazy<Catalog> Loaded = new(Load);

    public int CatalogVersion => Loaded.Value.CatalogVersion;

    public string NameOf(LicensedModule module) => Find(module).Name;

    public string DescriptionOf(LicensedModule module) => Find(module).Description;

    /// <summary>Contenido del recurso embebido tal como está en <c>contracts/module-catalog.json</c>.</summary>
    internal static Catalog Read() => Loaded.Value;

    private static CatalogModule Find(LicensedModule module)
    {
        var id = ModuleCatalog.IdOf(module);
        return Loaded.Value.Modules.FirstOrDefault(m => m.Id == id)
            ?? throw new InvalidOperationException($"El catálogo embebido no contiene el módulo {module}.");
    }

    private static Catalog Load()
    {
        using var stream = typeof(EmbeddedModuleCatalogInfo).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Falta el recurso embebido {ResourceName}.");
        return JsonSerializer.Deserialize<Catalog>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"El recurso {ResourceName} está vacío.");
    }

    internal sealed record Catalog(int CatalogVersion, IReadOnlyList<CatalogModule> Modules);

    internal sealed record CatalogModule(Guid Id, string Key, string Name, string Description, int Order, bool IsBase);
}
