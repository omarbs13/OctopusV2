using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Products;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Catálogo fijo de unidades de medida, sembrado con HasData (constitución, Principio IV).</summary>
internal sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("UnitsOfMeasure");
        builder.HasKey(u => u.Code);
        builder.Property(u => u.Code).HasMaxLength(UnitOfMeasure.CodeMaxLength).ValueGeneratedNever();
        builder.Property(u => u.Name).HasMaxLength(UnitOfMeasure.NameMaxLength).IsRequired();
        builder.Property(u => u.SortOrder).IsRequired();
        builder.Property(u => u.DecimalPlaces).IsRequired().HasDefaultValue(0);
        builder.HasData(UnitOfMeasure.All);
    }
}
