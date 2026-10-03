using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>Datos del catálogo compartido que no son reglas: versión, nombre y descripción (025, research §5).</summary>
public interface IModuleCatalogInfo
{
    int CatalogVersion { get; }

    string NameOf(LicensedModule module);

    string DescriptionOf(LicensedModule module);
}
