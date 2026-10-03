using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.GetCurrentShift;
using Pos.Application.Licensing;
using Pos.Application.Sales;
using Pos.Application.Sales.GetSaleDraft;
using Pos.Desktop.CashShifts;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Licensing;
using Pos.Desktop.Sales;
using Pos.Domain.Licensing;

namespace Pos.Desktop.Navigation;

/// <summary>Resultado de la regla de licencia sobre una opción del menú.</summary>
public enum LicenseMenuAccess
{
    Allowed,

    /// <summary>Algún módulo que exige la opción no está activo (012, FR-012).</summary>
    ModuleInactive,

    /// <summary>El sistema está bloqueado y la opción no está entre las permitidas (025, FR-028).</summary>
    SystemBlocked,
}

/// <summary>
/// Qué opciones del menú se ven y se pueden abrir según la licencia (025, contracts/blocked-mode.md §2): en bloqueo,
/// solo Inicio y "Ayuda > Licencia", más "Punto de venta" mientras haya un borrador con líneas y el cierre de caja
/// mientras haya un turno abierto. El cierre de caja del turno abierto se muestra sin la regla de módulo, con o sin
/// bloqueo (FR-030a). Las reglas de negocio las vuelve a aplicar cada caso de uso; esto solo decide el menú.
/// </summary>
public sealed class LicenseMenuPolicy
{
    private readonly ILicenseState? _license;
    private readonly UseCases? _useCases;

    public LicenseMenuPolicy(ILicenseState? license = null, UseCases? useCases = null)
    {
        _license = license;
        _useCases = useCases;
    }

    /// <summary>El usuario tiene una venta en curso con líneas (borrador guardado).</summary>
    public bool HasDraftWithLines { get; private set; }

    /// <summary>Hay un turno abierto en la caja.</summary>
    public bool HasOpenShift { get; private set; }

    public bool IsBlocked => _license?.Current.IsBlocked == true;

    public LicenseMenuAccess Evaluate(NavigationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Id is HomeModule.PageId or LicenseModule.PageId)
        {
            return LicenseMenuAccess.Allowed;
        }

        // Cerrar el turno abierto nunca depende de la licencia (Principio I).
        if (entry.Id == CashModule.ClosingPageId && HasOpenShift)
        {
            return LicenseMenuAccess.Allowed;
        }

        if (IsBlocked)
        {
            return entry.Id == SalesModule.PointOfSalePageId && HasDraftWithLines
                ? LicenseMenuAccess.Allowed
                : LicenseMenuAccess.SystemBlocked;
        }

        return entry.Permission is { } required && _license is not null
            && ModuleAccess.FirstInactive(required, _license.IsModuleActive) is not null
            ? LicenseMenuAccess.ModuleInactive
            : LicenseMenuAccess.Allowed;
    }

    /// <summary>
    /// Vuelve a consultar si hay venta en curso y turno abierto. Devuelve verdadero si algo cambió. Nunca lanza:
    /// ante una falla conserva lo último que sabía.
    /// </summary>
    public async Task<bool> RefreshAsync()
    {
        if (_useCases is null)
        {
            return false;
        }

        var (draft, shift) = (HasDraftWithLines, HasOpenShift);
        try
        {
            var stored = await _useCases.RunAsync<GetSaleDraftHandler, Result<RecoveredDraft?>>(h => h.HandleAsync(CancellationToken.None));
            HasDraftWithLines = stored is { IsSuccess: true, Value.Lines.Count: > 0 };
            var current = await _useCases.RunAsync<GetCurrentShiftHandler, Result<CurrentShiftSummary?>>(h => h.HandleAsync(CancellationToken.None));
            HasOpenShift = current is { IsSuccess: true, Value: not null };
        }
#pragma warning disable CA1031 // El menú es de apoyo: una falla nunca debe afectar la operación.
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }

        return draft != HasDraftWithLines || shift != HasOpenShift;
    }
}
