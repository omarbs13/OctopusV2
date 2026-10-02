# Research: Corte X y Corte Z

Decisiones técnicas de la funcionalidad 017. Cada sección indica la decisión, su justificación y
las alternativas descartadas. No quedan puntos "NEEDS CLARIFICATION".

## §1. Un agregado nuevo `ShiftCut` para los dos tipos de corte

- **Decisión**: una sola entidad `ShiftCut` (tabla `ShiftCuts`) con `Type` (`X` o `Z`), `Number`
  consecutivo por tipo, turno, quién lo generó, quién autorizó y una instantánea completa de cifras.
  Vive en `Pos.Domain/CashShifts/` junto al turno, porque no existe sin él.
- **Justificación**: el histórico (FR-015) lista ambos tipos en una sola consulta paginada, con un
  solo lector y un solo armado de ticket. El Corte X no puede vivir dentro de `CashShift`: el turno
  no debe cambiar (FR-003) y su `Version` no debe subir, o el cierre en curso devolvería `Conflict`.
- **Alternativas descartadas**:
  - Tabla separada para X y Z: duplica consulta, paginación y ticket.
  - Guardar el folio Z como columna en `CashShifts`: obliga a unir dos fuentes en el histórico y
    los Corte X igual necesitan tabla propia.

## §2. Instantánea completa también en el Corte Z

- **Decisión**: el Corte Z copia las mismas cifras que `CashShift.Close` guarda en el turno
  (totales, ingresos, retiros, esperado, contado, diferencia, comentario), en la misma transacción
  y desde los mismos valores calculados.
- **Justificación**: FR-006 y la entidad "Corte" piden una copia fija por corte; el histórico y la
  reimpresión leen siempre de `ShiftCuts`, sin ramas por tipo. La duplicación es de unas 25 columnas
  enteras por turno y no puede divergir: ambas se escriben en el mismo `SaveChanges`.
- **Alternativa descartada**: que el Z solo apunte al turno y lea su instantánea. Ahorra columnas
  pero obliga a dos caminos de lectura y a que el ticket distinga el origen.

## §3. Folios consecutivos sin huecos ni repeticiones

- **Decisión**: `Number = MAX(Number) + 1` del mismo tipo, calculado dentro de la transacción de
  escritura (`BEGIN IMMEDIATE`, igual que `CashShiftRepository.NextNumberAsync`), con índice único
  `(Type, Number)`. Folio legible `X-000001` / `Z-000001` (`ShiftCutFolio`).
- **Justificación**:
  - `BEGIN IMMEDIATE` serializa a los escritores: dos cortes simultáneos no leen el mismo máximo.
  - Si algo falla antes del `Commit`, la transacción se revierte y el número no se consume
    (casos límite "Falla al generar…").
  - Los cortes nunca se borran (§9), así que el máximo nunca baja y un folio no se reutiliza.
  - El índice único es la última defensa: un duplicado se convierte en `Conflict`, no en dato malo.
- **Alternativas descartadas**: tabla de contadores (otra fila que bloquear y migrar, sin ventaja
  con una sola caja); secuencia en memoria (se pierde al reiniciar).
- **Turnos anteriores (FR-010a)**: la migración no crea filas; el primer Z posterior es `Z-000001`.

## §4. Corte Z = cierre de turno de la spec 008

- **Decisión**: `CloseShiftHandler` crea el `ShiftCut` tipo Z justo después de `shift.Close(...)`,
  dentro de la misma transacción y antes del único `SaveChangesAsync`. `ClosedShift` agrega
  `CutId` y `CutFolio`. No hay flujo de cierre nuevo: conteo ciego, `ShiftChanged`, comentario
  obligatorio, venta en curso y cierre de turno ajeno quedan igual (FR-008).
- **Justificación**: FR-009 exige el folio dentro de la misma operación atómica; cualquier cierre,
  incluido el de un turno ajeno por un Administrador (escenario 2.6), pasa por este caso de uso.
- **Doble Corte Z simultáneo**: `BEGIN IMMEDIATE` serializa; el segundo ve el turno `Closed` y
  recibe `ShiftClosed`. Defensa adicional: índice único filtrado `ShiftId WHERE Type = 'Z'` y el
  token de concurrencia `Version` del turno.

## §5. Corte X: lectura que sí escribe su registro

- **Decisión**: caso de uso `GenerateShiftReadout` en una transacción de escritura que:
  1. Lee el turno abierto de la caja con el `GetOpenAsync` existente y no lo modifica (EF Core no
     emite `UPDATE` sobre una entidad sin cambios).
  2. Calcula `GetShiftTotalsAsync` y el esperado con `CashShift.ExpectedCash` (misma regla del
     cierre, FR-016).
  3. Inserta solo la fila `ShiftCut` tipo X y la bitácora.
- **Justificación**: FR-003 y SC-001: el turno, sus ventas y movimientos no se tocan; `Version` del
  turno no cambia, así que un cierre en curso no se invalida.
- **Venta en curso**: los borradores viven en `ISaleDraftStore`, no en `Sales`; el Corte X no los
  ve ni los toca (caso límite). No se aplica `ShiftGuard.CheckHeldSaleAsync`, que solo protege el cierre.

## §6. Permiso del Corte X y autorización del Cajero

- **Decisión**: nuevo permiso `GenerateShiftReadout`, solo del Administrador, **autorizable**
  (`RolePermissions.Authorizable`) y del módulo `CashShifts` (`ModuleAccess`).
  - El Cajero recibe `Forbidden(CanBeAuthorized: true)`; la pantalla abre
    `AdminAuthorizationService.RequestAsync(GenerateShiftReadout, contexto)` y reintenta con la
    concesión, igual que el retiro (008).
  - `AccessDecision.AuthorizedBy` se guarda en el corte y en la bitácora (FR-007).
  - Los rechazos de autorización ya quedan como `ADMIN_AUTHORIZATION_DENIED` con el contexto
    (007, 015); los rechazos por permiso, en el log de `AccessControl` (FR-017).
- **Turno ajeno**: el Corte X es siempre del turno abierto de la caja, sea de quien sea; el permiso
  basta (caso límite "turno abierto por otro usuario").
- **Efectivo esperado visible**: el Corte X muestra el esperado (FR-004). El arqueo ciego de 008
  protege el *cierre*; un Cajero solo ve el esperado con un Administrador presente que autoriza.
- **Alternativas descartadas**: reusar `ManageShifts` (no es autorizable y abriría otras
  operaciones); permitirlo a `OperateShift` (contradice la clarificación).

## §7. Histórico de cortes

- **Decisión**: permiso existente `ManageShifts` (solo Administrador, módulo `CashShifts`). Consulta
  `SearchShiftCuts` con filtros tipo, desde/hasta (fecha local de generación convertida a UTC, hasta
  exclusivo) y usuario que lo generó; orden `GeneratedAt DESC, Id DESC`; páginas de 100
  (`ShiftCutPage.DefaultPageSize`, como `ShiftPage`). Índices `(GeneratedAt, Id)` y
  `(GeneratedBy, GeneratedAt)`.
- **Justificación**: es una consulta de auditoría del Administrador como "Turnos"; no hace falta un
  permiso nuevo (Principio VII).

## §8. Navegación: grupo nuevo "Caja"

- **Decisión**: grupo `cash` ("Caja", orden 6; con el mismo orden que Clientes, el id `cash`
  ordena antes que `customers`, así que queda justo después de Ventas) con tres páginas:

  | Página | Id | Orden | Permiso del menú |
  |---|---|---|---|
  | Corte X | `cash.readout` | 0 | `OperateShift` (el Cajero la ve; el caso de uso pide autorización) |
  | Corte Z | `cash.close` | 10 | `OperateShift` |
  | Histórico de cortes | `cash.cuts` | 20 | `ManageShifts` |

- **Justificación**: el menú solo registra páginas con un permiso; los tres permisos son del
  módulo `CashShifts`, así que el grupo desaparece sin licencia (FR-020) y vuelve al reactivarlo.
  "Turnos" se queda en Ventas para no mover lo que ya usan los clientes.
- **Corte Z como página**: muestra el estado del turno abierto (folio, dueño, desde) y el botón
  "Hacer Corte Z", que abre `CashShiftDialogs.CloseShiftAsync`. El botón "Cerrar turno" del Punto de
  venta y del detalle de turno pasa a decir "Corte Z" y usa el mismo diálogo.

## §9. Inmutabilidad (FR-013)

- **Decisión**: `ShiftCut` no tiene métodos de modificación; el repositorio solo expone `Add` y
  consultas. `DeletedAt` existe por el Principio IV y siempre es nulo; no hay caso de uso para
  borrar, editar ni reabrir. El turno cerrado ya era inmutable (`EnsureOpen`).

## §10. Impresión y reimpresión

- **Decisión**: `PrintSource.ShiftCut(cutId)` y `ShiftTicketBuilder.BuildCut` sobre `ShiftCutReportDto`.
  - Título "CORTE X" o "CORTE Z", folio del corte, folio del turno, fecha y hora de generación.
  - El X agrega la leyenda "LECTURA PARCIAL - NO ES CIERRE DE CAJA" y omite contado, diferencia y
    comentario; el Z los incluye.
  - Mismas filas de cifras que el corte de 008 (se extraen a un método común del builder).
- **Acceso a imprimir**: quien generó el corte o quien tiene `ManageShifts` (mismo criterio que el
  comprobante de movimiento). Así el Cajero autorizado imprime su Corte X sin pedir otra autorización.
- **Bitácora**: la generación queda en la bitácora del caso de uso; cada reimpresión
  (`IsReprint = true`) agrega `SHIFT_CUT_REPRINTED` con tipo, folio y turno (FR-017).
- **Impresora no disponible**: el corte ya está guardado; `TicketPrintingService` muestra el error con
  "Reintentar" y el corte se reimprime desde el histórico.
- **"Turnos" (008)**: `ShiftReportDto` y `ShiftDetailDto` agregan `CutFolio` (nulo en turnos
  anteriores); el corte impreso desde "Turnos" muestra "Corte Z Z-000001" cuando existe.

## §11. Bitácora

| Evento | Código | Texto |
|---|---|---|
| Corte X generado | `SHIFT_READOUT_GENERATED` (nuevo) | "Corte X generado" |
| Corte Z | `SHIFT_CLOSED` / `SHIFT_CLOSED_BY_ADMIN` (existentes) | "Turno cerrado (Corte Z)" / "Turno cerrado por administrador (Corte Z)" |
| Reimpresión de corte | `SHIFT_CUT_REPRINTED` (nuevo) | "Corte reimpreso" |

- Entidad `ShiftCut` (nueva) para X y reimpresiones; el cierre sigue con entidad `CashShift`.
- El Z conserva una sola entrada de cierre (research §8 de 008) y agrega el folio Z al resumen:
  "Corte Z Z-000001. Turno T-000123. Esperado…".

## §12. Turnos nuevos empiezan en cero (FR-011, FR-012)

- **Decisión**: sin cambios de código. `GetShiftTotalsAsync` ya filtra por `ShiftId`, y ventas,
  movimientos, cancelaciones con reintegro y abonos ya exigen turno abierto (`ShiftGuard`,
  `PaymentShiftGate`). Las operaciones de 013 y 014 sobre ventas de turnos anteriores ya se cuentan
  en el turno donde se registran.
- **Verificación**: una prueba de integración del Corte X después de un Corte Z cubre el escenario 2.4.

## §13. Migración y versión

- Migración `ShiftCuts`: crea la tabla y sus índices. **No reconstruye ninguna tabla** ni toca
  `CashShifts`.
- Clave foránea `ShiftCuts.ShiftId → CashShifts.Id` con `Restrict`: como la tabla es nueva, no
  reconstruye nada. `GeneratedBy` y `AuthorizedBy` van sin clave foránea, como `OpenedBy` y
  `CreatedBy` en el resto del proyecto.
- `Version` 0.11.0 → 0.12.0; base de ejemplo `v0.12.0.db` con turnos cerrados antes de la
  actualización (sin Z) y `ShiftCutsMigrationTests`.

## §14. Pruebas (política mínima, constitución v1.2.0)

- **Domain** (`ShiftCutTests`): la instantánea del X y del Z da el mismo esperado que
  `CashShiftMath` y el Z rechaza datos sin conteo (caso válido + límite).
- **Casos de uso sobre SQLite real** (`ShiftCutUseCaseTests`):
  - El Corte X no cambia `Version`, cifras ni esperado del turno, y su instantánea no cambia tras otra
    venta (SC-001, escenario 3.4).
  - Folios X y Z consecutivos; un Z rechazado (`ShiftChanged`) no consume folio (FR-010).
  - Dos Corte Z simultáneos: uno `ShiftClosed`, sin huecos (caso límite).
  - Cajero sin concesión → `Forbidden(CanBeAuthorized: true)`; sin turno → `ShiftRequired`.
  - Corte X tras Corte Z y turno nuevo: cifras solo del turno nuevo (escenario 2.4).
  - Histórico: filtro por tipo y paginación.
- **Migración**: `ShiftCutsMigrationTests` y `SampleDatabaseUpgradeTests` con `v0.12.0.db`.
- **Arquitectura e inventario**: las obligatorias existentes.
- Sin pruebas de ViewModels, vistas, ticket ni mapeos.

## §15. Dependencias

Ninguna nueva.
