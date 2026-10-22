# Contratos de casos de uso (Application)

- **CreateProduct / UpdateProduct**: entrada con `Model`, `Measures`, `Brand`, `SupplierId`, `PackagingText`
  (texto: se acepta "NA"), `PiecesPerBox`, `PricePerSqmCents`, `PricePerBoxCents`, `PiecePriceCents`,
  `Sku` (opcional: vacío genera uno único), más los campos actuales. Errores por campo: modelo obligatorio,
  embalaje inválido, falta de precio de loseta, duplicado (`DuplicateField = "Identity"` con el producto
  existente). Resultado incluye `Warnings` (diferencia > 2 %).
- **SuggestTilePrice** (función pura de dominio, `ProductPricing`): `(PricePerSqm, Packaging) → PricePerBox` y
  `(PricePerBox, Packaging) → PricePerSqm`, redondeo a centavo, media hacia arriba.
- **SearchProducts**: `Text`, `Brand`, `Measures`, `NeedsReviewOnly` además de lo existente.
- **GetProductFilters**: marcas y medidas distintas de productos no borrados.
- **FindProductsForSale / CartProduct**: devuelve los tres precios y el embalaje; la selección de modalidad
  la hace el dominio (`Product.PriceFor`, `Product.AvailableModes`).
- **GetMigrationSummary / MarkMigrationSummaryShown**.
- **Reporte de productos**: 5 columnas nuevas.
