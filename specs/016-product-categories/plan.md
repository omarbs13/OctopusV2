# Implementation Plan: Categorías de productos

**Branch**: `016-product-categories` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/016-product-categories/spec.md`

## Summary

Clasificar productos en categorías de un nivel y consultar ventas, productos más vendidos y
existencias por categoría, con totales que cuadran al centavo con los reportes sin filtro.

1. **Catálogo** (research §1, §3–§5): agregado `Category` con nombre único sin mayúsculas ni acentos
   (`NameKey` con índice único), descripción opcional, activa o inactiva, borrado lógico, versión y
   bitácora. Desactivar con productos exige confirmación en el caso de uso; eliminar solo sin productos
   no borrados, en una transacción de escritura.
2. **Asignación** (research §2, §4): `Products.CategoryId` nulo, sin clave foránea (evita reconstruir
   `Products`). Crear o cambiar la categoría de un producto valida, dentro de la transacción, que esté
   activa; conservar una inactiva sí se permite.
3. **Filtro común** (research §8): `CategoryFilter` (Todas, Sin categoría, una categoría) para el
   listado de productos y los dos reportes.
4. **Reportes** (research §9–§13): la categoría se resuelve al consultar (categoría vigente). El importe
   por línea es `AmountCents` (ya neto de descuentos, 015) menos lo devuelto en esa línea (013), así que
   las categorías suman exactamente el total. Nueva sección "Ventas por categoría" con desglose de
   productos; filtro en ventas e inventario; formas de pago no se desglosan por categoría.
5. **Exportaciones** (research §14): filtro, columna y sección en PDF y Excel.

Hay una migración nueva, `ProductCategories`: una tabla y una columna, sin reconstrucciones. No se
agrega ninguna dependencia externa ni permiso nuevo.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12, CommunityToolkit.Mvvm, Hosting, Serilog,
EF Core 10 Sqlite, FluentValidation). **No se agrega ninguna.**

**Storage**:

- SQLite, migración `ProductCategories`: tabla `Categories` y columna `Products.CategoryId` con índice.
- `Version` 0.10.0 → 0.11.0. Base de ejemplo `v0.11.0.db` según [docs/migraciones.md](../../docs/migraciones.md).

**Testing**: xUnit v3 con la política mínima de la constitución v1.2.0 (detalle en research §17):

- **Domain**: validaciones de `Category` y `ShareMath`.
- **Casos de uso sobre SQLite real**: nombre único, confirmación al desactivar, bloqueo de
  eliminación, asignación de categoría inactiva o borrada, cuadre de "Ventas por categoría" (descuentos,
  devoluciones, canceladas, sin categoría, reclasificación), reporte de ventas filtrado e inventario
  filtrado.
- **Migración**: `ProductCategoriesMigrationTests` y bases de ejemplo.
- **Arquitectura e inventario**: las obligatorias existentes.
- Sin pruebas de ViewModels, vistas ni exportadores.

**Target Platform**: Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Crear una categoría y asignarla en menos de 1 minuto de operación (SC-001).
- Reporte de ventas filtrado o agrupado por categoría en menos de 2 s con 10,000 ventas (SC-004): una
  consulta agrupada por producto, apoyada en los índices de `Sales.CreatedAt`, `SaleLines.SaleId` y
  `Products` por clave primaria.
- Listado de categorías con conteo de productos en menos de 200 ms con 500 categorías y 10,000
  productos (índice `IX_Products_CategoryId`).

**Constraints**:

- Funciona sin conexión; la venta no cambia (la categoría no interviene en el punto de venta).
- 0 advertencias.
- Eliminar categoría y asignar categoría a un producto se serializan con `BEGIN IMMEDIATE`.
- Sin reconstrucción de tablas en la migración.

**Scale/Scope**:

- Decenas a cientos de categorías; miles de productos.
- Casos de uso: **7 nuevos** (`SearchCategories`, `GetCategory`, `CreateCategory`, `UpdateCategory`,
  `SetCategoryActive`, `DeleteCategory`, `ListCategoryOptions`); **cambian** `CreateProduct`,
  `UpdateProduct`, `GetProduct`, `SearchProducts`, `GetSalesReport`, `GetInventoryReport` y la
  exportación.
- Pantallas: nueva "Catálogos > Categorías" con su formulario; cambian Productos (listado y
  formulario), Reportes > Ventas y Reportes > Inventario.

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó después del diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Todo es local. El punto de venta no cambia y un producto sin categoría se vende igual (FR-009). Eliminar categoría y asignarla a un producto son una transacción cada una. Los errores inesperados se registran y se muestran sin detalles técnicos. |
| II. Capas | `Category` y `ShareMath` en Domain; casos de uso, `CategoryFilter` e `ICategoryRepository` en Application; `CategoryRepository`, configuración EF y lectores en Infrastructure; ViewModels solo invocan casos de uso. La carpeta nueva `Categories/` queda cubierta por las pruebas de arquitectura. |
| III. Lógica en el núcleo | Validaciones de la categoría en Domain; reglas que dependen del conteo de productos (confirmar, bloquear eliminación, asignar solo activas) en los casos de uso; porcentajes con `ShareMath`. La interfaz no calcula importes ni porcentajes. |
| IV. Integridad de datos | GUID v7, UTC, `Version`, `DeletedAt` y auditoría en `Category`. Importes en centavos y sumas enteras. Migración de EF Core sin reconstrucciones, con SQL revisado y base de ejemplo 0.11.0. La ausencia de clave foránea se justifica en Complexity Tracking. |
| V. Multiplataforma | Sin código de plataforma. |
| VI. Calidad verificable | Solo se prueban validaciones de integridad (nombre único, eliminación, asignación, confirmación), cálculos de dinero (cuadre por categoría y por producto, porcentajes) y las obligatorias de migración, inventario y arquitectura. SQLite real. |
| VII. Simplicidad | Sin dependencias ni permisos nuevos (`ManageProducts` y `ViewProducts`, research §6). Un nivel de categoría. Sin categoría en la venta. Repositorio específico del agregado. |
| VIII. Soporte | Serilog registra alta, edición, cambio de estado, eliminación y rechazos con id de categoría y usuario. `docs/categorias.md` documenta reglas, categoría vigente y cuadre; `docs/reportes.md` se actualiza. |
| IX. Seguridad local | Las operaciones del catálogo quedan en la bitácora con usuario y fecha (FR-008). Solo el Administrador gestiona el catálogo. |

**Resultado**: sin violaciones. Decisiones explícitas:

- **Navegación (research §7)**: "Catálogos > Categorías", junto a "Productos", porque ese es el
  grupo actual del catálogo (la spec ya usa esta ruta).
- **Formas de pago con filtro de categoría (research §11)**: muestran "—" con la nota "No se desglosa
  por categoría"; un pago cubre la venta completa y no se prorratea.
- **Productos borrados al eliminar una categoría (research §4)**: su referencia se limpia en la misma
  transacción y sus ventas pasadas pasan a "Sin categoría".
- **Unidades de una categoría (research §9)**: suman cantidades de unidades distintas tal cual; el
  desglose por producto muestra la unidad.
- **Sección con filtro (research §12)**: con filtro de categoría, "Ventas por categoría" muestra solo esa
  categoría.

## Project Structure

### Documentation (this feature)

```text
specs/016-product-categories/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── application-ports.md
│   └── ui.md
├── checklists/
└── tasks.md             # Lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Pos.Domain/
│   ├── Categories/Category.cs                                        # nuevo agregado
│   ├── Products/Product.cs                                           # + CategoryId
│   └── Reports/ShareMath.cs                                          # porcentaje del total
├── Pos.Application/
│   ├── Abstractions/Error.cs                                         # + ConfirmationRequired, CategoryInUse, CategoryNotAssignable
│   ├── Audit/AuditActions.cs                                         # + CATEGORY_*, CategoryEntity
│   ├── Categories/
│   │   ├── ICategoryRepository.cs  CategoryDtos.cs  CategoryFilter.cs  CategoryFields.cs  CategoryMessages.cs
│   │   ├── SearchCategories/  GetCategory/  CreateCategory/  UpdateCategory/
│   │   └── SetCategoryActive/  DeleteCategory/  ListCategoryOptions/
│   ├── Products/ProductDto.cs  IProductRepository.cs  ProductMapping.cs  # + categoría y filtro
│   ├── Products/CreateProduct/  UpdateProduct/  GetProduct/  SearchProducts/
│   ├── Reports/GetSalesReport/SalesReportDtos.cs  GetSalesReportHandler.cs   # + Category, Categories
│   ├── Reports/GetInventoryReport/InventoryReportDtos.cs             # + Category, columna, orden
│   ├── Reports/Export/ReportDocumentBuilder.cs  ReportTexts.cs       # filtro, columna, sección
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── Categories/CategoryRepository.cs  CategoryQueryExtensions.cs  # WhereCategory
│   ├── Products/ProductRepository.cs                                 # filtro y nombre de categoría
│   ├── Reports/SalesReportReader.cs  InventoryReportReader.cs        # filtro y agrupación
│   ├── Persistence/Configurations/CategoryConfiguration.cs  ProductConfiguration.cs
│   ├── Persistence/PosDbContext.cs                                   # DbSet<Category>
│   ├── Persistence/Migrations/…_ProductCategories.cs
│   └── DependencyInjection.cs
├── Pos.Desktop/
│   ├── Categories/CategoriesModule.cs  CategoriesView*  CategoriesViewModel.cs
│   │   CategoryFormView*  CategoryFormViewModel.cs  CategoryPicker*
│   ├── Products/ProductsView*  ProductsViewModel.cs  ProductEditorView*  ProductEditorViewModel.cs
│   ├── Reports/SalesReportView*  SalesReportViewModel.cs  InventoryReportView*  InventoryReportViewModel.cs
│   ├── Resources/Strings.resx                                        # Category_*, Nav_Categories
│   └── Composition/HostBuilder.cs                                    # + AddCategoriesModule
tests/
├── Pos.Domain.Tests/Categories/       CategoryTests
├── Pos.Domain.Tests/Reports/          ShareMathTests
├── Pos.Infrastructure.Tests/Categories/  CategoryUseCaseTests, ProductCategoryAssignmentTests
├── Pos.Infrastructure.Tests/Reports/     SalesByCategoryReportTests, InventoryByCategoryReportTests
└── Pos.Infrastructure.Tests/SampleDatabases/  v0.11.0.db, ProductCategoriesMigrationTests
docs/
├── categorias.md                      # guía de soporte: reglas, categoría vigente, cuadre, eliminación
├── reportes.md                        # filtro, sección "Ventas por categoría", formas de pago, unidades
└── migraciones.md                     # + sección 0.11.0 (sin reconstrucciones)
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de 001 a 015.

- `Categories` es carpeta nueva en Domain, Application, Infrastructure, Desktop y pruebas.
- `CategoriesModule` registra la página `catalogs.categories` en el grupo `ProductsModule.GroupId`
  con orden 1 y permiso `ManageProducts`.
- `CategoryPicker` vive en Desktop/Categories y lo usan el formulario de producto y los filtros de
  Productos y Reportes.
- Las pruebas existentes que construyen `Product.Create/Update`, `ProductDto`, `SalesReport` o
  `InventoryReportRow` se ajustan a las firmas nuevas (parámetros opcionales donde sea posible).

## Complexity Tracking

| Desviación | Por qué se necesita | Alternativa más simple descartada |
|---|---|---|
| `Products.CategoryId` sin `FOREIGN KEY` en SQLite | Agregar una clave foránea a una tabla existente obliga a EF Core a reconstruir `Products`, la tabla más referenciada, en bases de clientes. El borrado de categorías es lógico, así que la clave foránea tampoco impediría apuntar a una borrada. La integridad la dan `DeleteCategory` y la asignación en transacciones serializadas, y una prueba lo verifica (research §2, §4) | Clave foránea con reconstrucción de `Products`: más riesgo en la migración sin proteger la regla real |
