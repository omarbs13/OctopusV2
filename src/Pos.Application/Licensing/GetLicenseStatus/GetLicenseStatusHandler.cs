using Pos.Domain.Licensing;

namespace Pos.Application.Licensing.GetLicenseStatus;

public sealed record LicenseStatusDto(
    LicenseKind Kind,
    int? DaysRemaining,
    bool IsReadOnly,
    LicenseWarning Warning,
    string ContactPhone,
    string ContactEmail,
    InvalidLicenseReason? InvalidReason);

/// <summary>
/// Estado de la licencia para la tarjeta de Inicio, el login y Acerca de (011). No exige permiso:
/// el login lo consulta antes de iniciar sesión y solo expone días y contacto.
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
            status.Kind,
            status.DaysRemaining,
            status.IsReadOnly,
            status.Warning,
            _contact.Phone,
            _contact.Email,
            status.InvalidReason);
    }
}
