using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Application.Abstractions;
using Pos.Domain.Users;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public const string UserNameIndexName = "IX_Users_NormalizedUserName";

    private static readonly DateTime SeedDate = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.FullName).HasMaxLength(User.FullNameMaxLength).IsRequired();
        builder.Property(u => u.UserName).HasMaxLength(UserNameRules.UserNameMaxLength).IsRequired();
        builder.Property(u => u.NormalizedUserName).HasMaxLength(UserNameRules.UserNameMaxLength).IsRequired();
        builder.Property(u => u.Role)
            .HasConversion(role => role.ToCode(), code => UserRoleExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.IsSystem).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(User.PasswordHashMaxLength);
        builder.Property(u => u.MustChangePassword).IsRequired();
        builder.Property(u => u.FailedLoginCount).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.CreatedBy).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();
        builder.Property(u => u.UpdatedBy).IsRequired();
        builder.Property(u => u.Version).IsConcurrencyToken().IsRequired();

        // Único sin distinguir mayúsculas: NormalizedUserName ya viene en mayúsculas invariantes.
        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName(UserNameIndexName).IsUnique();

        // "Sistema" es un dato fijo, igual en toda instalación: conserva el id que ya tienen los
        // registros previos, así que la migración no actualiza datos (007, research §2).
        builder.HasData(new
        {
            Id = SystemUser.Id,
            FullName = SystemUser.DisplayName,
            UserName = SystemUser.DisplayName,
            NormalizedUserName = "SISTEMA",
            Role = UserRole.Admin,
            IsActive = false,
            IsSystem = true,
            MustChangePassword = false,
            FailedLoginCount = 0,
            CreatedAt = SeedDate,
            CreatedBy = SystemUser.Id,
            UpdatedAt = SeedDate,
            UpdatedBy = SystemUser.Id,
            Version = 1,
        });
    }
}
