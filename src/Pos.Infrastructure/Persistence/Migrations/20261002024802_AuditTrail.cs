using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_CreatedAt",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_CreatedBy",
                table: "AuditEntries");

            migrationBuilder.AddColumn<string>(
                name: "Changes",
                table: "AuditEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityName",
                table: "AuditEntries",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "AuditEntries",
                type: "TEXT",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Action_CreatedAt",
                table: "AuditEntries",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_AuthorizedBy",
                table: "AuditEntries",
                column: "AuthorizedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CreatedAt",
                table: "AuditEntries",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CreatedBy",
                table: "AuditEntries",
                columns: new[] { "CreatedBy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_CreatedAt",
                table: "AuditEntries",
                columns: new[] { "EntityType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_Action_CreatedAt",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_AuthorizedBy",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_CreatedAt",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_CreatedBy",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_EntityType_CreatedAt",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "Changes",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "EntityName",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "AuditEntries");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CreatedAt",
                table: "AuditEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CreatedBy",
                table: "AuditEntries",
                column: "CreatedBy");
        }
    }
}
