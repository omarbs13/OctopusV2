# Implementation Plan: Mejoras al catálogo de Productos

**Branch**: `003-product-catalog-improvements` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-product-catalog-improvements/spec.md`

## Summary

Se prepara el catálogo de Productos para uso real:

1. **Defecto "Mostrar inactivos"**: la causa probable es el corte del listado en 200 filas (ver
   [research.md](research.md) §1). Primero se escribe una prueba que lo reproduce y después se
   corrige con la paginación.
2. **Paginación en la base**: páginas de 100 con `COUNT` y `LIMIT/OFFSET`, orden
   `NameSearch, Sku`, total de registros y navegación primera, anterior, siguiente y última. Al
   guardar, el listado salta a la página del producto guardado.
3. **Validaciones**:
   - El precio debe ser mayor que 0, con mensajes específicos, y acepta el separador de miles.
   - La unidad de medida es obligatoria, de un catálogo fijo con claves del SAT sembrado con
     `HasData`.
4. **Imagen del producto**:
   - Se guarda dentro de SQLite (tabla `ProductImages`), así que entra sola en los respaldos y en
     las restauraciones y se guarda en la misma transacción que el producto.
   - Se procesa con SkiaSharp, que ya viene con Avalonia: WEBP de 800 px y miniatura de 128 px.
   - El paquete de diagnóstico la excluye.

Una migración nueva (`ProductCatalogImprovements`) y una dependencia directa (SkiaSharp, ya
presente de forma transitiva).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (igual que la fundación)

**Primary Dependencies**:

- Las existentes: Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4, Microsoft.Extensions.Hosting,
  Serilog, EF Core 10 Sqlite y FluentValidation.
- **Nueva referencia directa**: `SkiaSharp` 3.119.4 en Infrastructure. Es la misma versión que
  ya trae Avalonia.
- `SkiaSharp.NativeAssets.Linux` 3.119.4 solo en `Pos.Infrastructure.Tests`.

**Storage**:

- SQLite existente. Tablas nuevas: `UnitsOfMeasure` (con `HasData`) y `ProductImages` (BLOB).
- La columna `Products.UnitCode` implica reconstruir la tabla `Products`.

**Testing**: xUnit v3; SQLite real para la persistencia (paginación, imágenes, respaldo,
diagnóstico y migración); ViewModels sin UI; imágenes de prueba generadas en memoria.

**Target Platform**: escritorio Windows 10+ y Linux (X11 o Wayland)

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas

**Performance Goals**:

- Con 10,000 productos, cambiar de página, buscar o cambiar el filtro tarda menos de 1 s (SC-002).
- Procesar una imagen de 5 MB tarda menos de 2 s fuera del hilo de la interfaz.

**Constraints**:

- Funciona sin conexión.
- 0 advertencias.
- Nunca se redondea un precio.
- Los datos y la imagen se guardan en una sola transacción.
- Ninguna imagen en el paquete de diagnóstico.
- La migración respalda antes y restaura si falla (el orden de la fundación no cambia).

**Scale/Scope**:

- Alrededor de 10,000 productos y hasta alrededor de 5,000 imágenes.
- La base crece de 300 a 450 MB en ese extremo (research §5).
- 2 pantallas modificadas (listado y editor).

No queda ningún NEEDS CLARIFICATION.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitución v1.1.0.

| Principio o regla | Cómo lo cumple el diseño | Antes | Después |
|---|---|---|---|
| I. Venta sin conexión | Nada depende de la red | ✅ | ✅ |
| I. Transacción única | Producto e imagen se guardan en un solo `SaveChanges` | ✅ | ✅ |
| I. Errores no cierran la app | La selección y el guardado pasan por `OperationRunner`. Una imagen inválida es un `Result` de error, no una excepción. Una miniatura ilegible muestra un ícono neutro y se registra | ✅ | ✅ |
| I. Respaldos | Las imágenes viven en la base, así que los respaldos automáticos, los previos a migración y las restauraciones las incluyen sin cambios | ✅ | ✅ |
| I. Estabilidad primero | El defecto de inactivos se corrige primero (H1, P1) | ✅ | ✅ |
| II. Capas | `IImageProcessor` en Application con implementación en Infrastructure (SkiaSharp). Desktop no referencia SkiaSharp de forma directa; muestra bytes con `Bitmap` de Avalonia. Las pruebas de arquitectura siguen aplicando | ✅ | ✅ |
| II. Organización por funcionalidad | `Products/PrepareProductImage`, `Products/ListUnitsOfMeasure`; `Infrastructure/Products/SkiaImageProcessor` | ✅ | ✅ |
| III. Lógica en el núcleo | Precio mayor que 0, unidad válida e imagen del agregado en `Product` (Domain). Formatos de precio en `Money`. Paginación y localización en el caso de uso. El ViewModel solo coordina | ✅ | ✅ |
| IV. Identificadores GUID v7 | `ProductImages.ProductId` es el GUID del producto. `UnitsOfMeasure` usa la clave SAT, porque es un catálogo fijo y no una entidad de negocio, y no usa autoincremento | ✅ | ✅ |
| IV. UTC y auditoría | `ProductImages` tiene auditoría de creación y modificación. Cambiar la imagen incrementa `Version` y `UpdatedAt` del producto | ✅ | ✅ |
| IV. Dinero en centavos | Sin cambios; `Money` sigue en `long` | ✅ | ✅ |
| IV. Code First y migraciones | Migración nueva `ProductCatalogImprovements`. El SQL se revisa, sobre todo la reconstrucción de `Products` (data-model) | ✅ | ✅ |
| IV. Base de ejemplo por versión | Se genera `v0.2.0.db` y la prueba de actualización cubre `v0.1.0.db` → actual | ✅ | ✅ |
| IV. Catálogos fijos con `HasData` | `UnitsOfMeasure` | ✅ | ✅ |
| V. Multiplataforma | SkiaSharp con binarios nativos para Windows y Linux (ya distribuidos por Avalonia). Sin rutas nuevas. CI en ambos sistemas | ✅ | ✅ |
| VI. Calidad | Prueba de reproducción del defecto; pruebas de todos los casos límite del precio; imagen con SQLite real; rendimiento con 10,000 productos; migración de la base de ejemplo | ✅ | ✅ |
| VI. Nunca el proveedor InMemory de EF | Todas las pruebas de persistencia usan SQLite real | ✅ | ✅ |
| VII. YAGNI | Una imagen por producto, sin editor de imágenes, sin catálogo de unidades editable ni paginación configurable. SkiaSharp se justifica (research §6) | ✅ | ✅ |
| VII. Sin repositorios genéricos | Se amplía `IProductRepository` | ✅ | ✅ |
| VIII. Diagnóstico | Errores de imagen y miniaturas ilegibles se registran con el `ProductId`, sin volcar bytes. El paquete de diagnóstico excluye imágenes (tamaño y privacidad) | ✅ | ✅ |
| IX. Seguridad local | Tope de 5 MB y de dimensiones antes de decodificar, contra imágenes diseñadas para agotar la memoria. El formato se identifica por la firma de los bytes y no por la extensión | ✅ | ✅ |
| Restricciones técnicas | FluentValidation para la entrada; Serilog; xUnit; gestión central de paquetes (se agrega `SkiaSharp` a `Directory.Packages.props`) | ✅ | ✅ |
| Flujo de desarrollo | Especificación y clarificación hechas; este plan; tareas a continuación | ✅ | ✅ |

**Resultado**: sin violaciones.

**Justificación de la dependencia (Principio VII)**:

- SkiaSharp es la única forma multiplataforma y con licencia MIT de decodificar JPG, PNG y WEBP y
  de codificar WEBP sin agregar binarios nativos nuevos: Avalonia ya los distribuye.
- La versión debe coincidir con la que trae Avalonia. Al actualizar Avalonia, se actualiza
  SkiaSharp a la misma versión.

## Project Structure

### Documentation (this feature)

```text
specs/003-product-catalog-improvements/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── use-cases.md     # SearchProducts paginado, PrepareProductImage, Money.Parse, puertos
│   └── ui.md            # Listado paginado, filas inactivas, editor con unidad e imagen
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

Archivos nuevos (➕) y modificados (✏️):

```text
src/Pos.Domain/
├── Common/Money.cs                           ✏️ Parse con separador de miles y MoneyParseError
├── Products/Product.cs                       ✏️ UnitCode, precio > 0, SetImage/RemoveImage
├── Products/UnitOfMeasure.cs                 ➕ catálogo fijo (claves SAT)
└── Products/ProductImage.cs                  ➕

src/Pos.Application/
├── Abstractions/Error.cs                     ✏️ InvalidImage(Reason)
├── Products/IProductRepository.cs            ✏️ GetAsync(includeImage), ProductPage, LocatePageAsync
├── Products/IImageProcessor.cs               ➕
├── Products/ProductDto.cs, ProductMapping.cs ✏️ UnitCode, Thumbnail, Image
├── Products/ProductRules.cs, ProductMessages.cs, ProductFields.cs ✏️
├── Products/ProductImageChange.cs, PreparedProductImage.cs ➕
├── Products/SearchProducts/                  ✏️ paginación y LocateProductId
├── Products/CreateProduct/, UpdateProduct/   ✏️ UnitCode e imagen
├── Products/PrepareProductImage/             ➕
├── Products/ListUnitsOfMeasure/              ➕
└── DependencyInjection.cs                    ✏️

src/Pos.Infrastructure/
├── Persistence/Configurations/ProductConfiguration.cs      ✏️ UnitCode, FK, índice (NameSearch, Sku)
├── Persistence/Configurations/UnitOfMeasureConfiguration.cs ➕ HasData
├── Persistence/Configurations/ProductImageConfiguration.cs  ➕
├── Persistence/PosDbContext.cs, AuditingInterceptor.cs     ✏️
├── Persistence/Migrations/<fecha>_ProductCatalogImprovements.cs ➕
├── Products/ProductRepository.cs             ✏️ COUNT + LIMIT/OFFSET, LEFT JOIN de miniatura, LocatePageAsync
├── Products/SkiaImageProcessor.cs            ➕
├── Diagnostics/ZipDiagnosticsExporter.cs     ✏️ quita imágenes de la copia y la compacta
├── DependencyInjection.cs                    ✏️
└── Pos.Infrastructure.csproj                 ✏️ SkiaSharp

src/Pos.Desktop/
├── Common/IDialogService.cs, DialogService.cs ✏️ PickOpenFileAsync
├── Common/ThumbnailConverter.cs              ➕ bytes → Bitmap, con ícono neutro si falla
├── Products/ProductsViewModel.cs, ProductsView.axaml       ✏️ paginación, filas inactivas, miniatura, unidad
├── Products/ProductEditorViewModel.cs, ProductEditorView.axaml ✏️ unidad, imagen, obligatorios
├── Resources/Strings.resx, Icons.axaml       ✏️
└── (sin cambios en Navigation, Forms ni Shell)

tests/
├── Pos.Domain.Tests/        Common/MoneyTests ✏️, Products/ProductTests ✏️, Products/UnitOfMeasureTests ➕
├── Pos.Application.Tests/   Products/CreateProductValidatorTests ✏️, UpdateProductValidatorTests ➕,
│                            ListUnitsOfMeasureHandlerTests ➕, PrepareProductImageHandlerTests ➕,
│                            SearchProductsHandlerTests ✏️ (adaptación), TestSupport/InMemoryProductRepository ✏️
├── Pos.Infrastructure.Tests/ Products/ProductPagingTests ➕, ProductImagePersistenceTests ➕,
│                            SkiaImageProcessorTests ➕, ProductPerformanceTests ✏️,
│                            Startup/SqliteBackupServiceTests ✏️ (imagen en respaldo y restauración),
│                            Diagnostics/ZipDiagnosticsExporterTests ✏️,
│                            SampleDatabases/ v0.2.0.db ➕, SampleDatabaseUpgradeTests ✏️,
│                            SampleDatabaseGenerator ✏️
│                            Pos.Infrastructure.Tests.csproj ✏️ SkiaSharp.NativeAssets.Linux
└── Pos.Desktop.Tests/       Products/ProductsViewModelInactiveFilterTests ➕ (reproduce el defecto),
                             ProductEditorViewModel*Tests ✏️ (solo adaptación), TestSupport/FakeDialogService ✏️

Directory.Packages.props     ✏️ SkiaSharp y SkiaSharp.NativeAssets.Linux 3.119.4
```

**Structure Decision**: se mantiene la solución de la fundación y se organiza por funcionalidad.
Todos los cambios se concentran en `Products` de cada capa, más tres puntos transversales:

- `ZipDiagnosticsExporter`, para excluir las imágenes del diagnóstico.
- `IDialogService.PickOpenFileAsync`, para el selector de archivos.
- `ThumbnailConverter`, para mostrar las miniaturas.

## Orden de implementación sugerido

1. **H1**: la prueba que reproduce el defecto, en rojo.
2. **H2**: la paginación, con la prueba de H1 ya en verde; incluye la prueba de rendimiento.
3. **H3**: `Money.Parse`, la regla de precio mayor que 0, la unidad de medida y la migración.
4. **H4**: la tabla de imágenes (misma migración si aún no se integra; si ya se integró, una
   migración nueva), `SkiaImageProcessor`, el editor, el listado y el diagnóstico.

La migración se genera una sola vez con el modelo completo, antes de publicar. Si H3 se integra
antes que H4, H4 agrega su propia migración (una migración publicada nunca se modifica).

## Documentación a actualizar

- `docs/carpeta-de-datos.md`: las imágenes viven dentro de `pos.db`; impacto en el tamaño de los
  respaldos; el diagnóstico no incluye imágenes.
- `docs/agregar-funcionalidad.md`: patrón de listado paginado (`ProductPage` y barra de
  paginación) como referencia para futuros catálogos.
- `docs/migraciones.md`: ejemplo de revisión de una reconstrucción de tabla en SQLite
  (`Products`).
- `specs/001-pos-foundation/contracts/ui.md`: nota de que el listado y el editor de Productos se
  rigen ahora por `specs/003-product-catalog-improvements/contracts/ui.md`.

## Complexity Tracking

Sin violaciones de la constitución; no aplica.
