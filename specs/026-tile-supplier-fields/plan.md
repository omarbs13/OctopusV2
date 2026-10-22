# Implementation Plan: Campos de proveedor de loseta en el producto

**Branch**: `026-tile-supplier-fields` | **Date**: 2026-10-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/026-tile-supplier-fields/spec.md`

## Summary

Se amplía el agregado `Product` con los datos con los que los proveedores de loseta manejan su
catálogo y se ajustan la búsqueda, el punto de venta y los reportes de productos:

1. **Campos nuevos en `Product`** (ver [data-model.md](data-model.md)): Modelo (obligatorio), Medidas,
   Marca, Proveedor (opcional), Embalaje (milésimas de m²), Piezas por caja, X M2, X CAJA y precio por
   pieza (el `Price` actual pasa a ser nulable y se renombra).
2. **Identidad**: una clave normalizada `IdentityKey` (proveedor + modelo + medidas) con índice único
   filtrado; los duplicados migrados quedan con clave nula y la marca "Para revisión".
3. **Nombre derivado**: `Name` se conserva pero se deriva de Modelo + Medidas, de modo que tickets,
   ventas, reportes, auditoría y búsqueda siguen funcionando sin cambios y la descripción de las ventas
   nuevas incluye modelo y medidas (FR-015).
4. **Punto de venta**: la modalidad (caja, m² o pieza) se elige por partida; el precio sale de X CAJA,
   X M2 o el precio por pieza. Se agrega la unidad "Metro cuadrado" al catálogo fijo.
5. **Migración**: una migración de esquema y un paso de arranque en C# (normalización con acentos no es
   posible en SQL) que calcula las claves, marca duplicados y deja el resumen de una sola vez.
6. **Reportes de productos** (009) con 5 columnas más; **respaldo/restauración** sin cambios de código
   salvo verificar que bases anteriores migran.

Sin dependencias externas nuevas.

## Brechas entre la spec y el código actual (importante)

La spec da por existentes campos que **no existen** hoy en `Product`: Marca, Acabado, Costo, Formato/Medida,
M² por caja y proveedor del producto (hoy `Product` solo tiene Nombre, SKU, código de barras, Precio, Unidad,
inventario, categoría, imagen). Este plan **crea** Marca, Medidas, Embalaje y Proveedor y migra solo lo
que existe. **Acabado y Costo no se agregan** (no son datos de los archivos de proveedor ni se usan en
ninguna regla de la spec); el Costo de compra ya vive por compra (020). La spec ya refleja esto. Ver [research.md](research.md) §1.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (igual que el resto)

**Primary Dependencies**: las existentes (Avalonia, CommunityToolkit.Mvvm, EF Core 10 Sqlite,
FluentValidation, Serilog). Ninguna nueva.

**Storage**: SQLite existente. Una migración nueva (`TileSupplierFields`) con reconstrucción de la tabla
`Products` (la columna `Price` pasa a nulable) y tabla `ProductMigrationSummaries` de una fila.

**Testing**: xUnit v3; SQLite real; pruebas de dominio para normalización, redondeo y reglas de precios;
pruebas de infraestructura para unicidad, búsqueda y migración de la base de ejemplo; sin pruebas de
ViewModels ni de interfaz (constitución VI).

**Target Platform**: escritorio Windows 10+ y Linux

**Project Type**: aplicación de escritorio con arquitectura por capas (Domain / Application / Infrastructure / Desktop)

**Performance Goals**: búsqueda, filtro y cambio de página < 1 s con 5,000 productos (SC-005); la
migración de 10,000 productos termina en menos de 10 s.

**Constraints**: importes en centavos; cantidades en milésimas; fechas UTC; migración reversible por
respaldo automático previo (constitución IV).

**Scale/Scope**: hasta ~10,000 productos; 1 migración; ~25 archivos de código modificados en las 4 capas.

## Constitution Check

*Se evalúa contra la constitución vigente en el repositorio (commit `HEAD`, principios I–IX de escritorio
.NET). El archivo del árbol de trabajo tiene cambios sin confirmar que lo describen como app Android; no
coincide con el código y no se usó (ver Notas).*

| Principio | Evaluación |
|---|---|
| I. La venta nunca se detiene | ✅ La migración respalda antes y restaura si falla; los productos duplicados se marcan, no bloquean. |
| II. Capas hacia el centro | ✅ Reglas (normalización, redondeo, precios) en Domain; casos de uso en Application; EF en Infrastructure. |
| III. Lógica en el núcleo | ✅ `Product`, `Measures`, `ProductPricing` y `SaleMode` en Domain. |
| IV. Integridad de datos | ✅ Centavos y milésimas; migración EF versionada con revisión de SQL; base de ejemplo de la versión anterior se migra en prueba; versión de concurrencia intacta. |
| V. Multiplataforma | ✅ Sin código de plataforma. Normalización con `string.Normalize` de .NET. |
| VI. Calidad verificable | ✅ Pruebas solo en reglas con cálculos, validaciones de integridad, migración y consistencia de inventario. |
| VII. Simplicidad | ✅ Sin dependencias ni capas nuevas; se evita una tabla de medidas o marcas (texto libre normalizado). |
| VIII. Soporte | ✅ El resumen de migración se registra también en el log. |
| IX. Seguridad local | ✅ Sin cambios de permisos; la edición usa el permiso de productos existente. |

Resultado: **sin violaciones**; no hay Complexity Tracking. Re-evaluado tras el diseño: igual.

## Project Structure

### Documentation (this feature)

```text
specs/026-tile-supplier-fields/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── ui.md
│   └── use-cases.md
└── tasks.md            # lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/Pos.Domain/
├── Products/Product.cs                 # campos nuevos, Apply, nombre derivado, PriceFor(mode)
├── Products/ProductText.cs             # NEW normalización de modelo y medidas, IdentityKey
├── Products/ProductPricing.cs          # NEW sugerencia X CAJA/X M2, advertencia del 2 %
├── Products/UnitOfMeasure.cs           # + "MTK" Metro cuadrado
├── Sales/SaleMode.cs                   # NEW Box, SquareMeter, Piece
├── Sales/CartLine.cs, SaleLine.cs      # precio y unidad por modalidad
src/Pos.Application/
├── Products/{ProductDto,ProductFields,ProductMapping,ProductMessages,ProductRules}.cs
├── Products/{Create,Update,Search}Product*/ # validadores, comandos, filtros marca/medida/para revisión
├── Products/GetProductFilters/         # NEW listas de marcas y medidas
├── Products/MigrationSummary/          # NEW leer y marcar como visto
├── Sales/{SaleProductMapping,SaleDtos,FindProductsForSale}  # precios por modalidad
├── Reports/ (productos)                # 5 columnas nuevas
src/Pos.Infrastructure/
├── Persistence/Configurations/ProductConfiguration.cs, Migrations/*_TileSupplierFields*
├── Products/ (repositorio: unicidad, búsqueda, filtros)
├── Startup/ProductIdentityBackfill.cs  # NEW paso de arranque tras migrar
src/Pos.Desktop/
├── Products/{ProductEditorViewModel,ProductsViewModel} + vistas   # sección "Datos del proveedor"
├── Sales/PointOfSaleViewModel.cs + vista                          # selector de modalidad
├── Startup/ (diálogo del resumen de migración)
tests/
├── Pos.Domain.Tests/Products/          # normalización, pricing, identidad
├── Pos.Application.Tests/Products/     # validadores y casos de uso
├── Pos.Infrastructure.Tests/Products/  # unicidad, búsqueda, migración, ventas con modalidad
docs/                                    # actualizar manual de productos y migraciones
```

**Structure Decision**: se mantiene la estructura por capas existente; no se crean proyectos nuevos.

## Notas

- La constitución del árbol de trabajo (sin confirmar) describe Android/Kotlin/Room; el código es .NET.
  Se recomienda revisar ese cambio antes de confirmarlo.
