using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Licensing;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class LicenseSealConfiguration : IEntityTypeConfiguration<LicenseSealEntity>
{
    public void Configure(EntityTypeBuilder<LicenseSealEntity> builder)
    {
        builder.ToTable("LicenseSeals");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Payload).IsRequired();
    }
}
