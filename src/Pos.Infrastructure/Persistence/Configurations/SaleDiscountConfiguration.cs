using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Discounts;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Parte inmutable de la venta, como <c>SaleLines</c>: sin versión ni borrado lógico (plan.md, Complexity Tracking).</summary>
internal sealed class SaleDiscountConfiguration : IEntityTypeConfiguration<SaleDiscount>
{
    public void Configure(EntityTypeBuilder<SaleDiscount> builder)
    {
        builder.ToTable("SaleDiscounts");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        // Referencia de consulta a la línea, sin llave foránea: la línea siempre es de la misma venta.
        builder.Property(d => d.SaleLineId);
        builder.Property(d => d.Kind)
            .HasConversion(kind => kind.ToCode(), code => DiscountKindExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(d => d.Mode)
            .HasConversion(mode => mode.ToCode(), code => DiscountModeExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(d => d.Value).IsRequired();
        builder.Property(d => d.AmountCents).IsRequired();
        builder.Property(d => d.CouponId);
        builder.Property(d => d.CouponCode).HasMaxLength(Coupon.CodeMaxLength);
        builder.Property(d => d.AppliedBy).IsRequired();
        builder.Property(d => d.AuthorizedBy);
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Ignore(d => d.Discount);

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(d => d.CouponId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.SaleId).HasDatabaseName("IX_SaleDiscounts_Sale");
        builder.HasIndex(d => new { d.CreatedAt, d.Id }).HasDatabaseName("IX_SaleDiscounts_CreatedAt");
        builder.HasIndex(d => d.CouponId).HasDatabaseName("IX_SaleDiscounts_Coupon");
    }
}
