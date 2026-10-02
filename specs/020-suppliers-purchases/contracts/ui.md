# Contrato: interfaz (Pos.Desktop)

Todos los textos van en español en `Resources/Strings.resx` (prefijos `Supplier_`, `Purchase_`,
`Nav_Suppliers`, `Nav_PurchaseEntry`, `Nav_ReportPurchases`, `MovementType_Purchase*`). Los ViewModels
solo invocan los casos de uso de [application-ports.md](application-ports.md); no calculan importes,
subtotales, impuestos ni totales (Principio III).

## Navegación

| Grupo / página | Id | Orden | Permiso | Licencia |
|---|---|---|---|---|
| Inventario > Entrada de mercancía | `inventory.purchases` | 20 | `RegisterPurchases` | `Inventory` |
| Inventario > Proveedores | `inventory.suppliers` | 30 | `ManageSuppliers` | `Inventory` |
| Reportes > Compras | `reports.purchases` | 25 | `ViewPurchaseReport` | `Inventory` |

- Las dos primeras se registran en `PurchasesModule.AddPurchasesModule()` dentro del grupo
  `InventoryModule.GroupId`; el reporte, en `ReportsModule`. `HostBuilder` agrega
  `AddPurchasesModule`.
- Sin el permiso o sin la licencia, la opción no aparece (escenario 16, caso límite de licencia).

## Proveedores: lista

- Búsqueda mientras se escribe, con retardo, por nombre o RUC.
- Casilla "Incluir inactivos" (FR-004). Los inactivos se muestran atenuados.
- Columnas: nombre, RUC, teléfono, condiciones de pago ("Contado" / "Crédito 30 días") y estado.
- Botones: "Nuevo proveedor", "Editar" (doble clic o Enter) y "Desactivar" / "Activar".

## Proveedores: alta y edición (formulario de 002)

- **Campos**: nombre*, RUC, teléfono, email, dirección (multilínea) y condiciones de pago*
  ("Contado" / "Crédito").
- "Días de crédito" se habilita y es obligatorio solo con "Crédito" (escenario 7).
- **Errores por campo**: nombre vacío o largo; email inválido; días vacíos, 0 o > 365; RUC repetido
  con "El RUC ya está registrado para el proveedor {nombre}" (escenario 2).
- **Conflicto de versión**: mensaje estándar de recarga.

## Inventario > Entrada de mercancía

Pantalla de captura de una compra:

- **Encabezado**:
  - Proveedor* (selector con búsqueda; solo activos).
  - Número de factura* (máx. 50).
  - Fecha de factura* (selector de fecha; por omisión hoy; no permite fechas futuras).
- **Agregar producto**: buscador reutilizando `ProductPickerView` (nombre, SKU o código de barras;
  solo productos activos que controlan inventario). Si el producto ya está en la compra, enfoca su
  línea y selecciona la cantidad (escenario 10).
- **Tabla de líneas**: producto (nombre y SKU), unidad, cantidad*, costo unitario antes de impuestos*,
  importe (calculado), etiqueta "Bonificación" cuando el costo es $0.00, botón quitar línea.
  - Los errores de línea se marcan en la celda con su mensaje (escenario 7).
- **Pie**: Subtotal (calculado), Impuestos (capturado; vacío = $0.00), **Total** (calculado).
  - Los importes vienen de `CalculatePurchaseTotals` en cada cambio.
- **Botones**:
  - "Registrar compra": se deshabilita mientras guarda. Al terminar muestra "Compra registrada:
    {proveedor} factura {número}, total {total}" y limpia la captura.
  - "Descartar": pide confirmación si hay líneas.
- **Errores**:
  - `ValidationFailed`: marca campos y líneas; la captura se conserva.
  - `DuplicateInvoice`: mensaje con la fecha de la compra existente y enlace "Ver compra".
  - Falla inesperada: mensaje genérico sin detalles técnicos; **la captura se conserva**
    (escenario 12).
- **Teclado (SC-003)**: Enter en el buscador agrega y lleva a cantidad; Tab a costo; Enter regresa al
  buscador.

## Detalle de compra (vista compartida)

Se abre desde "Reportes > Compras" y desde el kárdex (movimiento con `PurchaseId`).

- **Encabezado**: proveedor (nombre guardado), factura, fecha de factura, registrada por y fecha y
  hora de registro (hora local), subtotal, impuestos, total.
- Si está anulada: insignia "Anulada", fecha y hora, usuario y motivo (Historia 3, escenario 8).
- **Líneas**: núm., producto, SKU, cantidad, unidad, costo unitario, importe, "Bonificación".
- Sin opciones para editar, borrar ni anular líneas (escenario 11).
- **"Anular compra"**: visible solo con `VoidPurchases` y compra vigente. Abre un diálogo con motivo*
  (máx. 250) y el texto "Se descontarán de la existencia las cantidades de cada línea". Errores:
  - `PurchaseVoidBlocked`: lista de productos con su causa (escenario 14);
  - `InvalidState`: "La compra ya está anulada" (escenario 15);
  - `Conflict`: recargar el detalle.

## Reportes > Compras

- **Filtros**: proveedor (incluye inactivos, marcados "(inactivo)"), fecha de factura desde / hasta,
  total mínimo / máximo, casilla "Incluir anuladas". Botón "Aplicar".
  - Filtro inválido: mensaje en el campo y sin resultados (FR-023).
- **Resumen** (sobre todo el filtro): número de compras, subtotal, impuestos y total acumulados
  (FR-021).
- **Tabla** (100 por página, de la más reciente a la más antigua): fecha de factura, proveedor,
  factura, líneas, subtotal, impuestos, total, usuario. Las anuladas en gris con insignia "Anulada".
- Sin resultados: "No hay compras con estos filtros." y acumulados $0.00 (escenario 6).
- Doble clic o Enter abre el detalle.

## Inventario > Movimientos (kárdex)

- `MovementTypeLabels`: "Entrada de compra" (`Purchase`) y "Anulación de compra" (`PurchaseVoid`),
  distintas de "Entrada" (FR-014).
- El filtro por tipo los ofrece.
- Columna "Referencia": "Factura {número} · {proveedor}" en los movimientos de compra; abre el
  detalle de la compra con `ViewPurchaseReport` o `RegisterPurchases`.
