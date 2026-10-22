---

description: "Tareas de la funcionalidad 026: campos de proveedor de loseta en el producto"
---

# Tasks: Campos de proveedor de loseta en el producto

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Pruebas**: solo las que exige la constitución (Principio VI): cálculos con dinero, validaciones de integridad, migración y consistencia de inventario. Sin pruebas de ViewModels ni de interfaz.

**Formato**: `- [ ] T### [P] [US#] Descripción con ruta`. `[P]` = se puede hacer en paralelo (archivos distintos, sin dependencia pendiente).

**Comandos**: compilar `dotnet build -v q`; probar `dotnet test <proyecto de pruebas> --verbosity quiet` (solo el proyecto modificado).

**Historias**: US1 migración sin pérdida (P1) · US2 captura y edición (P1) · US3 sugerencia de precio (P2) · US4 unicidad (P1) · US5 ver, buscar y filtrar (P2) · US6 venta con precio correcto (P1) · US7 reportes (P3).

## Phase 1: Setup

- [X] T001 Confirmar línea base: ejecutar `dotnet build -v q` y `dotnet test --verbosity quiet` desde la raíz y anotar cualquier falla previa en `specs/026-tile-supplier-fields/research.md` (sección nueva "Línea base").
- [X] T002 Revisar `TextNormalizer.ForSearch` en `src/Pos.Domain/Common/` y confirmar que quita acentos y pasa a minúsculas; documentar en `research.md` §3 cómo se reutiliza.

---

## Phase 2: Foundational (bloquea todas las historias)

**⚠️ CRÍTICO**: ninguna historia empieza hasta terminar esta fase.

- [X] T003 [P] Crear `src/Pos.Domain/Products/ProductText.cs` con `NormalizeModel`, `NormalizeMeasures` (sin espacios; `X`, `x` y `*` equivalen a `X`; sin tocar la "x" dentro de números como "1.22") y `BuildIdentityKey(Guid? supplierId, string model, string? measures)` = `{SupplierId:N|"-"}|{ModelKey}|{MeasuresKey}`. Mayúsculas, sin acentos, espacios múltiples reducidos; el valor original no se modifica.
- [X] T004 [P] Agregar a `src/Pos.Domain/Products/UnitOfMeasure.cs` la unidad `new("MTK", "Metro cuadrado", 9, 3)` y cambiar `XBX` "Caja" de 0 a 3 decimales (research §5 y §6); agregar `IsTile(string code)` (XBX o MTK).
- [X] T005 [P] Crear `src/Pos.Domain/Sales/SaleMode.cs` con `Piece`, `Box`, `SquareMeter` y su unidad (`H87`, `XBX`, `MTK`).
- [X] T006 Ampliar `src/Pos.Domain/Products/Product.cs` con: `Model` (texto ≤150 al capturar, obligatorio, recortado; la columna admite 200 para nombres migrados), `ModelKey`, `Measures` (texto ≤49, nulo), `MeasuresKey`, `Brand` (texto ≤100, nulo), `BrandSearch`, `SupplierId` (GUID, nulo), `PackagingThousandths` (long, nulo; "> 0, hasta 3 decimales"), `PiecesPerBox` (int, nulo; "> 0 si existe"), `PricePerSqmCents` y `PricePerBoxCents` (long, nulos; "> 0 si existen"), `PiecePriceCents` (long, nulo; antes `Price`), `IdentityKey` (texto, nulo), `NeedsReview` (bool). `Name` y `NameSearch` pasan a derivarse de `Model` + `" " + Measures`. Actualizar `Create`, `Update` y `Apply` con las invariantes de data-model.md (loseta: al menos X M2 o X CAJA; no loseta: precio por pieza > 0). Constantes de longitud públicas.
- [X] T007 Agregar en `Product` los métodos `AvailableModes`, `PriceFor(SaleMode)` (Box: X CAJA o X M2 × Embalaje; SquareMeter: X M2 o X CAJA ÷ Embalaje; Piece: solo precio por pieza; derivados redondeados al centavo, media hacia arriba; nulo si no hay forma de derivarlo) y `MarkReviewed()`/`MarkForReview()`. En `src/Pos.Domain/Products/Product.cs`.
- [X] T008 Actualizar `src/Pos.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`: columnas nuevas con sus longitudes, `PiecePriceCents` nulable (renombra `Price`), índice único filtrado sobre `IdentityKey` con `DeletedAt IS NULL AND IdentityKey IS NOT NULL`, índices en `BrandSearch`, `MeasuresKey` y `NeedsReview`, `HasData` para `MTK` y el cambio de `XBX`; entidad `ProductMigrationSummary` (data-model.md) en `src/Pos.Domain/Products/ProductMigrationSummary.cs` y su configuración, registrada en `src/Pos.Infrastructure/Persistence/PosDbContext.cs`.
- [X] T009 Generar la migración `TileSupplierFields` con `dotnet ef migrations add TileSupplierFields --project src/Pos.Infrastructure --output-dir Persistence/Migrations`; completar con SQL de datos: `Model = Name` (completo, hasta 200 caracteres), `NeedsReview = 1` si `length(Name) > 150`, `PricePerBoxCents = Price` y `PiecePriceCents = NULL` donde `UnitCode = 'XBX'`, `PiecePriceCents = Price` en los demás, `NeedsReview = 0` en el resto. Revisar el SQL generado (reconstrucción de `Products`) según `docs/migraciones.md`.
- [X] T010 [P] Pruebas de dominio en `tests/Pos.Domain.Tests/Products/ProductTextTests.cs`: "33 X 33" = "33X33", "120*240" = "120X240", acentos, espacios dobles, símbolos (`&`, `-`, `/`, paréntesis), medida de tres dimensiones "1.22 X 2.44 X .03".
- [X] T011 [P] Pruebas de dominio en `tests/Pos.Domain.Tests/Products/ProductPricingTests.cs`: `PriceFor` con X CAJA $287.05 y X M2 $175.03; derivación con redondeo; sin precio derivable devuelve nulo; invariantes de loseta y no loseta; embalaje "1.8" = "1.80", rechazo de 0, negativos y 4 decimales.
- [X] T039 [P] Crear `src/Pos.Domain/Products/ProductPricing.cs` con `SuggestBoxPrice`, `SuggestSquareMeterPrice` (redondeo al centavo, media hacia arriba, `decimal`/enteros, sin `double`) y `ExceedsTolerance(boxCents, sqmCents, packagingThousandths)` con 2 %.
- [X] T040 [P] Pruebas en `tests/Pos.Domain.Tests/Products/ProductPricingSuggestionTests.cs`: $196.25 × 1.86 = $365.03; inversa con redondeo; tolerancia justo en 2 % y por encima; sin embalaje no sugiere.
- [X] T012 Compilar `dotnet build -v q` y corregir los usos de `Product.Price`, `Create` y `Update` que se rompan en `src/` y `tests/` (solo lo mínimo para compilar; la lógica de cada historia va en su fase).

**Checkpoint**: dominio y esquema listos.

---

## Phase 3: US2 – Capturar y editar productos con los campos del proveedor (P1)

**Meta**: alta y edición con Modelo, Medidas, Embalaje, Piezas por caja, X M2, X CAJA, precio por pieza, Marca y Proveedor; código automático; sección "Datos del proveedor".

**Prueba independiente**: crear una loseta completa y un producto de pieza; editar ambos; validar mensajes.

- [X] T013 [P] [US2] Actualizar `src/Pos.Application/Products/ProductFields.cs`, `ProductDto.cs`, `ProductMapping.cs` y `ProductMessages.cs` con los campos nuevos (precios en centavos, embalaje en milésimas, `NeedsReview`) y los mensajes en español (modelo obligatorio, embalaje inválido, falta de precio de loseta).
- [X] T014 [US2] Actualizar `CreateProductCommand.cs`/`CreateProductValidator.cs`/`CreateProductHandler.cs` en `src/Pos.Application/Products/CreateProduct/`: entrada con `Model`, `Measures`, `Brand`, `SupplierId`, `PackagingText` ("NA" y vacío = no aplica), `PiecesPerBox`, `PricePerSqmCents`, `PricePerBoxCents`, `PiecePriceCents`; SKU opcional (vacío genera uno único); el proveedor, si se indica, debe existir y estar activo; devolver advertencias.
- [X] T015 [US2] Igual para `src/Pos.Application/Products/UpdateProduct/` (`UpdateProductCommand.cs`, `UpdateProductValidator.cs`, `UpdateProductHandler.cs`); incluir el cambio de unidad conservando la regla de movimientos existente y exigir precios de loseta al pasar de pieza a caja/m².
- [X] T016 [US2] Generador de SKU único en `src/Pos.Domain/Products/ProductText.cs` (`GenerateSku` con prefijo y consecutivo/GUID corto) y su uso en `CreateProductHandler`; comprobar unicidad con `IProductRepository.SkuExistsAsync` en `src/Pos.Application/Products/IProductRepository.cs`.
- [X] T017 [P] [US2] Prueba de integridad en `tests/Pos.Application.Tests/Products/CreateProductHandlerTests.cs`: modelo vacío rechazado, loseta sin precios rechazada, SKU generado único, embalaje "NA" aceptado, advertencia de diferencia > 2 % sin bloquear.
- [X] T018 [US2] Actualizar `src/Pos.Desktop/Products/ProductEditorViewModel.cs` y su vista (`src/Pos.Desktop/Products/`) con el formulario de [contracts/ui.md](contracts/ui.md): bloque "Datos del proveedor" (incluye Precio por pieza, FR-014a) a dos columnas en pantalla ancha y una en angosta; ocultar Embalaje, Piezas por caja, X M2 y X CAJA si el producto no es loseta; mostrar advertencia amarilla; etiquetas "Medidas" y "Embalaje" (textos en el archivo de recursos de `src/Pos.Desktop/Resources/`).
- [X] T056 [US2] Auditoría de productos (018): agregar Modelo, Medidas, Marca, Proveedor, Embalaje, Piezas por caja, X M2, X CAJA, precio por pieza y "Para revisión" a `src/Pos.Application/Products/ProductAuditFields.cs` y a la instantánea de auditoría que usan `CreateProductHandler`/`UpdateProductHandler`; prueba en `tests/Pos.Infrastructure.Tests/Audit/ProductAuditTests.cs`.
- [X] T019 [US2] Renombrar en toda la interfaz "Formato/Medida" y "M² por caja" si aparecen en `src/Pos.Desktop/` y `docs/manual-usuario/`; buscar con `grep -rn "Formato\|M² por caja"`.

**Checkpoint**: se puede capturar y editar con todos los campos.

---

## Phase 4: US4 – Unicidad proveedor + modelo + medidas (P1)

**Meta**: nunca dos productos con la misma identidad normalizada; sí con otra medida.

**Prueba independiente**: guardar duplicados y variantes (mayúsculas, acentos, "33 X 33"/"33x33", "120*240"/"120X240", sin proveedor).

- [X] T020 [US4] En `src/Pos.Infrastructure/Products/ProductRepository.cs` y `IProductRepository`: `IdentityExistsAsync(identityKey, excludingId)` que devuelve el producto existente, y mapear la violación del índice único a `SaveOutcome.Duplicate("Identity")` en `SaveChangesAsync`.
- [X] T021 [US4] En `CreateProductHandler`/`UpdateProductHandler` calcular la `IdentityKey` con `ProductText.BuildIdentityKey`, consultar duplicado y devolver error con el nombre y SKU del existente; al guardar un producto con `NeedsReview` cuya clave sea libre y con modelo válido, quitar la marca y calcular su `IdentityKey` (FR-017a); guardar una loseta sin embalaje no la vuelve a marcar.
- [X] T022 [P] [US4] Pruebas de integridad con SQLite real en `tests/Pos.Infrastructure.Tests/Products/ProductIdentityTests.cs`: mismo proveedor+modelo+medida rechazado; otra medida aceptada; variantes normalizadas rechazadas; sin proveedor solo choca con sin proveedor; mismo modelo y medida con otro proveedor aceptado; índice único impide la carrera.
- [ ] T023 [US4] Mostrar en `ProductEditorViewModel.cs` el mensaje de duplicado con acceso al producto existente (contracts/ui.md). **Parcial**: el mensaje muestra nombre y SKU del existente; falta el enlace/botón para abrirlo.

---

## Phase 5: US1 – Conservar todo al actualizar (P1) 🎯 MVP junto con US2, US4 y US6

**Meta**: migración sin pérdida, duplicados marcados, resumen una sola vez.

**Prueba independiente**: migrar la base de ejemplo `v0.17.0.db` y comparar conteos y totales.

- [X] T024 [US1] Crear `src/Pos.Infrastructure/Startup/ProductIdentityBackfill.cs`: tras migrar, para cada producto sin `IdentityKey` y no borrado calcula `ModelKey`, `MeasuresKey`, `BrandSearch` y clave; el más antiguo de cada grupo duplicado conserva la clave y los demás quedan con clave nula y `NeedsReview = 1`; marca `NeedsReview` en loseta sin embalaje, en modelo vacío y en modelo de más de 150 caracteres (solo en la migración); escribe una fila en `ProductMigrationSummaries` (migrados, sin modelo, sin embalaje, duplicados; `Shown = 0`) solo si no existe. Todo en una transacción.
- [X] T025 [US1] Invocar el backfill (se invoca al final de `SqliteDatabaseMaintenance.MigrateAsync`, no como `StartupStep` nuevo: así corre en la misma operación que migra y, si falla, `DatabaseStartup` restaura el respaldo; ver docs/migraciones.md). Original: `src/Pos.Application/Startup/DatabaseStartup.cs` (nuevo `StartupStep`, tras "migrar" y antes de abrir sesión) respetando el orden de la constitución (respaldo → migrar → restaurar si falla); registrar el resumen en el log con Serilog.
- [X] T026 [P] [US1] Casos de uso `src/Pos.Application/Products/MigrationSummary/GetMigrationSummaryHandler.cs` y `MarkMigrationSummaryShownHandler.cs` con DTO y registro en `src/Pos.Application/DependencyInjection.cs`.
- [X] T027 [US1] Diálogo del resumen en `src/Pos.Desktop/Startup/` (o `Shell/`) tras el primer inicio de sesión si `Shown = 0`, con botón "Ver productos para revisión"; se marca visto al cerrar.
- [X] T028 [P] [US1] Prueba de migración obligatoria en `tests/Pos.Infrastructure.Tests/SampleDatabases/TileSupplierFieldsMigrationTests.cs` (patrón de `ProductCategoriesMigrationTests.cs`): migrar `v0.17.0.db`; mismos conteos de productos, ventas, imágenes y existencias; totales de reportes idénticos; productos de caja con X CAJA = precio anterior; productos de pieza con precio por pieza; `Model = Name`; un nombre de 200 caracteres se conserva completo y queda marcado "para revisión".
- [X] T029 [P] [US1] Prueba en `tests/Pos.Infrastructure.Tests/Products/ProductBackfillTests.cs`: dos productos con el mismo nombre quedan ambos y uno marcado; el resumen se crea una sola vez y reiniciar no lo duplica; restaurar un respaldo anterior produce el mismo resultado.
- [X] T057 [P] [US1] Prueba de rendimiento en `tests/Pos.Infrastructure.Tests/Products/ProductBackfillTests.cs`: 10,000 productos migrados y con clave calculada en menos de 10 s.
- [X] T030 [US1] Verificar que `src/Pos.Infrastructure/Startup/SqliteBackupService.cs` y la restauración funcionan con el esquema nuevo (sin cambios de código si las pruebas T028 y T029 pasan).

---

## Phase 6: US6 – Venta con el precio correcto (P1)

**Meta**: modalidad por partida (caja, m², pieza) con el precio correspondiente; descripción con modelo y medidas; inventario consistente.

**Prueba independiente**: vender 3 cajas a $287.05 y 4.92 m² a $175.03 → $861.15 en ambas.

- [X] T031 [US6] Actualizar `src/Pos.Domain/Sales/CartLine.cs` y `CartProduct` para incluir `SaleMode`, los precios disponibles y el embalaje; la línea usa `Product.PriceFor(mode)`; cambiar de modalidad conserva la cantidad válida o la ajusta a los decimales de la unidad.
- [X] T032 [US6] Actualizar `src/Pos.Application/Sales/SaleProductMapping.cs`, `SaleDtos.cs` y `FindProductsForSale/FindProductsForSaleHandler.cs` para devolver modalidades disponibles (solo las que tienen precio derivable) y la inicial (la unidad del producto).
- [X] T033 [US6] En `src/Pos.Domain/Sales/SaleLine.cs` y el caso de uso de confirmar venta (`src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs`) guardar `UnitCode` de la modalidad vendida y `ProductName` con modelo y medidas; convertir la cantidad a la unidad del producto con el embalaje para el movimiento de inventario (m² ÷ embalaje = cajas, 3 decimales); rechazar modalidades distintas a la unidad en productos con inventario sin embalaje.
- [X] T034 [US6] Devoluciones y cancelaciones (`src/Pos.Application/Returns/`, `Sale.Cancel`): usar la misma conversión para reintegrar existencia; revisar `Quantity.FitsDecimals` con `XBX` de 3 decimales.
- [X] T035 [US6] Selector Caja / m² / Pieza por partida en `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs` y su vista ([contracts/ui.md](contracts/ui.md)).
- [X] T036 [P] [US6] Pruebas de cálculo en `tests/Pos.Domain.Tests/Sales/TileSaleTests.cs`: 3 × $287.05 = $861.15; 4.92 × $175.03 = $861.15; precio derivado con redondeo; sin precio derivable la modalidad no está disponible; pieza sin precio por pieza no se vende.
- [X] T037 [P] [US6] Prueba de consistencia de inventario en `tests/Pos.Infrastructure.Tests/Sales/TileSaleInventoryTests.cs` con SQLite real: vender 4.92 m² de un producto por caja con embalaje 1.64 descuenta 3.000 cajas; cancelar la reintegra exactamente; vender y devolver parcial no deja residuos de redondeo.
- [X] T054 [US6] Precio mostrado para loseta fuera del punto de venta: definir y aplicar en `src/Pos.Application/Scanner/InspectScan/` y en los listados de existencias (`src/Pos.Application/Inventory/SearchStock/`) qué precio se muestra (X CAJA si la unidad es caja, X M2 si es m², precio por pieza en los demás); revisar los demás usos de `Product.Price` con `grep -rn "\.Price\b" src` y anotar el resultado en `research.md`.
- [X] T055 [US6] Revisar el efecto de `XBX` con 3 decimales: `Quantity.FitsDecimals` en `src/Pos.Domain/Common/Quantity.cs`, la captura de movimientos (`src/Pos.Application/Inventory/RegisterMovement/`) y las alertas de existencia (`src/Pos.Application/Inventory/CheckStockAlerts/`); ajustar y cubrir en `tests/Pos.Infrastructure.Tests/Inventory/`.
- [X] T053 [US6] Compatibilidad de borradores: revisar `src/Pos.Domain/Sales/SaleDraft.cs` y `src/Pos.Application/Sales/ISaleDraftStore.cs`; los carritos guardados con el formato anterior (sin modalidad) deben cargarse con la modalidad inicial de la unidad del producto y el precio vigente; prueba en `tests/Pos.Infrastructure.Tests/Sales/` (archivo de borradores existente).
- [X] T038 [US6] Confirmar que las ventas ya registradas no cambian: ampliar T028 para comparar descripción, precio y total de cada `SaleLine` antes y después.

---

## Phase 7: US3 – Sugerencia de precio (P2)

**Prueba independiente**: X M2 $196.25 y Embalaje 1.86 → X CAJA sugerido $365.03. (La lógica de cálculo y sus pruebas están en T039–T040, fase base.)

- [X] T041 [US3] En `ProductEditorViewModel.cs` mostrar la sugerencia (de `ProductPricing`, T039) con botón "Usar" que no sobrescribe el valor escrito; la advertencia del 2 % de T018 usa el mismo `ProductPricing`.

---

## Phase 8: US5 – Ver, buscar y filtrar (P2)

**Prueba independiente**: con varias marcas y medidas, "fort wood" devuelve todos los FORT WOOD; filtros por marca, medida y "Para revisión".

- [X] T042 [US5] Ampliar `ProductSearch` y `SearchProductsQuery` (`src/Pos.Application/Products/IProductRepository.cs`, `SearchProducts/SearchProductsQuery.cs`, `SearchProductsHandler.cs`) con `Brand`, `Measures` y `NeedsReviewOnly`; un texto con forma de medida se compara con `MeasuresKey` normalizada.
- [X] T043 [US5] Implementar en `src/Pos.Infrastructure/Products/ProductRepository.cs` y `ProductTextFilter.cs` el predicado de búsqueda (nombre, `BrandSearch`, `MeasuresKey`) y los filtros; incluir `PricePerSqmCents`, `PricePerBoxCents`, `Model`, `Measures` y `Brand` en `ProductListItemDto`.
- [X] T044 [P] [US5] Caso de uso `src/Pos.Application/Products/GetProductFilters/GetProductFiltersHandler.cs` (marcas y medidas distintas de productos no borrados) y registro en `DependencyInjection.cs`.
- [X] T045 [US5] Actualizar el listado en `src/Pos.Desktop/Products/ProductsViewModel.cs` y su vista: columnas Modelo, Medidas, Marca, X M2, X CAJA, Existencia; filtros Marca, Medida y "Para revisión"; insignia en productos marcados.
- [X] T046 [P] [US5] Pruebas en `tests/Pos.Infrastructure.Tests/Products/ProductSearchTests.cs` (ampliar): "fort wood" encuentra todas las medidas; "60 x 60" encuentra "60X60"; marca sin acentos; filtros; y en `ProductPerformanceTests.cs` el catálogo de 5,000 productos responde en < 1 s.

---

## Phase 9: US7 – Reportes de productos (P3)

**Prueba independiente**: el reporte de productos trae las 5 columnas y los mismos totales.

- [X] T047 [US7] Localizar el lector y la exportación del reporte de productos con `grep -rn "InventoryReport" src/Pos.Application/Reports src/Pos.Infrastructure` y agregar Modelo, Medidas, Embalaje, X M2 y X CAJA (vacío si no aplica) en `src/Pos.Application/Reports/GetInventoryReport/` y `src/Pos.Application/Reports/Export/`; sin cambiar el cálculo de totales.
- [X] T048 [P] [US7] Prueba en `tests/Pos.Infrastructure.Tests/Reports/` (archivo del reporte de inventario existente): columnas nuevas presentes y totales idénticos a los anteriores.

---

## Phase 10: Pulido

- [X] T049 [P] Actualizar `docs/manual-usuario/` (productos, ventas, resumen de migración) y `docs/migraciones.md` con la migración `TileSupplierFields` y el paso `ProductIdentityBackfill`.
- [X] T050 [P] Actualizar `tests/Pos.ArchitectureTests/` si alguna regla de capas se ve afectada y confirmar que pasa.
- [ ] T051 (paso de publicación, pendiente hasta fijar la versión) Generar y conservar la base de ejemplo de la versión nueva con `tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseGenerator.cs` al publicar (Principio IV).
- [ ] T052 (suites en verde; falta la verificación manual de SC-006 y revisar la interfaz con la app abierta) Ejecutar los escenarios de [quickstart.md](quickstart.md) (incluida la verificación manual de SC-006: capturar una loseta completa en menos de 2 minutos), `dotnet build -v q` sin advertencias y las suites de `Pos.Domain.Tests`, `Pos.Application.Tests` y `Pos.Infrastructure.Tests`.

---

## Dependencias

- Setup → Foundational → historias.
- T039–T040 (precios) están en la fase base porque las usan US2 y US3.
- US2 (captura) antes de US4 (unicidad en los casos de uso) y de US3 (sugerencia en el formulario).
- US1 depende de T006–T009 y del backfill; es independiente de US2.
- US6 depende de T004–T007 y T031–T034 antes de la interfaz T035.
- US5 depende de T008 (columnas e índices) y de T013.
- US7 depende de T013.
- Orden sugerido: Fase 2 → US2 → US4 → US1 → US6 → US3 → US5 → US7 → Pulido.

## Paralelismo

- Fase 2: T003, T004, T005 juntas; T010 y T011 juntas tras T006–T007.
- US2: T013 y T017 en paralelo con T016.
- US1: T026, T028 y T029 en paralelo tras T024.
- US6: T036 y T037 en paralelo tras T033.
- US3, US5 y US7 son independientes entre sí tras US2.

## Estrategia

- **MVP**: Fase 2 + US2 + US4 + US1 + US6 (conservar datos, capturar, no duplicar, vender con el precio correcto).
- Después: US3, US5, US7 y pulido, en entregas incrementales con `dotnet build -v q` y las pruebas del proyecto modificado en cada una.
- **Decisión**: Acabado y Costo no se incluyen (spec, Asunciones; research §1b).
