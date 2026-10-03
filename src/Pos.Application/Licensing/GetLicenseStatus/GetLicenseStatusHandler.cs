using Pos.Domain.Licensing;

namespace Pos.Application.Licensing.GetLicenseStatus;

/// <param name="Module">Módulo.</param>
/// <param name="Name">Nombre del catálogo compartido.</param>
/// <param name="State">Estado.</param>
/// <param name="ActivatesOn">Activación de la entrada que determina el estado.</param>
/// <param name="ExpiresOn">Vencimiento; nulo = indefinido.</param>
public sealed record ModuleStatusDto(LicensedModule Module, string Name, ModuleState State, DateOnly? ActivatesOn, DateOnly? ExpiresOn);

public sealed record LicenseStatusDto(
    LicenseOverall Overall,
    LicenseBlockReason? BlockReason,
    int TrialDaysRemaining,
    LicenseWarning TrialWarning,
    string? CustomerName,
    IReadOnlyList<ModuleStatusDto> Modules,
    IReadOnlyList<ModuleStatusDto> ExpiringSoon,
    bool ClockBehind,
    DateOnly LastSeen,
    bool StoredLicenseRejected,
    string ContactPhone,
    string ContactEmail,
    string MachineId)
{
    public bool IsBlocked => Overall == LicenseOverall.Blocked;
}

/// <summary>
/// Estado de la licencia para Inicio y "Ayuda > Licencia" (025, FR-040). No exige permiso: cualquier
/// usuario lo ve, también en bloqueo (blocked-mode §1).
/// </summary>
public sealed class GetLicenseStatusHandler
{
    private readonly ILicenseState _state;
    private readonly VendorContact _contact;
    private readonly IMachineIdProvider _machine;
    private readonly IModuleCatalogInfo _catalog;

    public GetLicenseStatusHandler(ILicenseState state, VendorContact contact, IMachineIdProvider machine, IModuleCatalogInfo catalog)
    {
        _state = state;
        _contact = contact;
        _machine = machine;
        _catalog = catalog;
    }

    public LicenseStatusDto Handle()
    {
        var status = _state.Current;
        return new LicenseStatusDto(
            status.Overall,
            status.BlockReason,
            status.TrialDaysRemaining,
            status.TrialWarning,
            status.CustomerName,
            [.. status.Modules.Select(ToDto)],
            [.. status.ExpiringSoon.Select(ToDto)],
            status.ClockBehind,
            status.LastSeen,
            status.StoredLicenseRejected,
            _contact.Phone,
            _contact.Email,
            _machine.GetMachineId());
    }

    private ModuleStatusDto ToDto(ModuleStatus status) =>
        new(status.Module, _catalog.NameOf(status.Module), status.State, status.ActivatesOn, status.ExpiresOn);
}
