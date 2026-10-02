using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Categories;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public const string NameKeyIndexName = "IX_Categories_NameKey";

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(c => c.NameKey).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(Category.DescriptionMaxLength);
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
        builder.Property(c => c.UpdatedBy).IsRequired();
        builder.Property(c => c.Version).IsConcurrencyToken().IsRequired();
        builder.Ignore(c => c.IsDeleted);

        // Único sin mayúsculas ni acentos, también entre inactivas; una borrada libera el nombre (FR-003).
        builder.HasIndex(c => c.NameKey)
            .HasDatabaseName(NameKeyIndexName)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(c => new { c.IsActive, c.NameKey })
            .HasDatabaseName("IX_Categories_IsActive_NameKey")
            .HasFilter("\"DeletedAt\" IS NULL");
    }
}
