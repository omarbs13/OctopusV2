using Pos.Domain.Licensing;

namespace Pos.Application.Licensing.GetLicenseStatus;

public sealed record LicenseStatusDto(
    LicensePhase Phase,
    int DaysRemaining,
    LicenseWarning Warning,
    IReadOnlyList<LicensedModule> ActiveModules,
    string ContactPhone,
    string ContactEmail);

/// <summary>
/// Estado de la licencia para la tarjeta de Inicio y Acerca de (012). No exige permiso y no accede a disco.
/// </summary>
public sealed class GetLicenseStatusHandler
{
    private readonly ILicenseState _state;
    private readonly VendorContact _contact;

    public GetLicenseStatusHandler(ILicenseState state, VendorContact contact)
    {
        _state = state;
        _contact = contact;
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
            _contact.Email);
    }
}
