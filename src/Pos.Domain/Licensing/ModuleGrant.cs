namespace Pos.Domain.Licensing;

/// <summary>Entrada de módulo de una licencia ya verificada (025, data-model).</summary>
/// <param name="Module">Solo módulos conocidos; los GUID desconocidos se descartan al leer.</param>
/// <param name="ActivatesOn">Fecha local de activación.</param>
/// <param name="ExpiresOn"><c>null</c> = indefinido. Inclusivo: el módulo funciona todo ese día.</param>
public sealed record ModuleGrant(LicensedModule Module, DateOnly ActivatesOn, DateOnly? ExpiresOn)
{
    /// <summary>Vencimiento anterior a la activación: la entrada nunca está activa y cuenta como vencida.</summary>
    public bool IsInvalid => ExpiresOn is { } expires && expires < ActivatesOn;

    public bool IsActiveOn(DateOnly day) => ActivatesOn <= day && (ExpiresOn is null || day <= ExpiresOn);
}
