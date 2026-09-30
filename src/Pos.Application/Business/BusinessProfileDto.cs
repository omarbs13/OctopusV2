using Pos.Domain.Business;

namespace Pos.Application.Business;

public sealed record BusinessProfileDto(
    string TradeName,
    string Address,
    string Phone,
    string? TaxId,
    string? FooterMessage,
    byte[]? Logo)
{
    public static BusinessProfileDto From(BusinessProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new BusinessProfileDto(
            profile.TradeName,
            profile.Address,
            profile.Phone,
            profile.TaxId,
            profile.FooterMessage,
            profile.Logo);
    }
}
