using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public const string FolioIndexName = "IX_Sales_Folio";
    public const string DraftIndexName = "IX_Sales_DraftId";

    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.FolioNumber).IsRequired();
        builder.Property(s => s.DraftId).IsRequired();
        builder.Property(s => s.TotalCents).IsRequired();
        builder.Property(s => s.Status)
            .HasConversion(status => status.ToCode(), code => SaleStatusExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(s => s.CancellationReason).HasMaxLength(Sale.CancellationReasonMaxLength);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.CreatedBy).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Property(s => s.UpdatedBy).IsRequired();
        builder.Property(s => s.Version).IsConcurrencyToken().IsRequired();

        builder.Ignore(s => s.Total);
        builder.Ignore(s => s.Folio);

        // Las ventas nunca se borran: ninguna relación se elimina en cascada.
        builder.HasMany(s => s.Lines)
            .WithOne()
            .HasForeignKey(l => l.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(s => s.Payments)
            .WithOne()
            .HasForeignKey(p => p.SaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.FolioNumber).HasDatabaseName(FolioIndexName).IsUnique();
        builder.HasIndex(s => s.DraftId).HasDatabaseName(DraftIndexName).IsUnique();
        builder.HasIndex(s => new { s.CreatedAt, s.Id }).HasDatabaseName("IX_Sales_CreatedAt");
        builder.HasIndex(s => new { s.Status, s.CreatedAt }).HasDatabaseName("IX_Sales_Status_CreatedAt");

        // Filtro por cajero y propiedad de las ventas (007, research §7).
        builder.HasIndex(s => new { s.CreatedBy, s.CreatedAt }).HasDatabaseName("IX_Sales_CreatedBy_CreatedAt");
    }
}
