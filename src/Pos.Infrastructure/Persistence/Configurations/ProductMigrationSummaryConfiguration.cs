using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Products;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class ProductMigrationSummaryConfiguration : IEntityTypeConfiguration<ProductMigrationSummary>
{
    public void Configure(EntityTypeBuilder<ProductMigrationSummary> builder)
    {
        builder.ToTable("ProductMigrationSummaries");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.MigratedCount).IsRequired();
        builder.Property(s => s.WithoutModelCount).IsRequired();
        builder.Property(s => s.WithoutPackagingCount).IsRequired();
        builder.Property(s => s.DuplicateCount).IsRequired();
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.Shown).IsRequired();
    }
}
