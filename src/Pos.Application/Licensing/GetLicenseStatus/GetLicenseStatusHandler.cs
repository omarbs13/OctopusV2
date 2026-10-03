using Pos.Domain.Licensing;

namespace Pos.Application.Licensing.GetLicenseStatus;

public sealed record LicenseStatusDto(
    LicensePhase Phase,
    int DaysRemaining,
    LicenseWarning Warning,
    IReadOnlyList<LicensedModule> ActiveModules,
    string ContactPhone,
    string ContactEmail,
    string MachineId);

/// <summary>
/// Estado de la licencia para la tarjeta de Inicio y Acerca de (012). No exige permiso. El ID de
/// máquina se muestra en Acerca de para soporte (023, FR-030); el proveedor lo calcula una sola vez.
/// </summary>
public sealed class GetLicenseStatusHandler
{
    private readonly ILicenseState _state;
    private readonly VendorContact _contact;
    private readonly IMachineIdProvider _machine;

    public GetLicenseStatusHandler(ILicenseState state, VendorContact contact, IMachineIdProvider machine)
    {
        _state = state;
        _contact = contact;
        _machine = machine;
    }

    public LicenseStatusDto Handle()
    {
        var status = _state.Current;
        return new LicenseStatusDto(
            status.Phase,
            status.DaysRemaining,
            status.Warning,
            _state.EnabledModules.ToArray(),
            _contact.Phone,
            _contact.Email,
            _machine.GetMachineId());
    }
}
