# Quickstart: validar el módulo de ventas

Guía para comprobar la funcionalidad de punta a punta. El modelo está en
[data-model.md](data-model.md) y los contratos, en [contracts/](contracts/).

## Requisitos

- .NET 10 SDK.
- Repositorio en la rama `005-sales-module`.
- Una base de datos existente de 004 con productos que controlan inventario (opcional, para ver la
  migración con datos reales).

## 1. Compilar y probar

```bash
dotnet build -v q
dotnet test --verbosity quiet
```

**Esperado**: 0 errores, 0 advertencias y todas las pruebas en verde. Al implementar basta con
ejecutar las pruebas del proyecto modificado (Principio VI); por ejemplo:
`dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet`.

## 2. Pruebas de esta funcionalidad

Solo se prueban las reglas con cálculos, las validaciones de integridad y las pruebas obligatorias
(constitución v1.2.0). No hay pruebas de ViewModels.

| Prueba | Proyecto | Qué demuestra |
|---|---|---|
| Importe de línea | `Pos.Domain.Tests/Sales` | `LineAmount(0.333 kg, $10.05)` = $3.35 (3.34665 → 3.35) y `LineAmount(0.5 kg, $0.01)` = $0.01 (mitad hacia arriba). El total es la suma de las líneas (FR-028, SC-008) |
| Carrito | `Pos.Domain.Tests/Sales` | Agregar el mismo producto incrementa la línea. Una cantidad con decimales en pieza se rechaza y conserva el valor anterior. Un total que excede el máximo se rechaza (FR-003, FR-005) |
| Cobro | `Pos.Domain.Tests/Sales` | Total $85.50 con $100 en efectivo da $14.50 de cambio. $50 en efectivo más $150 con tarjeta sobre $200 da cambio 0. Con $150 pagados sobre $200 hay $50 de faltante y no se puede confirmar. Una tarjeta mayor que el pendiente se rechaza (US3, escenarios 1 a 5) |
| Existencia negativa | `Pos.Domain.Tests/Inventory` | `RecordSale` de 5 con existencia 3 deja −2. `AdjustOut` con existencia −2 se rechaza. `StockStatusRule` con −2 da `Out` (FR-025, FR-026) |
| Cancelación | `Pos.Domain.Tests/Sales` | `Cancel` sin motivo se rechaza y cancelar dos veces lanza excepción (FR-033, FR-035) |
| Consistencia de inventario (obligatoria) | `Pos.Infrastructure.Tests/Sales` | Con una secuencia reproducible de ventas (incluso con negativos), cancelaciones, entradas y ajustes en SQLite real, la existencia es igual a la suma con signo de los movimientos y se cumplen los invariantes 1 y 2 de [data-model.md](data-model.md#invariantes-verificadas-por-pruebas-principio-vi) (FR-027, SC-006) |
| Atomicidad | `Pos.Infrastructure.Tests/Sales` | Una falla forzada antes del commit de `ConfirmSale` deja 0 filas nuevas y el borrador intacto. Al reintentar se asigna el mismo folio (SC-003, FR-022) |
| Folio | `Pos.Infrastructure.Tests/Sales` | Dos confirmaciones concurrentes con `DraftId` distintos producen folios n y n+1 (SC-005) |
| Idempotencia | `Pos.Infrastructure.Tests/Sales` | Confirmar dos veces el mismo `DraftId` produce una sola venta y la segunda devuelve `AlreadyRegistered` (FR-020) |
| Borrador y venta | `Pos.Infrastructure.Tests/Sales` | Después de confirmar no queda borrador. Un `SaveSaleDraft` atrasado del mismo `DraftId` no lo revive (Edge Case de cierre durante la confirmación) |
| Copia de datos | `Pos.Infrastructure.Tests/Sales` | Cambiar nombre, SKU y precio del producto no altera `SaleLines` (SC-007) |
| Estado en SQL | `Pos.Infrastructure.Tests/Inventory` | El filtro "sin existencia" incluye los negativos y coincide con `StockStatusRule` (FR-026) |
| Inicio | `Pos.Infrastructure.Tests/Sales` | `GetSalesDashboard` excluye las canceladas, pone 0 en los días sin ventas y coincide con `SearchSales` para el mismo rango (SC-009) |
| Migración (obligatoria) | `Pos.Infrastructure.Tests/SampleDatabases` | `v0.1.0.db` a `v0.4.0.db` migran a la versión actual. La migración `SalesModule` no reconstruye tablas existentes (revisar el SQL generado) |
| Rendimiento | `Pos.Infrastructure.Tests/Sales` | Confirmar una venta de 50 líneas con 10,000 productos tarda menos de 2 s. Una página de 100 ventas con 50,000 ventas, filtrada, tarda menos de 2 s (SC-002) |
| Arquitectura (obligatoria) | `Pos.ArchitectureTests` | Las reglas existentes siguen pasando con los espacios `Sales` nuevos |

## 3. Recorrido manual

Preparación:

1. Ejecutar la app con `dotnet run --project src/Pos.Desktop`.
2. Crear tres productos:
   - A: pieza, con inventario, existencia 3, precio $10.00 y un código de barras.
   - B: kilogramo, con inventario, existencia 5, precio $85.00.
   - C: servicio, sin inventario, precio $50.00.

Recorrido:

1. **Captura (US1)**.
   - Pulsar **F9**; se abre "Punto de venta".
   - Escribir el código de A y Enter dos veces: una línea con cantidad 2 y total $20.00.
   - F2, "B", elegir B. F4, capturar `0.750`: importe $63.75.
   - En A, F4 y `1.5`: se rechaza con "Pieza no admite decimales".
   - Escribir `0000000000` y Enter: aviso de que no existe; la venta no cambia.
2. **Recuperación (US2)**.
   - Con líneas en la venta, terminar el proceso (`kill -9` o el Administrador de tareas).
   - Reabrir y pulsar F9: se ofrece recuperar con las mismas líneas.
   - Descartar: queda vacía.
   - Repetir y recuperar.
3. **Cobro (US3)**.
   - Agregar C; el total es $133.75 (A 2 × $10.00 + B $63.75 + C $50.00).
   - F12, efectivo, $150: cambio $16.25.
   - Enter confirma y se muestra "Venta V-000001 registrada · Cambio $16.25". El carrito queda
     vacío.
4. **Pago mixto y faltante**.
   - Nueva venta de $200: tarjeta $150 (referencia "1234") y efectivo $40, que muestra "Faltan
     $10.00" y no deja confirmar.
   - Cambiar a $50: confirma con cambio 0.
5. **Existencia negativa (US4)**.
   - Vender 5 de A (existencia 1): aparece la advertencia; al aceptarla se registra.
   - En Inventario → Existencias, A muestra −4 con el estado "sin existencia".
   - En Movimientos, A tiene "Salida por venta" con la referencia del folio.
6. **Doble pulsación**: en el cobro, pulsar Enter varias veces rápido. En Ventas realizadas hay una
   sola venta nueva.
7. **Ventas realizadas y cancelación (US5, US6)**.
   - Filtrar por hoy y por folio `2`.
   - Abrir el detalle y cancelar sin motivo: no se permite.
   - Cancelar con el motivo "Error de captura": la venta queda "Cancelada" y la existencia de los
     productos vuelve.
   - Intentar cancelar de nuevo: el botón ya no aparece.
8. **Inicio (US7)**: "Ventas del día" muestra la suma y el número sin la cancelada; la gráfica de
   7 días y la de productos más vendidos reflejan lo mismo.
9. **Solo táctil (SC-001)**: repetir el paso 3 usando solo los botones.
10. **Precio cambiado**:
    - Con A en la venta, cambiar su precio en Productos y volver.
    - Al pulsar F12, el total se actualiza con el precio nuevo antes de abrir el cobro.

## 4. Migración de una base existente

1. Copiar una base de 004 a la carpeta de datos y arrancar.
2. **Esperado**:
   - Se crea el respaldo previo a la migración.
   - Aparecen las tablas `Sales`, `SaleLines`, `SalePayments`, `SaleDrafts` y `AuditEntries`.
   - Los productos, existencias y movimientos quedan sin cambios.
   - `__EFMigrationsHistory` incluye `SalesModule`.
