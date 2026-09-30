using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Business;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Datos del negocio; son de la instalación, por eso no hay <c>HasData</c>.</summary>
internal sealed class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> builder)
    {
        builder.ToTable("BusinessProfile");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.TradeName).HasMaxLength(BusinessProfile.TradeNameMaxLength).IsRequired();
        builder.Property(p => p.Address).HasMaxLength(BusinessProfile.AddressMaxLength).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(BusinessProfile.PhoneMaxLength).IsRequired();
        builder.Property(p => p.TaxId).HasMaxLength(BusinessProfile.TaxIdMaxLength);
        builder.Property(p => p.Logo);
        builder.Property(p => p.FooterMessage).HasMaxLength(BusinessProfile.FooterMaxLength);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
        builder.Property(p => p.UpdatedBy).IsRequired();
        builder.Property(p => p.Version).IsConcurrencyToken().IsRequired();
    }
}
