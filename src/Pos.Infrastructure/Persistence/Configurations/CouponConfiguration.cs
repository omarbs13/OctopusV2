using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Discounts;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public const string CodeIndexName = "IX_Coupons_Code";

    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupons");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Code).HasMaxLength(Coupon.CodeMaxLength).IsRequired();
        builder.Property(c => c.Mode)
            .HasConversion(mode => mode.ToCode(), code => DiscountModeExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(c => c.Value).IsRequired();
        // Vigencia como fecha local del negocio, no instante UTC (plan, Complexity Tracking).
        builder.Property(c => c.StartsOn).IsRequired();
        builder.Property(c => c.EndsOn).IsRequired();
        builder.Property(c => c.UsageLimit);
        builder.Property(c => c.UsesCount).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
        builder.Property(c => c.UpdatedBy).IsRequired();
        builder.Property(c => c.Version).IsConcurrencyToken().IsRequired();
        builder.Ignore(c => c.Discount);
        builder.Ignore(c => c.RemainingUses);

        // Código único también entre cupones inactivos: el código identifica la promoción.
        builder.HasIndex(c => c.Code).HasDatabaseName(CodeIndexName).IsUnique();
    }
}
