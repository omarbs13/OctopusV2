using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Licensing;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class InstalledLicenseConfiguration : IEntityTypeConfiguration<InstalledLicenseEntity>
{
    public void Configure(EntityTypeBuilder<InstalledLicenseEntity> builder)
    {
        builder.ToTable("InstalledLicenses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.ImportedAtUtc).IsRequired();
    }
}
