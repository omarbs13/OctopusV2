# Quickstart: validar las alertas de bajo stock

**Feature**: [spec.md](spec.md) | **Contratos**: [contracts/](contracts/)

## Requisitos previos

- Compilar y probar desde la raíz:
  - `dotnet build -v q` (0 advertencias)
  - `dotnet test --verbosity quiet`
- Base con la migración `LowStockAlerts` aplicada (se aplica sola al arrancar).
- Usuarios: un Administrador y un Cajero; licencia con el módulo Inventario.
- Productos de prueba que controlan inventario, en piezas:

  | SKU | Mínimo | Reorden | Existencia |
  |---|---|---|---|
  | A-URG | 20 | 5 | 3 |
  | B-ALE | 20 | 5 | 12 |
  | C-OUT | 10 | — | 0 |
  | D-OK | 20 | 5 | 50 |

## Escenarios

1. **Validación de umbrales (H1)**
   - En el editor de D-OK, captura reorden 20 con mínimo 20 → "El punto de reorden debe ser menor
     que la existencia mínima.".
   - Captura reorden 2.5 en piezas → error de decimales.
   - Desmarca "Controla inventario" en un producto sin movimientos → el campo se oculta y se guarda
     vacío.
   - Revisa la bitácora: el cambio de "Punto de reorden" aparece en la auditoría del producto.
2. **Notificación al iniciar sesión (H2)**: entra como Administrador.
   - Aparece "Existencia urgente" con 1 producto (A-URG).
   - Aparece "Existencia en alerta" con 2 productos (B-ALE y C-OUT; C-OUT no tiene reorden).
   - Ninguna quita el foco: si abres el Punto de venta con F9, el campo de captura recibe el
     teclado.
3. **Navegación**: pulsa la urgente → "Reportes > Inventario" con fecha de hoy y filtro "Urgente",
   1 fila (A-URG) con la columna "Punto de reorden" en 5. La notificación se cierra.
4. **Descartar y no repetir**: pulsa "×" en la de alerta. Cierra la sesión y vuelve a entrar como
   Administrador → no aparece ninguna notificación.
5. **Otro usuario**: entra como Cajero → recibe las dos notificaciones.
6. **Escalamiento**: vende 8 piezas de B-ALE (queda en 4). Espera a la siguiente revisión, o
   reinicia la aplicación → aparece "Existencia urgente" con 2 productos. No reaparece la de
   alerta.
7. **Día siguiente**: adelanta el reloj del equipo un día, o borra las filas de hoy de
   `StockAlertAcknowledgements` en una copia de la base. Al iniciar sesión se notifican de nuevo los
   productos que sigan en nivel.
8. **Tarjeta de Inicio (H3)**:
   - "Alertas de existencia" muestra "Urgentes" (color de error) y "En alerta" (color de
     advertencia) con las mismas cifras que el reporte filtrado (SC-005).
   - "Existencia baja" ya no aparece; "Sin existencia" sí.
   - Pulsar cada cifra abre el reporte con su filtro.
9. **Sin permiso o sin licencia**: con un usuario sin `ViewInventory`, o con una licencia sin
   Inventario, no hay notificaciones ni tarjeta.
10. **Falla silenciosa**: con la base bloqueada por otro proceso durante una revisión, no aparece
    ningún error al operador y el log registra `RevisarAlertasDeExistencia`.
11. **Rendimiento (SC-004)**: en una copia de la base con 10 000 productos que controlan
    inventario (al menos 1 000 con umbrales), abre el Punto de venta y captura productos mientras
    se dispara una revisión (inicio de sesión, o reloj adelantado una hora). La captura no se
    detiene de forma perceptible y el log de `RevisarAlertasDeExistencia` muestra una duración
    menor a 1 s. Exporta "Reportes > Inventario" con el filtro "Urgente": el encabezado dice
    "Urgente".

## Pruebas automáticas relevantes

- `tests/Pos.Domain.Tests/Inventory/StockAlertRuleTests.cs`, `StockAlertDedupTests.cs`
- `tests/Pos.Domain.Tests/Products/ProductTests.cs` (punto de reorden)
- `tests/Pos.Infrastructure.Tests/Inventory/CheckStockAlertsTests.cs`, `StockAlertConsistencyTests.cs`
- `tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests.cs` (`v0.16.0.db`)
