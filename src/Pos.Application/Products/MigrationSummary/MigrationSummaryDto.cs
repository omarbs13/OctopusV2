namespace Pos.Application.Products.MigrationSummary;

public sealed record MigrationSummaryDto(int Migrated, int WithoutModel, int WithoutPackaging, int Duplicates);
