# Implementation Plan: Alertas inteligentes de bajo stock

**Branch**: `022-low-stock-alerts` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/022-low-stock-alerts/spec.md`

## Summary

Avisar sin que el operador busque cuando hay productos en la existencia mínima (alerta) o en el
punto de reorden (urgente), como máximo una vez al día por producto, nivel y usuario.

1. **Punto de reorden** (research §1, §2): columna nula `Products.ReorderPoint` junto a
   `MinimumStock`. Debe ser estrictamente menor que el mínimo y respetar los decimales de la unidad.
   Se captura en el editor de producto y se audita.
2. **Regla de nivel** (research §3): `StockAlertRule` en Domain da `Urgent` / `Alert` / `None`,
   independiente de `StockStatusRule`; un producto en 0 puede ser urgente y además "sin existencia".
3. **Una vez al día** (research §4, §5): tabla técnica `StockAlertAcknowledgements` (usuario,
   producto, nivel, día local) y regla `StockAlertDedup`: alerta → urgente sí vuelve a notificar,
   urgente → alerta no. Se purga a los 7 días.
4. **Revisión** (research §6, §9): caso de uso `CheckStockAlerts` en una transacción. Lo invoca
   `StockAlertMonitor`, del ámbito de sesión, al iniciar sesión y cada hora, sin mostrar errores.
5. **Notificaciones** (research §10): `NotificationCenter` genérico en el shell, con una pila no
   bloqueante abajo a la derecha y una notificación por nivel. Pulsar abre "Reportes > Inventario"
   filtrado; "×" la descarta.
6. **Reporte e Inicio** (research §7, §8, §11):
   - `StockFilter` agrega `Alert` y `Urgent`, y el reporte agrega la columna "Punto de reorden" y
     recibe el filtro por navegación.
   - La tarjeta "Alertas de existencia", con dos cifras de color, reemplaza a "Existencia baja"
     mediante segmentos nuevos en `DashboardCard`.

No se agrega ninguna dependencia externa.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia, CommunityToolkit.Mvvm, Hosting, Serilog, EF Core 10
Sqlite, FluentValidation). **No se agrega ninguna.**

**Storage**:

- SQLite, migración `LowStockAlerts`:
  - `Products.ReorderPoint INTEGER NULL` (`ADD COLUMN`, sin reconstrucción);
  - tabla nueva `StockAlertAcknowledgements` con índice único `(UserId, LocalDate, Level, ProductId)`.
- `Version` 0.15.0 → 0.16.0 y base de ejemplo `v0.16.0.db`.

**Testing**: xUnit v3, con la política mínima de la constitución v1.2.0 (research §14):

- **Domain**: `StockAlertRuleTests`, `StockAlertDedupTests` y punto de reorden en `ProductTests`.
- **Casos de uso sobre SQLite real**: `CheckStockAlertsTests` (mismo día, otro usuario, día
  siguiente, escalamiento).
- **Consistencia de inventario (obligatoria)**: `StockAlertConsistencyTests`, que comprueba que el
  conteo SQL = `StockAlertRule` = filas del reporte filtrado.
- **Migración**: `SampleDatabaseUpgradeTests` con `v0.16.0.db`.
- Sin pruebas de ViewModels, monitor, centro de notificaciones ni vistas.

**Target Platform**: Windows 10+ y Linux.

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Una revisión con 10 000 productos sin pausa perceptible en el Punto de venta (SC-004). Es una
  consulta de candidatos (solo productos con umbral) más una de registros del día con índice, y
  corre en segundo plano de la UI (`DispatcherPriority.Background`, llamadas asíncronas).
- La tarjeta se cuenta en SQL sobre `IX_Products_TracksInventory`.

**Constraints**:

- Sin red.
- Las notificaciones nunca toman el foco ni bloquean la venta (FR-012).
- Una falla de la revisión solo se registra en el log (FR-015).
- 0 advertencias.

**Scale/Scope**:

- Catálogo de hasta 10 000 productos; un registro por producto, nivel, usuario y día, purgado a
  7 días.
- Casos de uso: 1 nuevo (`CheckStockAlerts`); cambian `CreateProduct`, `UpdateProduct`, `GetProduct`,
  `GetStockAlerts` (conteos) y `GetInventoryReport` (filtro, columna y exportación).
- Pantallas: editor de producto, shell (notificaciones), Inicio (tarjeta) y "Reportes > Inventario".

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó después del diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Todo local. Las notificaciones no son modales ni toman el foco. La revisión escribe en una única transacción (registros + purga). **Desviación justificada**: la revisión corre con `OperationRunner.RunQuietlyResultAsync`; un error se registra en el log pero no se muestra al operador (FR-015, ver Complexity Tracking). |
| II. Capas | Reglas (`StockAlertRule`, `StockAlertDedup`, `StockAlertAcknowledgement`) en Domain. `CheckStockAlerts` e `IStockAlertStore` en Application. `StockAlertStore`, la migración y los predicados SQL en Infrastructure. Monitor, centro de notificaciones y tarjeta en Desktop. Las carpetas nuevas quedan cubiertas por las pruebas de arquitectura existentes. |
| III. Lógica en el núcleo | El nivel y la deduplicación se deciden en Domain y se orquestan en el caso de uso. El ViewModel solo muestra los conteos y navega. La validación del punto de reorden está en `Product` y en `ProductRules`. |
| IV. Integridad de datos | La columna nueva es nula y no reconstruye `Products`; se revisa el SQL. La tabla nueva usa GUID v7, `CreatedAt` en UTC y migración versionada con `v0.16.0.db`. **Desviación justificada**: `StockAlertAcknowledgements` no lleva usuario de modificación, versión ni borrado lógico y se purga físicamente; además, su columna `LocalDate` guarda un día calendario local, no un instante UTC (ver Complexity Tracking). |
| V. Multiplataforma | Notificaciones dentro de la ventana, con Avalonia; no usa APIs del sistema operativo. `DispatcherTimer` es igual en Windows y Linux. |
| VI. Calidad verificable | Se prueban las reglas de umbral, nivel y deduplicación (validaciones de integridad y reglas de inventario), la consistencia de inventario y la migración, sobre SQLite real. La UI se valida con [quickstart.md](quickstart.md). |
| VII. Simplicidad | Sin dependencias ni umbrales configurables globales; la hora de revisión es fija (1 h) y la purga también (7 días). Se reutilizan `OperationRunner`, `Navigator` con argumento, `StockFilter`, el contrato de tarjetas y los colores de estado. El repositorio nuevo es específico (`IStockAlertStore`), no genérico. |
| VIII. Soporte | Cada revisión registra en el log los conteos y si notificó (`RevisarAlertasDeExistencia`, con el usuario). Se crea `docs/alertas-de-existencia.md` y se actualizan `docs/reportes.md` y `docs/migraciones.md`. |
| IX. Seguridad local | Se reutiliza el permiso `ViewInventory`. El cambio del punto de reorden se audita con el producto (018). Los registros de notificación no son operaciones sensibles y no se auditan. |

**Resultado**: tres desviaciones justificadas (Principio I: falla silenciosa de la revisión;
Principio IV: registro técnico sin auditoría ni borrado lógico, y `LocalDate` local; ver
Complexity Tracking). Decisiones
explícitas:

- **El nivel es independiente del estado** (clarificación 4, research §3): `Out` sigue existiendo
  para la tarjeta "Sin existencia" y el filtro existente.
- **Registro al mostrar, no al descartar** (research §4): mostrar ya cuenta como notificado; así un
  cierre inesperado no repite la notificación.
- **Total del nivel en el mensaje** (research §5): coincide con la tarjeta y con el reporte.

## Project Structure

### Documentation (this feature)

```text
specs/022-low-stock-alerts/
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
│   ├── Products/Product.cs                                   # ReorderPoint, IsValidReorderPoint
│   └── Inventory/StockAlertLevel.cs  StockAlertDedup.cs  StockAlertAcknowledgement.cs
├── Pos.Application/
│   ├── Products/ProductRules.cs  ProductFields.cs  ProductMessages.cs  ProductDto.cs
│   │   ProductMapping.cs  ProductAuditFields.cs
│   ├── Products/CreateProduct/…  UpdateProduct/…              # ReorderPointText
│   ├── Inventory/IInventoryRepository.cs                      # StockFilter.Alert/Urgent, StockAlertCounts
│   ├── Inventory/InventoryMessages.cs                         # sujeto "El punto de reorden"
│   ├── Inventory/IStockAlertStore.cs
│   ├── Inventory/CheckStockAlerts/CheckStockAlertsHandler.cs  StockAlertCheck.cs
│   ├── Reports/GetInventoryReport/InventoryReportDtos.cs      # ReorderPoint, Level, Alert/Urgent
│   ├── Reports/Export/ReportDocumentBuilder.cs                # columna "Punto de reorden"
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── Persistence/Configurations/ProductConfiguration.cs  StockAlertAcknowledgementConfiguration.cs
│   ├── Persistence/PosDbContext.cs                            # DbSet nuevo
│   ├── Persistence/Migrations/…_LowStockAlerts.cs  PosDbContextModelSnapshot.cs
│   ├── Inventory/InventoryRepository.cs                       # FilterByAlertLevel, conteos
│   ├── Inventory/StockAlertStore.cs
│   ├── Reports/InventoryReportReader.cs                       # Level, filtro, columna
│   └── DependencyInjection.cs
├── Pos.Desktop/
│   ├── Shell/NotificationCenter.cs  NotificationItem.cs  NotificationSeverityBrushConverter.cs  MainView.axaml  RootViewModel.cs
│   ├── Inventory/StockAlertMonitor.cs  StockAlertLevelsCard.cs  InventoryCards.cs  InventoryModule.cs
│   ├── Home/DashboardCard.cs  DashboardCardSegment.cs  HomeView.axaml  HomeViewModel.cs
│   ├── Reports/InventoryReportViewModel.cs  InventoryReportView.axaml
│   ├── Products/ProductEditorViewModel.cs  ProductEditorView.axaml
│   └── Resources/Strings.resx                                 # Notify_*, Card_StockAlertLevels*, Editor_ReorderPoint*
tests/
├── Pos.Domain.Tests/Inventory/StockAlertRuleTests.cs  StockAlertDedupTests.cs
├── Pos.Domain.Tests/Products/ProductTests.cs
├── Pos.Infrastructure.Tests/Inventory/CheckStockAlertsTests.cs  StockAlertConsistencyTests.cs
└── Pos.Infrastructure.Tests/SampleDatabases/v0.16.0.db  SampleDatabaseGenerator.cs
    LowStockAlertsMigrationTests.cs  SampleDatabaseUpgradeTests.cs
docs/
├── alertas-de-existencia.md     # nuevo: umbrales, niveles, cuándo se notifica, descartar
├── reportes.md                  # filtros "En alerta"/"Urgente", columna nueva
└── migraciones.md               # sección 0.16.0
Directory.Build.props            # Version 0.16.0
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de 001 a 021.

- Las reglas de nivel viven en `Domain/Inventory`, junto a `StockStatusRule`.
- El caso de uso va en `Application/Inventory/CheckStockAlerts/`, porque es inventario y usa
  `ViewInventory`.
- `NotificationCenter` va en `Desktop/Shell` porque es infraestructura del shell, reutilizable por
  otras funcionalidades. El monitor y la tarjeta van en `Desktop/Inventory`.
- Las pruebas existentes que construyen `StockAlertCounts`, `InventoryReportRow` o
  `Product.Create/Update` con argumentos posicionales se ajustan a las firmas nuevas.

## Complexity Tracking

| Desviación | Por qué se necesita | Alternativa más simple descartada y por qué |
|---|---|---|
| Principio IV: `StockAlertAcknowledgements` no tiene usuario ni fecha de modificación, versión ni `DeletedAt`, y sus filas se borran físicamente al cumplir 7 días. | Es un registro técnico para no repetir avisos: no tiene valor histórico ni contable, nunca se modifica (solo se inserta y se purga) y crecería sin límite si no se borrara. Ya hay un precedente con `SaleDrafts`. Conserva GUID v7, `CreatedAt` en UTC y la migración versionada. | Columnas de auditoría completas y borrado lógico: agregan escrituras y crecimiento sin ningún consumidor. Guardarlo en `IPreferencesStore`: no es transaccional y no pertenece a la base del negocio por usuario. |
| Principio I: una falla de la revisión de alertas se registra en el log y **no** se muestra al operador (FR-015). | La revisión no la inicia el operador: corre sola al iniciar sesión y cada hora, posiblemente a mitad de una venta. Un mensaje de error interrumpiría la venta, que es lo que el Principio I protege, por una operación informativa que se reintenta sola en la siguiente revisión. Ninguna venta ni dato se pierde: la transacción se revierte. Precedente: `RunQuietlyResultAsync` en cargas de fondo (002). La tarjeta de Inicio sigue mostrando los conteos. | Mostrar el error con `RunAsync`: un diálogo cada hora ante una base bloqueada interrumpe la venta. Aviso no bloqueante de error: el operador no puede hacer nada con él; el soporte lo encuentra en el log (`RevisarAlertasDeExistencia`, Principio VIII). |
| Principio IV: `StockAlertAcknowledgements.LocalDate` guarda el día calendario local (`DateOnly`), no un instante UTC. | Es una clave de día ("ya se notificó hoy"), no un instante: el día se define como el día calendario local del equipo (FR-009). Guardar el instante en UTC obligaría a recalcular la medianoche local en cada consulta y no sería indexable por día. El instante sí se guarda en UTC en `CreatedAt`. Precedente: `Coupon.StartsOn`/`EndsOn` (`DateOnly`). | Guardar solo `CreatedAt` en UTC y filtrar por rango local convertido: consulta más compleja, propensa a errores con cambios de horario y sin índice único por día. |
