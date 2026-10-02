# Categorías de productos: guía para soporte

Funcionalidad 016 (versión 0.11.0). Cubre el catálogo de categorías, la asignación de una categoría a cada
producto y los reportes por categoría. Forma parte del catálogo básico: no es un módulo licenciado. La
especificación completa está en [specs/016-product-categories](../specs/016-product-categories/spec.md).

## Conceptos

| Concepto | Qué es |
|---|---|
| **Categoría** (`Categories`) | Grupo de productos de un solo nivel, con nombre único y descripción opcional. |
| **Categoría inactiva** | Conserva sus productos y aparece en los reportes, pero no se ofrece al asignar categoría a otro producto. |
| **Sin categoría** | Agrupación de los productos sin categoría (`Products.CategoryId` nulo). No es una categoría registrada. |
| **Categoría vigente** | La que tiene el producto **al consultar**. La venta no guarda la categoría: reclasificar un producto mueve también sus ventas pasadas. |

## Pantallas y permisos

| Acción | Dónde | Permiso |
|---|---|---|
| Crear, editar, desactivar, reactivar y eliminar categorías | **Catálogos > Categorías** | `ManageProducts` (solo Administrador) |
| Asignar categoría a un producto | Formulario de producto, campo "Categoría" | `ManageProducts` |
| Ver la categoría y filtrar productos | Catálogos > Productos | `ViewProducts` |
| Filtro y "Ventas por categoría" en reportes | Reportes > Ventas e Inventario | Los de cada reporte (`ViewReports`, `ViewInventory`) |

No hay permisos nuevos. La especificación habla de "Productos > Categorías"; en la aplicación el catálogo de
productos vive en el grupo **Catálogos**, así que la pantalla está ahí, justo después de Productos. El punto
de venta no usa categorías.

## Reglas del catálogo

- **Nombre**: obligatorio, de 1 a 50 caracteres, se recorta. Es **único sin distinguir mayúsculas ni
  acentos**, también contra las inactivas ("Bebidas", "bebidas" y "  BEBÍDAS " son el mismo nombre). La
  unicidad la protege la columna `NameKey` (nombre sin acentos y en minúsculas) con el índice único filtrado
  `IX_Categories_NameKey ... WHERE "DeletedAt" IS NULL`: una categoría eliminada libera su nombre.
- **Descripción**: opcional, hasta 200 caracteres; vacía se guarda como nula.
- **Desactivar** una categoría con productos pide confirmación con el número de productos ("La categoría
  tiene 12 productos…"). La confirmación se exige en el caso de uso (`SetCategoryActive` devuelve
  `ConfirmationRequired`), no solo en la pantalla. Desactivar no quita la categoría a sus productos.
- **Eliminar** solo es posible si la categoría no tiene productos **no borrados** (activos o inactivos);
  si tiene, se rechaza con "No se puede eliminar: la categoría tiene N productos. Reasígnalos o
  desactívala". La eliminación es lógica (`DeletedAt`).
- Editar, desactivar y eliminar usan concurrencia optimista: si otro usuario cambió la categoría, se avisa
  "La categoría cambió desde que la abriste. Vuelve a abrirla".
- Cada alta, edición, desactivación, reactivación y eliminación queda en la bitácora con usuario y fecha:
  `CATEGORY_CREATED`, `CATEGORY_UPDATED`, `CATEGORY_DEACTIVATED`, `CATEGORY_ACTIVATED`, `CATEGORY_DELETED`.
  El log (Serilog) registra además el `CategoryId` y los rechazos (nombre repetido, conflicto, con productos).

## Asignación a productos

- Un producto tiene cero o una categoría. Sin categoría se guarda, se vende, ajusta inventario y aparece en
  los reportes igual que cualquier otro.
- El selector ofrece "Sin categoría" y las categorías **activas**. Si el producto ya tiene una inactiva,
  aparece como "{nombre} (inactiva)" y se conserva al guardar otros cambios; solo se puede cambiar a una
  activa o a "Sin categoría". Con más de 15 opciones se puede escribir para buscar.
- Al guardar un producto con una categoría distinta de la que tenía, `CreateProduct`/`UpdateProduct`
  verifican **dentro de una transacción de escritura** que exista, no esté borrada y esté activa; si no,
  devuelven `CategoryNotAssignable` ("La categoría elegida ya no está disponible. Elige otra").

## Integridad sin clave foránea

`Products.CategoryId` es una columna nula **sin `FOREIGN KEY`**. En SQLite, agregar una clave foránea a una
tabla existente obliga a EF Core a reconstruir `Products`, la tabla más referenciada de la base; además, como
la categoría se borra lógicamente, la clave foránea no impediría apuntar a una categoría borrada. La regla la
garantizan los casos de uso:

- `DeleteCategory` cuenta los productos no borrados y marca la categoría como borrada en una sola
  transacción `BEGIN IMMEDIATE`; asignar una categoría también va en una transacción de escritura. Como las
  escrituras están serializadas, no se pueden intercalar: **ningún producto no borrado apunta a una categoría
  borrada**. `SampleDatabaseUpgradeTests` verifica este invariante en las bases de ejemplo.
- En la misma transacción de la eliminación, los productos **borrados** que aún tenían la categoría quedan
  con `CategoryId = NULL`. Por eso **sus ventas pasadas pasan a agruparse en "Sin categoría"**: una
  categoría eliminada nunca aparece en los reportes.

Para revisar una base a mano:

```sql
-- Debe devolver 0.
SELECT COUNT(*) FROM Products p
WHERE p.DeletedAt IS NULL AND p.CategoryId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Categories c WHERE c.Id = p.CategoryId AND c.DeletedAt IS NULL);
```

## Reportes por categoría

Ver [reportes.md](reportes.md#categorías-016) para el filtro, la sección "Ventas por categoría", el cuadre con
descuentos y devoluciones y las formas de pago.
