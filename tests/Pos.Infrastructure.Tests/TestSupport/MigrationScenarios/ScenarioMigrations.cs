using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pos.Infrastructure.Tests.TestSupport.MigrationScenarios;

/// <summary>
/// Esquema mínimo de Products compatible con el modelo actual (para sembrar con EF Core). Cuando el
/// modelo agrega columnas obligatorias, se agregan aquí con un valor por defecto.
/// </summary>
internal static class ScenarioSql
{
    public const string CreateProducts = """
        CREATE TABLE "Products" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY,
            "Name" TEXT NOT NULL,
            "NameSearch" TEXT NOT NULL,
            "Sku" TEXT NOT NULL,
            "Barcode" TEXT NULL,
            "PriceCents" INTEGER NOT NULL,
            "UnitCode" TEXT NOT NULL DEFAULT 'H87',
            "IsActive" INTEGER NOT NULL,
            "TracksInventory" INTEGER NOT NULL DEFAULT 0,
            "MinimumStock" INTEGER NULL,
            "IsCritical" INTEGER NOT NULL DEFAULT 0,
            "CategoryId" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "CreatedBy" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL,
            "UpdatedBy" TEXT NOT NULL,
            "DeletedAt" TEXT NULL,
            "Version" INTEGER NOT NULL
        );
        CREATE UNIQUE INDEX "IX_Products_Barcode" ON "Products" ("Barcode") WHERE "DeletedAt" IS NULL AND "Barcode" IS NOT NULL;
        CREATE INDEX "IX_Products_NameSearch" ON "Products" ("NameSearch") WHERE "DeletedAt" IS NULL;
        CREATE UNIQUE INDEX "IX_Products_Sku" ON "Products" ("Sku") WHERE "DeletedAt" IS NULL;
        """;
}

[DbContext(typeof(ScenarioDbContext))]
[Migration("00000000000001_S1_Initial")]
public sealed class S1Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(ScenarioSql.CreateProducts);
}

[DbContext(typeof(ScenarioDbContext))]
[Migration("00000000000002_S2_AddColumn")]
public sealed class S2AddColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "Notes", table: "Products", type: "TEXT", nullable: true);
}

[DbContext(typeof(FailingScenarioDbContext))]
[Migration("00000000000001_S1_Initial")]
public sealed class FailingS1Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(ScenarioSql.CreateProducts);
}

[DbContext(typeof(FailingScenarioDbContext))]
[Migration("00000000000002_S2_Failing")]
public sealed class S2Failing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Notes", table: "Products", type: "TEXT", nullable: true);
        migrationBuilder.Sql("SELECT RAISE(ABORT, 'fallo simulado');");
    }
}
