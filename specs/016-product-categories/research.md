# Research: Categorías de productos

**Funcionalidad**: `016-product-categories` | **Fecha**: 2026-10-01 | **Plan**: [plan.md](plan.md)

El contexto técnico no tiene incógnitas abiertas: es el mismo stack de 001 a 015 y no se agrega
ninguna dependencia. Esta investigación resuelve cómo encaja la categoría en el catálogo de productos
(003), el inventario (004), los reportes (009), las devoluciones (013) y los descuentos (015).

Hallazgos del código que condicionan el diseño:

- `SaleLine.AmountCents` ya es el importe neto después del descuento de línea y de la parte repartida
  del descuento global (015). `Sale.TotalCents` es exactamente su suma (`Sale.Register`).
- Las devoluciones parciales (013) guardan el importe por línea en `SaleReturnLine.AmountCents`, y
  `Sale.ReturnedCents` es exactamente su suma (`Sale.ApplyReturn`). `SaleLine.ReturnedQuantity` acumula
  las milésimas devueltas.
- `SalesReportReader` calcula el total como `Σ (TotalCents − ReturnedCents)` de las ventas `Completed`;
  la fila del detalle muestra `TotalCents` original. Las canceladas quedan fuera por estado.
- `InventoryReportReader` carga en memoria todos los productos que controlan inventario y calcula las
  tarjetas sobre ese conjunto; el filtro de estado y la búsqueda solo afectan a la tabla.
- `TextNormalizer.ForSearch` quita acentos y pasa a minúsculas; `Product.NameSearch` ya lo usa.
- `IWriteTransactions` abre `BEGIN IMMEDIATE`: un solo escritor a la vez.
- `RolePermissions`: el Administrador tiene todos los permisos; el Cajero tiene `ViewProducts` y
  `ViewInventory`, pero no `ManageProducts`. `ManageProducts` no está ligado a ningún módulo licenciado.
- La navegación agrupa Productos en el grupo "Catálogos" (`ProductsModule.GroupId = "catalogs"`).

---

## §1. Entidad y unicidad del nombre

- **Decisión**: agregado `Category` (Domain/Categories) con `Name`, `NameKey`, `Description?`,
  `IsActive` y los campos estándar (auditoría, `DeletedAt`, `Version`). `NameKey =
  TextNormalizer.ForSearch(Name)`; índice único `IX_Categories_NameKey` filtrado por `DeletedAt IS NULL`.
  El caso de uso consulta antes de guardar para dar el mensaje; el índice cubre la carrera y el
  repositorio traduce la violación a `Duplicate`.
- **Por qué**: FR-003 exige unicidad sin mayúsculas ni acentos e incluyendo inactivas. Una columna
  normalizada con índice es el mismo patrón que `NameSearch` y que el RUC de 014. Excluir las borradas
  permite volver a crear un nombre eliminado.
- **Alternativas**: `COLLATE NOCASE` (rechazada: no ignora acentos); comparación solo en memoria
  (rechazada: no protege contra dos altas simultáneas).

## §2. Referencia del producto a la categoría, sin clave foránea en la base

- **Decisión**: `Products.CategoryId` (`TEXT NULL`) con índice `IX_Products_CategoryId` filtrado por
  `DeletedAt IS NULL`. **Sin** restricción `FOREIGN KEY` en SQLite. La integridad la garantizan los
  casos de uso dentro de transacciones `BEGIN IMMEDIATE` (§4) y una prueba de migración.
- **Por qué**: en SQLite, EF Core solo puede agregar una clave foránea a una tabla existente
  **reconstruyendo** `Products`, la tabla más referenciada (`ProductImages`, `ProductStocks`,
  movimientos). Además, como la categoría se borra lógicamente, la clave foránea no evitaría apuntar a
  una categoría borrada: la regla de negocio sigue siendo necesaria. Ver Complexity Tracking del plan.
- **Alternativas**: clave foránea con reconstrucción (rechazada: riesgo en bases de clientes sin
  beneficio real); tabla puente producto-categoría (rechazada: un producto tiene cero o una categoría).

## §3. Validaciones del catálogo

- **Decisión**: en `Category` (Domain): nombre recortado de 1 a 50 caracteres
  (`NameMaxLength = 50`), descripción recortada de hasta 200 (`DescriptionMaxLength = 200`); vacía o
  solo espacios se guarda como nula. FluentValidation en Application repite las reglas para devolver
  `ValidationFailed` por campo (mismo patrón que `CreateCustomerValidator`).
- **Por qué**: FR-002 y FR-004; el dominio rechaza estados inválidos aunque el validador falle.

## §4. Desactivar, eliminar y asignar sin carreras

- **Decisión**:
  - `SetCategoryActive(false)` cuenta los productos no borrados de la categoría. Si hay al menos uno y
    el comando no trae `Confirmed = true`, devuelve `ConfirmationRequired(ProductCount)` sin cambiar
    nada. La interfaz muestra el aviso con ese número y reintenta con `Confirmed = true`.
  - `DeleteCategory` abre una transacción de escritura, cuenta los productos **no borrados** (activos o
    inactivos) y, si hay alguno, devuelve `CategoryInUse(ProductCount)`. Si no hay, marca `DeletedAt` y
    pone `CategoryId = NULL` en los productos **borrados** que aún la referencian (`ExecuteUpdate`), en
    la misma transacción.
  - `CreateProduct` y `UpdateProduct` validan la categoría dentro de una transacción de escritura cuando
    la categoría elegida es distinta de la actual: debe existir, no estar borrada y estar activa
    (FR-011). Si no cambia, se acepta aunque esté inactiva.
- **Por qué**: como todas las escrituras están serializadas, "contar y borrar" y "validar y asignar"
  no se pueden intercalar: no hay producto vivo apuntando a una categoría borrada (spec, casos límite).
  Limpiar las referencias de los productos borrados evita que sus ventas pasadas se agrupen bajo una
  categoría que ya no existe; con eso se agrupan en "Sin categoría".
  La confirmación se exige en el caso de uso, no solo en la interfaz (SC-006).
- **Alternativas**: dejar la confirmación solo en la interfaz (rechazada: SC-006 pide que ninguna
  desactivación ocurra sin confirmar); mostrar ventas de productos borrados bajo una categoría borrada
  con su nombre (rechazada: la categoría "desaparece del listado y de los filtros").

## §5. Concurrencia y bitácora

- **Decisión**: `UpdateCategory`, `SetCategoryActive` y `DeleteCategory` reciben `ExpectedVersion` y
  devuelven `Conflict` si cambió (spec, edición simultánea). Cada operación agrega un registro con
  `IAuditLog`: `CATEGORY_CREATED`, `CATEGORY_UPDATED`, `CATEGORY_DEACTIVATED`, `CATEGORY_ACTIVATED`,
  `CATEGORY_DELETED`, entidad `Category` (FR-008). El usuario y la fecha los pone la bitácora.
- **Por qué**: mismo patrón que clientes (014) y cupones (015).

## §6. Permisos

- **Decisión**: sin permisos nuevos.
  - Gestionar el catálogo: `ManageProducts` (solo el Administrador lo tiene).
  - Listar categorías para filtros y selectores: `ViewProducts` (Administrador y Cajero).
  - Reportes: los permisos actuales (`ViewReports` para ventas, `ViewInventory` para inventario, FR-022).
- **Por qué**: el supuesto de la spec dice que la asignación la hace quien ya edita productos y que el
  catálogo es solo del Administrador; `ManageProducts` cumple ambos sin ampliar roles ni licencias.
  Un permiso nuevo sería idéntico a `ManageProducts` en todos los roles (Principio VII).
- **Alternativas**: `ManageCategories` (rechazada: sin diferencia observable hoy).

## §7. Navegación

- **Decisión**: la pantalla se registra como `catalogs.categories`, "Categorías", en el grupo existente
  "Catálogos", justo después de "Productos" (orden 1).
- **Por qué**: la spec la llama "Productos > Categorías" para indicar que pertenece al catálogo de
  productos; en la aplicación ese catálogo vive en "Catálogos". Crear un grupo "Productos" duplicaría la
  navegación.

## §8. Filtro de categoría como valor

- **Decisión**: `CategoryFilter` (Application/Categories), un `record struct` con tres formas: `All`,
  `Uncategorized` y `Only(Guid id)`. Lo usan `SearchProductsQuery`, `SalesReportQuery` e
  `InventoryReportQuery`; cada lector lo traduce a `Where` con un único método de extensión
  `WhereCategory` sobre `IQueryable<Product>` y sobre las líneas.
- **Por qué**: "Sin categoría" no es una categoría registrada (spec, Conceptos); un `Guid?` no distingue
  "todas" de "sin categoría".

## §9. Importe y unidades por categoría (cuadre al centavo)

- **Decisión**: el importe de una línea en cualquier vista por categoría es
  `SaleLine.AmountCents − Σ SaleReturnLine.AmountCents` de esa línea, y las unidades,
  `QuantityThousandths − ReturnedQuantity`. Solo ventas `Completed` del período (y del cajero, si hay
  filtro). La categoría sale de `Products.CategoryId` **al consultar**, con `LEFT JOIN` a productos.
  - Como `Σ líneas = TotalCents` y `Σ devoluciones por línea = ReturnedCents`, la suma de todas las
    categorías es exactamente el total del reporte sin filtro (FR-018, SC-002). Una prueba lo verifica
    con descuentos de línea, descuento global, devolución parcial, venta cancelada y productos sin
    categoría.
  - Cada categoría se arma sumando sus productos, por lo que el importe de la categoría es la suma de
    sus productos por construcción (SC-003).
- **Por qué**: 015 ya dejó el importe neto en la línea; 013 ya lo deja por línea en devoluciones. No
  hace falta guardar la categoría en la venta (spec, clarificación 1).
- **Unidades mezcladas**: una categoría puede tener productos por pieza y por kilo. La columna
  "Unidades" de la categoría suma las cantidades tal como están (hasta 3 decimales) y el desglose
  muestra cada producto con su unidad. Se documenta en `docs/reportes.md`.
- **Alternativas**: guardar `CategoryId` en `SaleLine` (rechazada por la clarificación); agrupar con
  `Sale.TotalCents` prorrateado (rechazada: duplicaría el reparto que 015 ya hizo).

## §10. Porcentaje del total

- **Decisión**: `ShareMath.BasisPoints(part, total)` (Domain/Reports): `(part × 10 000 + total / 2) /
  total`, mitad hacia arriba, 0 si el total es 0. Se muestra con un decimal (`PercentCell`). No se
  ajustan los porcentajes para que sumen 100 % (spec, casos límite).
- **Por qué**: cálculo entero y probado, mismo estilo que `VariationMath`.

## §11. Reporte de ventas filtrado por categoría

- **Decisión**: con `CategoryFilter` distinto de `All`:
  - Ventas consideradas: las `Completed` del período con al menos una línea de la categoría.
  - Cantidad de ventas: esas ventas. Total: Σ importe neto (§9) de sus líneas de la categoría. Ticket
    promedio: total / ventas, mitad hacia arriba (igual que hoy). Descuento: Σ
    `OriginalAmountCents − AmountCents` de esas líneas.
  - Gráfica por día y comparativo: mismas reglas sobre las líneas de la categoría.
  - Detalle: cada venta con el importe de sus líneas de la categoría (original, sin restar
    devoluciones, igual que la fila sin filtro muestra `TotalCents`).
  - **Formas de pago**: no se pueden atribuir a una categoría (un pago cubre toda la venta). Las
    tarjetas de efectivo, tarjeta, transferencia, nota de crédito y crédito muestran "—" con la nota
    "No se desglosa por categoría", en pantalla y en las exportaciones.
- **Por qué**: FR-014 y FR-015. Repartir pagos entre líneas inventaría una regla que la spec no pide.
- **Alternativas**: prorratear los pagos (rechazada: cifras que no existen en caja); ocultar las
  tarjetas (rechazada: el diseño de la pantalla cambiaría según el filtro).

## §12. Sección "Ventas por categoría"

- **Decisión**: `SalesReport.Categories` trae todas las categorías con ventas netas en el período (más
  "Sin categoría" si tiene), con unidades, importe, porcentaje y **todos** sus productos con ventas,
  ordenados por unidades desc, importe desc y nombre. Se calcula con **una** consulta agrupada por
  `ProductId` (líneas + devoluciones por línea + producto + categoría) y se agrupa por categoría en
  memoria. Categorías ordenadas por importe desc; fila de total = Σ categorías.
  - Respeta los filtros de período y cajero. Si hay filtro de categoría, la sección muestra solo esa
    categoría (consistente con FR-014).
  - El nombre del producto es el actual; si está borrado, se usa el último nombre vendido
    (`SaleLine.ProductName` de la línea más reciente).
  - Una categoría inactiva se muestra con "(inactiva)".
- **Por qué**: la expansión en pantalla y la exportación necesitan el desglose completo (FR-017,
  FR-021, SC-003); con 10 000 ventas son a lo más unos miles de productos, una sola consulta.
- **Alternativas**: cargar el desglose al expandir (rechazada: la exportación lo necesita completo y
  serían dos rutas de cálculo).

## §13. Reporte de inventario por categoría

- **Decisión**: `InventoryReportQuery.Category` filtra los productos **antes** de calcular las tarjetas;
  el filtro de estado y la búsqueda siguen afectando solo a la tabla. La fila agrega `CategoryName`
  (o "Sin categoría") y se puede ordenar por categoría (`InventoryReportSort.Category`, luego por
  nombre).
- **Por qué**: escenario 8 de la Historia 3 y FR-020. El lector ya filtra en memoria; el filtro de
  categoría se aplica en SQL (`WhereCategory`).

## §14. Exportaciones

- **Decisión**: `ReportDocumentBuilder` agrega "Categoría: <nombre>" o "Categoría: Sin categoría" a
  `FilterTexts`, la columna "Categoría" al inventario y, en ventas, la tabla "Ventas por categoría"
  con una fila por categoría seguida de las filas de sus productos (sangría en el nombre), más la fila
  de total. Mismas cifras que pantalla porque usan el mismo `SalesReport`.

## §15. Selector de categoría

- **Decisión**: un `CategoryPicker` (Desktop/Categories) basado en `ComboBox`. Opciones: "Sin
  categoría" + activas por nombre; si el producto tiene una inactiva, aparece como "<nombre>
  (inactiva)" solo como valor actual. Con más de 15 opciones habilita escribir para filtrar
  (`IsEditable` del `ComboBox` de Avalonia). En los filtros de listados y reportes las opciones son
  "Todas", "Sin categoría" y todas las no borradas (las inactivas marcadas).
- **Por qué**: spec, casos límite; sin dependencias nuevas.

## §16. Migración

- **Decisión**: migración `ProductCategories`:
  - `CreateTable Categories` con índices `IX_Categories_NameKey` (único, filtrado) e
    `IX_Categories_IsActive_NameKey`.
  - `AddColumn Products.CategoryId TEXT NULL` y `CreateIndex IX_Products_CategoryId`.
  - **Sin** reconstrucción de tablas; el SQL se revisa con `dotnet ef migrations script`.
  - `Version` 0.10.0 → 0.11.0 y base de ejemplo `v0.11.0.db` según
    [docs/migraciones.md](../../docs/migraciones.md).
- **Por qué**: SC-005, todos los productos existentes quedan con `CategoryId = NULL` sin tocar sus
  filas.

## §17. Pruebas (política mínima, constitución v1.2.0)

- **Domain**: `Category` (recorte, longitudes, descripción vacía → nula, `NameKey` sin acentos ni
  mayúsculas); `ShareMath.BasisPoints` (caso normal y total 0).
- **Casos de uso sobre SQLite real**:
  - nombre duplicado con mayúsculas, acentos y contra una inactiva;
  - desactivar con productos sin confirmar → `ConfirmationRequired`; con confirmación → inactiva;
  - eliminar con un producto inactivo → `CategoryInUse`; con solo productos borrados → se elimina y
    sus referencias quedan nulas;
  - asignar una categoría inactiva o borrada → error; conservar una inactiva sin cambiarla → se guarda;
  - "Ventas por categoría": suma igual al total sin filtro con descuentos de línea y global, devolución
    parcial, venta cancelada y productos sin categoría; suma de productos igual a su categoría;
    reclasificación mueve ventas pasadas;
  - reporte filtrado: total, cantidad de ventas y detalle de una venta con líneas de dos categorías;
  - inventario filtrado: tarjetas solo de la categoría.
- **Migración**: `ProductCategoriesMigrationTests` (de v0.10.0: productos intactos y sin categoría) y la
  prueba de bases de ejemplo existente con `v0.11.0.db`.
- **Arquitectura e inventario**: las pruebas obligatorias existentes, sin cambios de reglas.
- Sin pruebas de ViewModels, vistas, mapeos ni exportadores.
