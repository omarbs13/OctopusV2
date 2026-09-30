namespace Pos.Application.Business.GetBusinessProfile;

/// <summary>Datos del negocio vigentes, o nulo si aún no se capturan.</summary>
public sealed class GetBusinessProfileHandler
{
    private readonly IBusinessProfileRepository _profiles;

    public GetBusinessProfileHandler(IBusinessProfileRepository profiles) => _profiles = profiles;

    public async Task<BusinessProfileDto?> HandleAsync(CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(cancellationToken);
        return profile is null ? null : BusinessProfileDto.From(profile);
    }
}
