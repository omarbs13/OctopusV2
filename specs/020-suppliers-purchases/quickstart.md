# Quickstart: validar proveedores y compras

Guía para comprobar de punta a punta la spec [020-suppliers-purchases](spec.md). Los contratos están
en [contracts/](contracts/) y el modelo en [data-model.md](data-model.md).

## §1 Preparación

```bash
dotnet build -v q
dotnet test --verbosity quiet
```

- Arranca la aplicación con una base que venga de la versión 0.13.0. La migración
  `SuppliersAndPurchases` se aplica sola, después del respaldo automático.
- La licencia debe tener activo el módulo Inventario.
- Inicia sesión como Administrador. Ten un Cajero y estos productos que controlan inventario:
  - "Refresco" (pieza) con existencia 10.
  - "Queso" (kg).
  - "Producto sin inventario" (no controla inventario) y "Producto inactivo".

## §2 Historia 1: catálogo de proveedores

1. **Inventario > Proveedores > Nuevo**: "Distribuidora Norte", RUC "abc123 ", crédito a 30 días,
   email válido. Guarda.
2. Crea "Abarrotes Sur" solo con nombre y "Contado".
3. Intenta crear otro con RUC "ABC123": se rechaza indicando "Distribuidora Norte".
4. Intenta guardar con email "correo@" y con "Crédito" sin días o con 0: errores por campo.
5. Busca "norte" y "abc123": aparece "Distribuidora Norte".
6. Edita su teléfono y pásalo a "Contado": se guarda y los días desaparecen.
7. Desactiva "Abarrotes Sur": desaparece de la lista; con "Incluir inactivos" aparece atenuado.
   Reactívalo y vuelve a desactivarlo para el §4.

**Esperado**: en **Administración > Auditoría**, grupo "Proveedores y compras": creado, modificado (solo
teléfono y condiciones), desactivado y activado.

## §3 Historia 2: registrar compra

1. **Inventario > Entrada de mercancía**, proveedor "Distribuidora Norte", factura "F-100", fecha de
   hoy.
2. Agrega "Refresco" 5 a $12.50 y "Queso" 2.5 a $40.00; impuestos $26.00.
   - **Esperado en pantalla**: importes $62.50 y $100.00, subtotal $162.50, total $188.50.
3. Vuelve a agregar "Refresco": no se crea otra línea, se enfoca la existente.
4. Busca "Producto sin inventario" y "Producto inactivo": no aparecen.
5. Registra la compra.

**Esperado**:

- Existencia de "Refresco" = 15. Precio y demás datos del producto sin cambios.
- **Inventario > Movimientos**: dos "Entrada de compra" con referencia "Factura F-100 · Distribuidora
  Norte", tu usuario y la existencia resultante. Filtrar por "Entrada" (manual) no las muestra.
- Bitácora: "Compra registrada" con subtotal, impuestos y total.

**Validaciones** (cada una se rechaza y no crea movimientos):

| Captura | Resultado |
|---|---|
| Mismo proveedor y factura " f-100" | Rechazo: factura ya registrada, con enlace a la compra |
| "Abarrotes Sur" (inactivo) | No aparece en el selector |
| Fecha de mañana | Error en la fecha |
| Sin líneas, sin factura o sin proveedor | Errores en cada campo |
| "Refresco" 1.5 piezas, cantidad 0 o costo −1 | Error en la línea indicada |
| Impuestos −1 | Error en impuestos |
| Todas las líneas a $0.00 | Rechazo: subtotal $0.00 |

**Bonificación**: con otro proveedor (o otra factura), "Refresco" 10 a $15.00 y "Queso" 2 a $0.00.
Se acepta, la segunda línea dice "Bonificación", el subtotal es $150.00 y ambas suman existencia.

**Costo por compra**: registra otra compra de "Refresco" a $13.00. El detalle de la primera sigue en
$12.50.

**Redondeo**: 3 a $10.00 y "Queso" 1.255 a $20.00, impuestos $8.82 → $30.00, $25.10, subtotal $55.10,
total $63.92.

**Cajero**: inicia sesión como Cajero; el menú Inventario no muestra "Entrada de mercancía" ni
"Proveedores", y Reportes no muestra "Compras".

## §4 Anulación

1. Con "Refresco" en existencia suficiente, abre la compra "F-100" desde **Reportes > Compras** y pulsa
   **Anular compra** sin motivo: se pide el motivo.
2. Captura "Factura con error" y confirma.
   - **Esperado**: "Refresco" baja 5; kárdex con "Anulación de compra"; la compra muestra "Anulada",
     fecha, usuario y motivo; bitácora "Compra anulada" con el motivo.
3. Intenta anularla otra vez: "La compra ya está anulada".
4. Registra de nuevo "Distribuidora Norte" / "F-100": se acepta.
5. Registra una compra de 5 piezas, vende hasta dejar existencia 2 e intenta anularla: se rechaza
   indicando el producto y su existencia; nada cambia.
6. Desactiva un producto de una compra vigente e intenta anularla: se rechaza pidiendo reactivarlo.

## §5 Historia 3: reporte de compras

1. Registra tres compras de dos proveedores con fechas de factura distintas.
2. **Reportes > Compras**: filtra por un proveedor y un rango de fechas.
   - **Esperado**: solo las compras esperadas, de la más reciente a la más antigua; número de compras,
     subtotal, impuestos y total acumulados exactos.
3. Filtra por "Abarrotes Sur" (inactivo): está en el filtro y sus compras aparecen.
4. Las anuladas no aparecen; con "Incluir anuladas" aparecen marcadas y **no** cambian los
   acumulados.
5. "Desde" posterior a "Hasta", o mínimo mayor que máximo: mensaje de filtro inválido.
6. Un rango sin compras: "No hay compras con estos filtros." y $0.00.
7. Abre una compra: líneas con producto, cantidad, unidad, costo e importe tal como se registraron.
   Renombra el producto y el proveedor: el detalle conserva los nombres originales.

## §6 Pruebas automáticas

```bash
dotnet test tests/Pos.Domain.Tests --verbosity quiet
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet
dotnet test tests/Pos.ArchitectureTests --verbosity quiet
```

Deben pasar:

- Domain: `PurchaseMathTests`, `PurchaseTests`, `SupplierTests`, `ProductStockTests` (ampliada),
  `RolePermissionsTests`.
- Infrastructure: `RegisterPurchaseTests`, `PurchaseAtomicityTests`, `PurchaseConcurrencyTests`,
  `VoidPurchaseTests`, `PurchaseImmutabilityTests`, `PurchaseReportTests`, `SupplierUseCaseTests`.
- Obligatorias: `InventoryConsistencyTests` (con compras y anulaciones),
  `SuppliersAndPurchasesMigrationTests` y `SampleDatabaseUpgradeTests` (con `v0.13.0.db` y
  `v0.14.0.db`), pruebas de arquitectura.

## §7 Rendimiento (SC-005, antes de publicar)

```bash
dotnet run --project tests/Pos.Infrastructure.Tests -c Release -- -class "Pos.Infrastructure.Tests.Purchases.PurchaseReportPerformanceTests" -explicit only
```

La prueba siembra 10,000 compras y mide la primera página con cada filtro y todos juntos.

**Esperado**: cada consulta tarda menos de 2 s. Si alguna pasa de 300 ms, revisa el plan de la
consulta (`EXPLAIN QUERY PLAN`) antes de publicar.
