using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Discounts;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Registro inmutable de un evento: sin versión, sin modificación ni borrado lógico (plan.md, Complexity Tracking).</summary>
internal sealed class DiscountApprovalConfiguration : IEntityTypeConfiguration<DiscountApproval>
{
    public void Configure(EntityTypeBuilder<DiscountApproval> builder)
    {
        builder.ToTable("DiscountApprovals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.DraftId).IsRequired();
        builder.Property(a => a.RequestedBy).IsRequired();
        builder.Property(a => a.AuthorizedBy).IsRequired();
        builder.Property(a => a.Scope)
            .HasConversion(scope => scope.ToCode(), code => DiscountScopeExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(a => a.ProductId);
        builder.Property(a => a.ApprovedBasisPoints).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => new { a.DraftId, a.RequestedBy }).HasDatabaseName("IX_DiscountApprovals_Draft");
    }
}
