using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UsersAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_SaleDrafts",
                table: "SaleDrafts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleDrafts_Slot",
                table: "SaleDrafts");

            migrationBuilder.DropColumn(
                name: "Slot",
                table: "SaleDrafts");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "SaleDrafts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "AuthorizedBy",
                table: "AuditEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SaleDrafts",
                table: "SaleDrafts",
                column: "UserId");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MustChangePassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LockoutEndsAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "DeletedAt", "FailedLoginCount", "FullName", "IsActive", "IsSystem", "LastLoginAt", "LockoutEndsAt", "MustChangePassword", "NormalizedUserName", "PasswordHash", "Role", "UpdatedAt", "UpdatedBy", "UserName", "Version" },
                values: new object[] { new Guid("00000000-0000-7000-8000-000000000001"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-7000-8000-000000000001"), null, 0, "Sistema", false, true, null, null, false, "SISTEMA", null, "ADMIN", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-7000-8000-000000000001"), "Sistema", 1 });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CreatedBy_CreatedAt",
                table: "Sales",
                columns: new[] { "CreatedBy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CreatedAt",
                table: "AuditEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CreatedBy",
                table: "AuditEntries",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUserName",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CreatedBy_CreatedAt",
                table: "Sales");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SaleDrafts",
                table: "SaleDrafts");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_CreatedAt",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_CreatedBy",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SaleDrafts");

            migrationBuilder.DropColumn(
                name: "AuthorizedBy",
                table: "AuditEntries");

            migrationBuilder.AddColumn<int>(
                name: "Slot",
                table: "SaleDrafts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SaleDrafts",
                table: "SaleDrafts",
                column: "Slot");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleDrafts_Slot",
                table: "SaleDrafts",
                sql: "\"Slot\" = 1");
        }
    }
}
