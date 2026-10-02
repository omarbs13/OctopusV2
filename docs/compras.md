# Proveedores y compras: guía para soporte

Funcionalidad 020 (versión 0.14.0). Cubre el catálogo de proveedores, el registro de compras como entrada
de mercancía, su anulación, los movimientos de compra en el kárdex y el reporte "Compras". Pertenece al
módulo **Inventario** de la licencia (012). Todo lo hace el Administrador.

## Conceptos

| Concepto | Qué es |
|---|---|
| **Proveedor** (`Suppliers`) | Nombre, RUC opcional (único), teléfono, email, dirección y condiciones de pago (`CASH` / `CREDIT` con 1–365 días). Se activa y desactiva; nunca se borra. |
| **Compra** (`Purchases`) | Una factura de proveedor: proveedor, número y fecha de factura, subtotal, impuestos y total en centavos. Estado `ACTIVE` ("Vigente") o `VOIDED` ("Anulada"). Nunca se edita ni se borra. |
| **Línea de compra** (`PurchaseLines`) | Producto, cantidad en milésimas, costo unitario antes de impuestos e importe. Enlaza su movimiento `PURCHASE` (`MovementId`) y, si se anuló, su `PURCH_VOID` (`VoidMovementId`). |

La compra guarda copias del nombre del proveedor y del nombre, SKU y unidad de cada producto: el detalle y
el reporte muestran los datos como estaban al registrar, aunque después se editen.

Registrar una compra **no cambia el producto**: ni precio de venta ni ningún otro dato. El costo queda solo
en la línea de la compra.

## Proveedores

**Inventario > Proveedores** (`ManageSuppliers`):

- Búsqueda por nombre o RUC, sin acentos ni mayúsculas. "Incluir inactivos" muestra los desactivados.
- El nombre puede repetirse; el RUC no, ni siquiera contra un proveedor inactivo. Se guarda recortado y en
  mayúsculas: "abc123 " y "ABC123" son el mismo. El mensaje dice qué proveedor ya lo tiene.
- Con "Crédito", los días de crédito son obligatorios (1 a 365). Al pasar a "Contado" se borran.
- Desactivar siempre se permite. Un proveedor inactivo no aparece al registrar compras, pero sus compras
  y el filtro del reporte lo siguen mostrando.

## Registrar una compra

**Inventario > Entrada de mercancía** (`RegisterPurchases`):

1. Proveedor (solo activos), número de factura (hasta 50 caracteres) y fecha de factura (no futura; por
   omisión hoy).
2. Agregar productos con el buscador: Enter agrega el primero y lleva a la cantidad; Tab pasa al costo y
   Enter regresa al buscador. Solo aparecen productos activos que controlan inventario. Un producto ya
   capturado no se repite: se enfoca su línea.
3. Cantidad (con los decimales de la unidad del producto) y costo unitario antes de impuestos.
4. Impuestos: un solo importe; vacío = $0.00.

Los importes los calcula el sistema en cada cambio:

- Importe de línea = cantidad × costo, redondeado "mitad hacia arriba" al centavo (la misma regla que la
  venta).
- Subtotal = suma de los importes de línea ya redondeados. Total = subtotal + impuestos.
- Línea, subtotal, impuestos y total no pueden exceder $999,999.99. Una factura mayor se registra en dos
  compras.

Al guardar, en **una sola transacción**: la compra, sus líneas, un movimiento `PURCHASE` por línea (suma
existencia, con la factura como referencia), los enlaces y la entrada de bitácora `PURCHASE_REGISTERED`.
Si algo falla no queda nada guardado y la captura se conserva en pantalla para reintentar.

Si hay errores, se muestran todos juntos en sus campos: proveedor inactivo, producto inactivo o que ya no
controla inventario, decimales de más, factura duplicada, etc.

### Bonificaciones

Una línea con costo **$0.00** es una bonificación (producto regalado): suma existencia y se marca
"Bonificación". La compra se rechaza si **todas** las líneas son bonificación (subtotal $0.00).

### Factura duplicada

No puede haber dos compras **vigentes** del mismo proveedor con el mismo número de factura. Se compara sin
espacios de los extremos y sin distinguir mayúsculas: "F-100" y " f-100" son la misma. El mensaje indica
la fecha de la compra existente y ofrece "Ver compra".

- El mismo número con **otro** proveedor sí se permite.
- Al anular una compra, su número queda libre para registrarla de nuevo.
- El índice único `IX_Purchases_Supplier_InvoiceKey` (filtrado `WHERE "Status" = 'ACTIVE'`) es la última
  barrera si dos equipos guardan la misma factura a la vez.

## Anular una compra

Desde el detalle de la compra, botón **"Anular compra"** (`VoidPurchases`, solo Administrador, sin
autorización de un Cajero). Se anula completa, con motivo obligatorio (hasta 250 caracteres), en una sola
transacción: un movimiento `PURCH_VOID` por línea (resta existencia), la compra queda `VOIDED` con fecha,
usuario y motivo, y la bitácora registra `PURCHASE_VOIDED`. No hay regreso a vigente.

La anulación **nunca deja la existencia bajo cero**. Se rechaza sin cambiar nada si alguna línea:

| Causa | Mensaje | Qué hacer |
|---|---|---|
| Existencia menor que la cantidad comprada | "{producto}: existencia {actual}, se requieren {cantidad}" | Registrar primero un ajuste o una entrada, o revisar ventas posteriores |
| Producto inactivo (o borrado) | "{producto}: está inactivo; actívelo primero" | Activar el producto |
| Producto que ya no controla inventario | "{producto}: ya no controla inventario" | Volver a activar el control de inventario |

El mensaje lista **todas** las líneas que lo impiden. Una venta y una anulación simultáneas se serializan:
la anulación lee la existencia ya afectada por la venta.

### Cómo corregir una compra

Las compras no se editan. Para corregir un error (cantidad, costo, factura, proveedor): **anular** la
compra con un motivo que explique la corrección y **registrarla de nuevo** con los datos correctos. Como la
factura queda libre al anular, se puede usar el mismo número.

## Kárdex

| Tipo (`InventoryMovements.Type`) | Etiqueta | Signo | Origen |
|---|---|---|---|
| `PURCHASE` | Entrada de compra | + | `RegisterPurchase`, uno por línea |
| `PURCH_VOID` | Anulación de compra | − | `VoidPurchase`, uno por línea |

Son distintos de "Entrada" (`RECEIPT`) y no se pueden registrar a mano desde "Registrar movimiento". La
referencia del movimiento es el número de factura; el kárdex muestra "Factura {número} · {proveedor}" y,
con `ViewPurchaseReport` o `RegisterPurchases`, abre el detalle de la compra. El enlace vive en
`PurchaseLines`, no en `InventoryMovements`.

Consulta para verificar que cada línea tiene su movimiento y que solo las anuladas tienen el de anulación
(debe devolver 0 filas):

```sql
SELECT l.Id, p.InvoiceNumber, p.Status
FROM PurchaseLines l JOIN Purchases p ON p.Id = l.PurchaseId
LEFT JOIN InventoryMovements m ON m.Id = l.MovementId AND m.Type = 'PURCHASE'
LEFT JOIN InventoryMovements v ON v.Id = l.VoidMovementId AND v.Type = 'PURCH_VOID'
WHERE m.Id IS NULL OR (p.Status = 'VOIDED') <> (v.Id IS NOT NULL);
```

Y que los importes guardados cuadran con las líneas (debe devolver 0 filas):

```sql
SELECT p.Id, p.InvoiceNumber FROM Purchases p
WHERE p.SubtotalCents <> (SELECT SUM(AmountCents) FROM PurchaseLines WHERE PurchaseId = p.Id)
   OR p.TotalCents <> p.SubtotalCents + p.TaxCents
   OR p.LineCount <> (SELECT COUNT(*) FROM PurchaseLines WHERE PurchaseId = p.Id);
```

## Reporte "Compras"

**Reportes > Compras** (`ViewPurchaseReport`):

- Filtros: proveedor (incluye inactivos, marcados "(inactivo)"), fecha de factura desde / hasta
  (inclusivas), total mínimo / máximo (con impuestos) e "Incluir anuladas". Botón "Aplicar".
- Un filtro inválido (fecha inicial posterior a la final, mínimo mayor que el máximo, importe negativo o mal
  escrito) se marca en su campo y no muestra resultados.
- 100 compras por página, de la fecha de factura más reciente a la más antigua.
- El resumen (número de compras, subtotal, impuestos y total) es de **todo el filtro**, no solo de la
  página, y cuenta **solo compras vigentes**. Las anuladas se listan en gris con "Anulada" cuando se piden,
  pero no se suman.
- Doble clic o Enter abre el detalle: datos guardados, quién y cuándo la registró, importes, líneas con
  "Bonificación" y, si está anulada, fecha, usuario y motivo.

## Permisos

| Permiso | Uso |
|---|---|
| `ManageSuppliers` | Inventario > Proveedores |
| `RegisterPurchases` | Inventario > Entrada de mercancía; también abre el detalle de una compra |
| `VoidPurchases` | Anular compras |
| `ViewPurchaseReport` | Reportes > Compras y el detalle de una compra |

Los cuatro son solo del Administrador, ninguno es autorizable y pertenecen al módulo **Inventario**: sin
esa licencia no aparecen en el menú. `RegisterPurchases` es independiente de `RegisterMovements`. Ver
[usuarios-y-permisos.md](usuarios-y-permisos.md).

## Bitácora y registro de errores

| Evento | Contenido |
|---|---|
| `SUPPLIER_CREATED`, `SUPPLIER_UPDATED`, `SUPPLIER_DEACTIVATED`, `SUPPLIER_ACTIVATED` | Campos del proveedor que cambiaron |
| `PURCHASE_REGISTERED` | "Compra {factura} · {proveedor}": proveedor, factura, fecha, líneas, subtotal, impuestos y total |
| `PURCHASE_VOIDED` | El motivo y el cambio de estado "Vigente" → "Anulada" |

En el registro de la aplicación (Serilog) quedan el registro y la anulación con proveedor, factura,
número de líneas, total y usuario, y los rechazos de anulación con los productos que los causaron. Ver
[auditoria.md](auditoria.md).
