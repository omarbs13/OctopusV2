# Research: Alertas inteligentes de bajo stock

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Fecha**: 2026-10-02

No quedaron `NEEDS CLARIFICATION` en el contexto técnico. Cada sección registra una decisión de
diseño tomada a partir del código existente (004, 009, 018, 012) y de la constitución v1.2.0.

## §1. Dónde vive el punto de reorden

- **Decision**: columna nueva `Products.ReorderPoint` (`INTEGER NULL`, milésimas), propiedad
  `Product.ReorderPointThousandths` / `ReorderPoint` (`Quantity?`), junto a `MinimumStock`.
  `Product.Create/Update` reciben `reorderPoint`; `ApplyInventory` valida y lo guarda nulo si
  `TracksInventory = false` (FR-003).
- **Rationale**: es configuración del producto, igual que `MinimumStock` (004 research §2). Agregar
  una columna nula a `Products` es un `ALTER TABLE ADD COLUMN` en SQLite, sin reconstruir la tabla.
- **Alternatives considered**: tabla `ProductStockThresholds` aparte (más uniones en cada consulta
  de existencias sin beneficio); guardarlo en `ProductStocks` (esa fila solo existe con movimientos).

## §2. Validación de los umbrales

- **Decision**: predicado estático `Product.IsValidReorderPoint(Quantity? reorder, Quantity? minimum,
  UnitOfMeasure unit)`: nulo es válido; si existe, respeta los decimales de la unidad, no supera
  `MaxCaptureThousandths` y, si hay mínimo, es **estrictamente menor** que él (FR-002). 0 es válido
  (urgente solo al agotarse). En Application, `ProductRules` agrega `ParseReorderPoint` con
  `Quantity.Parse(..., allowZero: true)` y la regla de relación, con el campo `ReorderPoint` y el
  mensaje "El punto de reorden debe ser menor que la existencia mínima.".
- **Rationale**: misma forma que `IsValidMinimumStock`; el dominio lanza `DomainException` como
  red de seguridad y el validador da el mensaje junto al campo (Principio III).
- **Alternatives considered**: permitir igualdad (mínimo = reorden): el nivel "alerta" nunca
  existiría para ese producto; se descarta porque la clarificación pide estrictamente menor.

## §3. Regla de nivel

- **Decision**: enum `StockAlertLevel { None, Alert, Urgent }` y `StockAlertRule.Evaluate(StockLevel
  onHand, Quantity? minimum, Quantity? reorderPoint)` en `Domain/Inventory`:
  - `Urgent` si hay reorden y `onHand <= reorden`;
  - `Alert` si hay mínimo y `onHand <= mínimo`;
  - `None` en otro caso.
  Es independiente de `StockStatusRule` (clarificación 4): un producto en 0 es `Out` y además
  `Urgent`/`Alert`. Los productos inactivos se tratan como `None` (spec, Edge Cases).
- **Rationale**: regla pura y probable en Domain. Infrastructure replica el predicado en SQL para
  contar, como ya se hace con `StockStatusRule` (004 research §8), y una prueba de consistencia
  compara ambos.
- **Alternatives considered**: ampliar `StockStatus` con `Urgent`: mezclaría dos ejes (agotado vs
  umbral) y rompería los filtros y conteos existentes de 004 y 009.

## §4. Registro de lo notificado

- **Decision**: tabla técnica `StockAlertAcknowledgements` con `Id` (GUID v7), `UserId`,
  `ProductId`, `Level` (INTEGER), `LocalDate` (TEXT `yyyy-MM-dd`) y `CreatedAt` (UTC). Índice único
  `(UserId, LocalDate, Level, ProductId)`. Se escribe una fila por producto incluido **cuando se
  muestra** la notificación (mostrar = notificado; descartar no escribe nada más, FR-011).
  En la misma transacción se borran filas con `LocalDate` de más de 7 días.
- **Rationale**:
  - Por usuario (clarificación 2) y persistente entre reinicios (FR-014).
  - Por producto y no por conteo: así una revisión posterior detecta productos nuevos en el nivel
    y el escalamiento alerta → urgente (FR-009).
  - `LocalDate` en texto con la fecha local evita recalcular la medianoche local en cada consulta;
    la constitución pide fechas en UTC, y `CreatedAt` lo cumple; `LocalDate` es una clave de día,
    no un instante.
  - Sin columnas de auditoría ni borrado lógico, como `SaleDrafts`: no tiene valor histórico ni
    contable y se purga (ver Complexity Tracking del plan).
- **Alternatives considered**:
  - `IPreferencesStore` (archivo local por máquina): no es transaccional y mezclaría datos por
    usuario con preferencias de la máquina.
  - Guardar solo "última fecha notificada por usuario y nivel": no detecta productos que entran al
    nivel más tarde el mismo día.
  - Guardar en memoria: se perdería al reiniciar (FR-014).

## §5. Qué productos disparan una notificación

- **Decision**: regla pura `StockAlertDedup.IsPending(level, acknowledgedToday)` en Domain:
  - `Urgent` es pendiente si hoy no se registró `Urgent` para ese producto;
  - `Alert` es pendiente si hoy no se registró **ningún** nivel para ese producto (urgente → alerta
    no notifica).
  Hay notificación de un nivel si hay al menos un producto pendiente en él. Al mostrarla se
  registran **todos** los productos que hoy están en ese nivel (no solo los pendientes), y el
  mensaje muestra el total del nivel (FR-008).
- **Rationale**: concentra en una regla probada el criterio "una por día máximo" con escalamiento.
- **Alternatives considered**: notificar solo los productos nuevos con su propio conteo: el número
  no coincidiría con la tarjeta ni con el reporte (SC-005).

## §6. Caso de uso de revisión

- **Decision**: `CheckStockAlerts` en `Application/Inventory/CheckStockAlerts/`:
  1. Exige `ViewInventory` (incluye la licencia del módulo Inventario, `ModuleAccess`); sin permiso o
     sin licencia devuelve `Forbidden`/`ModuleNotLicensed` y el monitor no muestra nada.
  2. Calcula la fecha local con `IClock` + zona local (`ReportPeriodResolver.ToLocalDate`).
  3. Abre `IWriteTransactions`, lee los niveles actuales (`IStockAlertStore.GetCandidatesAsync`: Id,
     existencia actual, mínimo, reorden de productos activos que controlan inventario) y los
     registros de hoy del usuario, evalúa con `StockAlertRule` y `StockAlertDedup`, inserta los
     registros, purga los viejos y confirma.
  4. Devuelve `StockAlertCheck(UrgentCount, AlertCount, NotifyUrgent, NotifyAlert)`.
- **Rationale**: una sola transacción (Principio I); la evaluación en el caso de uso con reglas de
  Domain (Principio III), igual que `GetReportAlerts` (009).
- **Alternatives considered**: evaluar en SQL: duplicaría la regla de deduplicación fuera del
  dominio.

## §7. Conteos de la tarjeta

- **Decision**: ampliar `StockAlertCounts` con `Alert` y `Urgent` y `IInventoryRepository.
  CountAlertsAsync` con un predicado SQL compartido `FilterByAlertLevel` (mismo archivo que
  `FilterByStatus`). `GetStockAlerts` no cambia de firma.
- **Rationale**: la tarjeta se recalcula cada vez que se muestra Inicio con una consulta de conteo
  sobre índices existentes (`IX_Products_TracksInventory`); 10 000 productos se cuentan en
  milisegundos (SC-004).
- **Alternatives considered**: reutilizar `CheckStockAlerts`: escribe registros; la tarjeta debe ser
  de solo lectura (FR-019).

## §8. Reporte "Reportes > Inventario"

- **Decision**:
  - `StockFilter` agrega `Alert` y `Urgent`. `InventoryReportReader` calcula `Level` por fila con
    `StockAlertRule` (con la existencia a la fecha del reporte y los umbrales actuales; productos
    inactivos = `None`) y filtra por él.
  - `InventoryReportRow` agrega `ReorderPointThousandths` y `Level`; la tabla muestra la columna
    "Punto de reorden" y la exportación (PDF/XLSX) la incluye.
  - `InventoryCounts` agrega `Alert` y `Urgent` (solo informativos; las tarjetas del reporte no
    cambian).
  - `InventoryReportViewModel` implementa `INavigationArgumentReceiver`: con un `StockFilter`,
    fija la fecha en hoy y el filtro de estado, y recarga.
- **Rationale**: con fecha = hoy, la existencia del reporte es la actual, así que las filas coinciden
  con la tarjeta (SC-005). El filtro por estado existente se conserva (FR-016).
- **Alternatives considered**: filtro de nivel separado del filtro de estado: dos combos para algo
  que el usuario ve como "estado"; se agrega a la misma lista con las etiquetas "En alerta" y
  "Urgente".

## §9. Monitor de revisiones (Desktop)

- **Decision**: `StockAlertMonitor` en `Desktop/Inventory/`, servicio del ámbito de sesión:
  - `RootViewModel.OpenSessionAsync` llama a `monitor.Start()` después de mostrar la ventana
    principal; al desechar el ámbito se detiene.
  - Hace la primera revisión al iniciar y luego un `DispatcherTimer` de 1 hora con prioridad
    `Background`.
  - Cada revisión corre con `OperationRunner.RunQuietlyResultAsync` (log sin mensaje, FR-015) y no
    se solapa con otra.
  - Con permisos sin `ViewInventory` no se inicia (FR-013).
- **Rationale**: misma forma que `IdleMonitor` y `LicenseClockScheduler`. El ámbito por sesión
  garantiza la revisión al iniciar sesión y al cambiar de usuario.
- **Alternatives considered**: `System.Threading.Timer` en el host: correría sin sesión y fuera del
  hilo de UI.

## §10. Notificaciones en pantalla

- **Decision**: `NotificationCenter` (Desktop/Shell, ámbito de sesión) con una lista observable de
  `NotificationItem` (`Severity`, `Title`, `Message`, `NavigateTo`, `Argument`, `Key`). Como
  máximo una por `Key` (`stock.urgent`, `stock.alert`): una nueva del mismo nivel reemplaza a la
  visible. `MainView` las muestra en una pila abajo a la derecha, encima del contenido y debajo del
  modal. Los botones tienen `Focusable="False"` y no se roba el foco (FR-012). Pulsar navega con
  `Navigator.NavigateAsync(ReportsModule.InventoryPageId, filtro)` y la cierra; "×" la descarta.
  Colores: `NotificationSeverityBrushConverter` en `Desktop/Shell` (advertencia y error, mismos tonos que
  `StockStatusBrushConverter`, sin que el shell dependa de inventario), más el texto del nivel.
- **Rationale**: no hay infraestructura de notificaciones; un centro genérico y pequeño en el
  shell sirve a esta y a futuras funcionalidades sin dependencias externas (Principio VII).
- **Alternatives considered**: notificaciones del sistema operativo (no portables entre Windows y
  Linux sin dependencias, Principio V); diálogo modal (interrumpe la venta, Principio I).

## §11. Tarjeta de Inicio con dos cifras

- **Decision**: `DashboardCard` agrega `Segments` (`IReadOnlyList<DashboardCardSegment>`: `Label`,
  `Value`, `Tone` = Neutral/Warning/Danger, `NavigateTo`, `Argument`). La plantilla de indicadores
  de `HomeView`, cuando hay segmentos, dibuja una fila de botones (uno por segmento) en lugar del
  valor único; la tarjeta exterior no es navegable (`NavigateTo = null`). `HomeViewModel` agrega
  `ActivateSegmentCommand`. `StockAlertLevelsCard` (Order 20) reemplaza a `LowStockCard`
  (FR-018a); `OutOfStockCard` sigue igual.
- **Rationale**: extensión mínima del contrato de tarjetas de 002; las demás tarjetas no cambian.
- **Alternatives considered**: dos tarjetas separadas "Urgentes" y "En alerta": contradice H3 (una
  tarjeta con ambas cifras).

## §12. Auditoría

- **Decision**: `ProductAuditFields` agrega "Punto de reorden" con los decimales de la unidad
  (FR-004). Los registros de notificación no se auditan (no son operaciones sensibles, Principio
  IX).

## §13. Migración y versión

- **Decision**: migración `LowStockAlerts`:
  - `ALTER TABLE Products ADD COLUMN ReorderPoint INTEGER NULL`;
  - `CREATE TABLE StockAlertAcknowledgements` con su índice único.
  Se revisa el SQL para confirmar que no hay reconstrucción de `Products`. `Version` 0.15.0 →
  0.16.0 y base de ejemplo `v0.16.0.db` con un producto con mínimo y punto de reorden y un registro
  de notificación.

## §14. Pruebas (constitución v1.2.0, política mínima)

- **Domain**: `StockAlertRuleTests` (urgente, alerta, sin alerta; existencia 0 sin reorden),
  `StockAlertDedupTests` (escalamiento sí, descenso no, repetición no), `Product` con
  `IsValidReorderPoint` (menor válido, igual rechazado, decimales).
- **Infrastructure (SQLite real)**: `CheckStockAlertsTests` (segunda revisión del mismo día no
  notifica; otro usuario sí; día siguiente sí; registros persistidos) y una prueba de consistencia
  que compara el conteo SQL de `CountAlertsAsync` con `StockAlertRule` y con las filas del reporte
  filtrado (obligatoria: consistencia de inventario).
- **Migración**: `SampleDatabaseUpgradeTests` con `v0.16.0.db`.
- **Arquitectura**: sin reglas nuevas; las carpetas nuevas quedan cubiertas.
- Sin pruebas de ViewModels, monitor, centro de notificaciones ni vistas; se validan con
  [quickstart.md](quickstart.md).
