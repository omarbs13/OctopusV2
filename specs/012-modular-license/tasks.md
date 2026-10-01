# Tasks: Licencia modular por módulos

**Input**: documentos de diseño en `/specs/012-modular-license/` (plan.md, spec.md, research.md, data-model.md, contracts/license-contracts.md, quickstart.md)

**Prerequisites**: plan.md, spec.md. Parte de la licencia 011 ya implementada (`src/*/Licensing`).

**Tests**: Política mínima del plan (Principio VI): se incluyen pruebas de dominio, aplicación e infraestructura, la de consistencia de inventario y la de migración (obligatorias). Se actualizan o eliminan las pruebas de 011 que asumían modo lectura.

**Organization**: Tareas agrupadas por historia de usuario. Comandos: compilar `dotnet build -v q`; pruebas `dotnet test <proyecto> --verbosity quiet` (solo los proyectos modificados).

## Format: `[ID] [P?] [Story] Descripción con ruta`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia de usuario (US1–US6)

## Path Conventions

Aplicación de escritorio por capas: `src/Pos.Domain`, `src/Pos.Application`, `src/Pos.Infrastructure`, `src/Pos.Desktop`; pruebas en `tests/Pos.*.Tests`.

---

## Phase 1: Setup

**Purpose**: línea base antes de tocar la licencia 011

- [X] T001 Ejecutar `dotnet build -v q` en la raíz y anotar el estado base (sin errores) y qué pruebas de `tests/Pos.Application.Tests/Licensing/`, `tests/Pos.Domain.Tests/Licensing/` y `tests/Pos.Infrastructure.Tests/Licensing/` pasan hoy, para distinguir regresiones de cambios esperados

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: modelo de dominio, puertos, persistencia y migración que todas las historias necesitan. Los retiros de 011 se hacen aquí porque cambian contratos compartidos.

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase

### Dominio

- [X] T002 [P] Crear enum `LicensedModule` (`Inventory`, `AdvancedReports`, `CreditAndCustomers`, `CashShifts`, `Returns`) en `src/Pos.Domain/Licensing/LicensedModule.cs`
- [X] T003 Crear `ModuleCatalog` estático interno en `src/Pos.Domain/Licensing/ModuleCatalog.cs`: un `Guid` v4 fijo y único por módulo (generado una sola vez, nunca visible ni configurable, FR-007); `TryGetModule(Guid)` devuelve el módulo o nada (GUID desconocido se ignora, FR-008); `IdOf(LicensedModule)`; `All` con los 5 módulos (depende de T002)
- [X] T004 [P] Crear `ModuleAccess` estático en `src/Pos.Domain/Licensing/ModuleAccess.cs` con `Required(Permission) → LicensedModule?`: `ViewInventory` y `RegisterMovements` → Inventory; `ViewReports` → AdvancedReports; `OperateShift`, `WithdrawCash` y `ManageShifts` → CashShifts; `null` para `Sell`, `OpenDrawerWithoutSale`, `ViewProducts`, `ManageProducts`, `ViewOwnSales`, `ViewAllSales`, `CancelSales`, `ManageUsers`, `ViewAuditLog`, `ManageSettings`, `ExportDiagnostics`, `ManageLicense` (FR-021). `Permission` está en `Pos.Domain.Users` (depende de T002)
- [X] T005 Reescribir `src/Pos.Domain/Licensing/LicenseRecord.cs` según data-model.md: `Version` (int, 2), `MachineId`, `FirstRunUtc` (UTC, inmutable salvo recuperación desde la copia protegida), `LastSeenUtc` (UTC, nunca retrocede, FR-019), `TrialDays` (int, 30) y `Modules` (conjunto de `LicensedModule`, solo conocidos y comprados). Eliminar `LicenseGrant` y `ValidUntil` (depende de T002)
- [X] T006 Reescribir `src/Pos.Domain/Licensing/LicenseStatus.cs`: `Phase` (`Trial` | `Modular`), `DaysRemaining` (0 en `Modular`), `Warning` (`None`, `Near` = exactamente 5, `Urgent` = exactamente 1). Eliminar `Expired`, `Invalid` e `IsReadOnly`; `IsModuleActive(m) = Phase == Trial || Modules.Contains(m)` (depende de T005)
- [X] T007 Reescribir `src/Pos.Domain/Licensing/LicenseEvaluator.cs`: fecha efectiva = máximo entre reloj y `LastSeenUtc`; conteo por fecha de calendario local (día 1 → 30 restantes, día 30 → 1, día 31 → `Modular` con 0); aviso `Near` solo con 5 y `Urgent` solo con 1; recalcula con el reloj en cada llamada (depende de T006)

### Aplicación (puertos y errores)

- [X] T008 [P] Cambiar `src/Pos.Application/Licensing/ILicenseState.cs`: `Current`, `Record`, `IsModuleActive(LicensedModule)` (sin acceso a disco), `EnabledModules`, `Set(record)` y evento `Changed`; eliminar `IsBlocked`. Antes de cargar la licencia no se restringe nada (solo pruebas). Actualizar en el mismo archivo la implementación `LicenseState` (hoy expone `IsBlocked(Permission)`) para que use `IsModuleActive` y dispare `Changed` (depende de T006)
- [X] T009 [P] Cambiar `src/Pos.Application/Licensing/ILicenseStore.cs`: `Load()` devuelve `LicenseLoadResult` = `Loaded(record)` | `Missing` | `Unusable` (corrupto, alterado u otra máquina) | `LegacyV1(record)` (solo para migrar); `Save(LicenseRecord)` (depende de T005)
- [X] T010 [P] Crear puerto `ILicenseSealStore` y record `LicenseSeal(FirstRunUtc, LastSeenUtc)` en `src/Pos.Application/Licensing/ILicenseSealStore.cs`: `Task<LicenseSeal?> ReadAsync(ct)` (carga alterada o ilegible → `null`) y `Task WriteAsync(LicenseSeal, ct)`
- [X] T011 [P] Cambiar `src/Pos.Application/Licensing/ILicenseVerifier.cs`: `Verify(path, machineId)` → `Valid(ExtendedGrant(modules, issuedUtc))` | `Rejected(reason)`; motivos `LicenseImportRejection`: `Unreadable`, `BadSignature`, `OtherMachine` (eliminar `Older`) (depende de T002)
- [X] T012 Cambiar `src/Pos.Application/Abstractions/Error.cs`: reemplazar `LicenseExpired` por `ModuleNotLicensed(LicensedModule)` con el mensaje "Este módulo no está activo en tu licencia."; ajustar `InvalidLicense(reason)` a los motivos nuevos (depende de T011)
- [X] T013 [P] Agregar `LicenseRecovered` a `AuditActions` en `src/Pos.Application/Audit/AuditActions.cs` (junto a `LicenseImported`, incluida su tabla de textos en español); el detalle de `LicenseImported` pasa a "N módulos activados"

### Infraestructura (formato, sello y migración)

- [X] T014 Reescribir `src/Pos.Infrastructure/Licensing/LicenseFileStore.cs` al formato v2 de contracts §2: marca `POSL`, versión `2`, huella de máquina SHA-256(`"fp:" + machineId`) truncada a 8 bytes, nonce 12, etiqueta 16, AES-256-GCM con el encabezado como datos asociados, JSON `{machineId, firstRunUtc, lastSeenUtc, trialDays, modules[]}` (módulos como GUID), clave HKDF-SHA256 del ID de máquina con secreto de aplicación; escritura atómica (temporal + reemplazo); versión `1` solo se lee y se devuelve como `LegacyV1`; cualquier edición, truncado o copia a otra máquina → `Unusable` (depende de T003, T009)
- [X] T015 [P] Crear `LicenseSealConfiguration` en `src/Pos.Infrastructure/Persistence/Configurations/LicenseSealConfiguration.cs` y la entidad de persistencia `LicenseSealEntity` (en `src/Pos.Infrastructure/Licensing/LicenseSealEntity.cs`, porque es un dato técnico y no una entidad de dominio): tabla `LicenseSeals`, `Id` GUID v7 PK generado en la aplicación, `Payload` BLOB NOT NULL; sin campos de auditoría, borrado lógico ni versión (justificado en data-model.md). Registrar el `DbSet<LicenseSealEntity>` en `src/Pos.Infrastructure/Persistence/PosDbContext.cs`
- [X] T016 Crear `src/Pos.Infrastructure/Licensing/LicenseSealStore.cs` que implementa `ILicenseSealStore`: una sola fila, `Payload` = AES-256-GCM (nonce + etiqueta + cifrado) de `{firstRunUtc, lastSeenUtc}` con clave HKDF derivada con **contexto distinto** al del archivo; carga alterada o ilegible → `null` (depende de T010, T015)
- [X] T017 Generar la migración EF Core `ModularLicense` en `src/Pos.Infrastructure/Persistence/Migrations/` (`dotnet ef migrations add ModularLicense`) y revisar que el SQL sea solo `CREATE TABLE "LicenseSeals"` sin reconstruir tablas existentes; actualizar `PosDbContextModelSnapshot.cs` (depende de T015)
- [X] T018 Registrar `ILicenseSealStore`/`LicenseSealStore` en la composición (`src/Pos.Desktop/Composition/HostBuilder.cs`) (depende de T016)

### Pruebas fundamentales

- [X] T019 [P] Reescribir `tests/Pos.Domain.Tests/Licensing/LicenseEvaluatorTests.cs`: día 30 con todos activos, día 31 modular, reloj atrasado no devuelve días, aviso solo con exactamente 5 y exactamente 1 día (depende de T007)
- [X] T020 [P] Crear `tests/Pos.Domain.Tests/Licensing/ModuleCatalogTests.cs`: GUID únicos y `TryGetModule` con desconocido → nada (caso límite de FR-008) (depende de T003)
- [X] T021 [P] Crear `tests/Pos.Domain.Tests/Licensing/ModuleAccessTests.cs`: permisos de Inventario, Reportes y Turnos mapean a su módulo; `Sell` y `ManageUsers` devuelven `null`; todo `Permission` definido está cubierto (con módulo o `null` explícito) (depende de T004)
- [X] T022 [P] Reescribir `tests/Pos.Infrastructure.Tests/Licensing/LicenseFileStoreTests.cs`: v2 ida y vuelta, archivo alterado → `Unusable`, de otra máquina → `Unusable`, lectura de v1 → `LegacyV1`, archivo ausente → `Missing` (depende de T014)
- [X] T023 [P] Crear `tests/Pos.Infrastructure.Tests/Licensing/LicenseSealStoreTests.cs` con SQLite real: guardar y leer, segunda escritura actualiza la misma fila, carga alterada → `null` (depende de T016)
- [X] T024 Agregar la base de ejemplo/escenario de migración de `ModularLicense` (Principio IV): revisar cómo se migran las bases de ejemplo en `tests/Pos.Infrastructure.Tests/Startup/SqliteDatabaseMaintenanceTests.cs` y `tests/Pos.Infrastructure.Tests/TestSupport/` (`TestDb.cs`, `MigrationScenarios/`) y añadir el caso que migra una base de la versión anterior (011) a la actual sin perder datos y verifica que `LicenseSeals` queda vacía (depende de T017)

**Checkpoint**: dominio, formato v2, sello protegido y migración listos; la solución aún puede no compilar hasta completar las historias que consumen los contratos cambiados

---

## Phase 3: User Story 1 - Evaluación de 30 días con todos los módulos (Priority: P1) 🎯 MVP

**Goal**: primer arranque registra la fecha de inicio (archivo + sello); 30 días con todos los módulos; después modo modular. Incluye recuperación desde el sello (FR-018), reloj atrasado (FR-019) y migración 011 (FR-022).

**Independent Test**: con fecha de inicio conocida, dentro de 30 días `IsModuleActive` es verdadero para los 5 módulos y el día 31 solo para los licenciados; borrar `license.lic` no reinicia la evaluación.

### Tests for User Story 1

- [X] T025 [P] [US1] Crear `tests/Pos.Application.Tests/Licensing/LicenseBootstrapperTests.cs`: primer arranque (ambos ausentes) usa el menor entre ahora y `IInstallationAgeReader`; archivo borrado con sello válido recupera `FirstRunUtc` del sello y regenera sin módulos comprados (SC-007); archivo editado/`Unusable` se trata como borrado y emite `LicenseRecovered`; archivo presente y sello ausente reescribe el sello; fecha de inicio = la más antigua y última vista = la más reciente; reloj atrasado tras borrar el archivo no devuelve días (FR-019); migración 011: v1 con concesión activa → 5 módulos conservando `FirstRunUtc`, v1 sin concesión o vencida → evaluación nueva con sello sembrado

### Implementation for User Story 1

- [X] T026 [US1] Reescribir `src/Pos.Application/Licensing/LicenseBootstrapper.cs` según research §3 y §6: conciliar archivo y sello (`Loaded`, `Missing`, `Unusable`, `LegacyV1`), regenerar el archivo sin módulos comprados cuando falte o sea inválido, avanzar `LastSeenUtc` solo hacia adelante, escribir archivo y sello, cargar `ILicenseState`, auditar `LicenseRecovered` sin contenido del archivo ni GUID, y nunca cerrar la aplicación ante un archivo ilegible (Principio I). Eliminar la lógica de 011 de vencimiento/`Invalid` (depende de T007, T008, T009, T010, T013)
- [X] T027 [P] [US1] Reescribir `src/Pos.Application/Licensing/GetLicenseStatus/GetLicenseStatusHandler.cs`: `LicenseStatusDto(Phase, DaysRemaining, Warning, ActiveModules, ContactPhone, ContactEmail)`, sin permiso y sin acceso a disco; eliminar `IsReadOnly`/`Expired` (depende de T008)
- [X] T028 [US1] Actualizar `src/Pos.Desktop/Composition/LicenseClockScheduler.cs`: recalcula el estado al cruzar la medianoche y dispara `ILicenseState.Changed` si cambia la fase, sin reiniciar (depende de T008)
- [X] T029 [US1] Actualizar `src/Pos.Desktop/Licensing/LicenseModule.cs` y la composición en `src/Pos.Desktop/Composition/HostBuilder.cs` y `src/Pos.Desktop/Composition/App.axaml.cs` para invocar el nuevo `LicenseBootstrapper` al arranque con `ILicenseSealStore` (depende de T018, T026)

**Checkpoint**: evaluación y recuperación funcionan; los módulos aún no se bloquean (siguiente historia)

---

## Phase 4: User Story 2 - Archivo de licencia protegido con módulos por identificador opaco (Priority: P1)

**Goal**: `license.lic` cifrado y validado en cada arranque; editado o de otra máquina no habilita nada; GUID desconocidos se ignoran.

**Independent Test**: editar un byte del archivo → se trata como borrado (se regenera sin módulos, evaluación sigue); copiarlo a otra máquina → no habilita módulos; GUID desconocido en una licencia válida → ignorado.

### Tests for User Story 2

- [X] T031 [P] [US2] Reescribir `tests/Pos.Infrastructure.Tests/Licensing/EcdsaLicenseVerifierTests.cs` para el formato 2: válido, otra máquina → `OtherMachine`, firma alterada → `BadSignature`, GUID desconocido ignorado (válido sin activar nada), formato 1 → `Unreadable`, archivo > 16 KB → `Unreadable`
- [X] T032 [P] [US2] Actualizar `tests/Pos.Infrastructure.Tests/Licensing/TestLicenseIssuer.cs` para emitir `.poslic` formato 2 (`format`, `machineId`, `issuedUtc`, `modules[]`, firma ECDSA P-256/SHA-256 sobre el canónico)

### Implementation for User Story 2

- [X] T033 [US2] Reescribir `src/Pos.Infrastructure/Licensing/LicenseCanonical.cs` al canónico formato 2: UTF-8 sin espacios, orden `{"format":2,"machineId":"…","issuedUtc":"yyyy-MM-ddTHH:mm:ssZ","modules":["…"]}`, GUID en minúscula formato `D` ordenados ascendentemente; el verificador reordena y normaliza antes de verificar (depende de T003)
- [X] T034 [US2] Reescribir `src/Pos.Infrastructure/Licensing/EcdsaLicenseVerifier.cs`: tamaño máximo 16 KB, formato distinto de 2 → `Unreadable`, firma con la clave pública ya embebida, `machineId` distinto → `OtherMachine`, GUID desconocidos descartados vía `ModuleCatalog.TryGetModule`; devuelve `ExtendedGrant(modules, issuedUtc)` (depende de T011, T033)

**Checkpoint**: archivo local y licencia extendida verificables; protección demostrada por pruebas

---

## Phase 5: User Story 3 - Activación de módulos pagados con licencia extendida (Priority: P1)

**Goal**: el administrador importa un `.poslic` desde Acerca de; suma módulos sin tocar la fecha de inicio; rechaza de otra máquina o alterado.

**Independent Test**: con evaluación vencida, importar un archivo válido que active Inventario deja solo Inventario activo; otra máquina o alterado se rechaza sin cambiar la licencia; importar de nuevo es idempotente.

### Tests for User Story 3

- [X] T035 [P] [US3] Reescribir `tests/Pos.Application.Tests/Licensing/ImportLicenseHandlerTests.cs`: suma módulos sin modificar `FirstRunUtc`; idempotente (repetir no duplica); conserva módulos previos al importar otros; rechazo `OtherMachine`/`BadSignature`/`Unreadable` deja la licencia intacta; usuario sin `ManageLicense` rechazado; GUID desconocidos ignorados; audita `LicenseImported` con el número de módulos; dispara `Changed`

### Implementation for User Story 3

- [X] T036 [US3] Reescribir `src/Pos.Application/Licensing/ImportLicense/ImportLicenseHandler.cs`: exige `ManageLicense`; verifica con `ILicenseVerifier`; une módulos conocidos con los ya habilitados; no modifica `FirstRunUtc` y solo avanza `LastSeenUtc`; guarda archivo y sello; `ILicenseState.Set` (dispara `Changed`); devuelve `Result<LicenseStatusDto>`; elimina la regla `Older` de 011 (depende de T008, T010, T011, T012, T013)
- [X] T037 [P] [US3] Actualizar `src/Pos.Desktop/About/AboutViewModel.cs` y `src/Pos.Desktop/About/AboutView.axaml`: Administración de licencia (solo `ManageLicense`) muestra fase y días o modo modular, nombres de los módulos activos (nunca GUID), importar y exportar solicitud; mensajes de rechazo por motivo (depende de T027, T036)
- [X] T038 [P] [US3] Reescribir `src/Pos.Desktop/Licensing/LicenseMessages.cs` y los textos de `src/Pos.Desktop/Resources/Strings.resx` con los mensajes en español: rechazo "otra máquina", "no válido", archivo regenerado (indica que la evaluación continúa, que las compras se reactivan reimportando la licencia extendida e incluye el contacto del proveedor) y retirar los textos de modo lectura de 011

**Checkpoint**: se puede convertir una evaluación vencida en compra por módulo

---

## Phase 6: User Story 4 - Bloqueo de módulos no licenciados (Priority: P1)

**Goal**: tras la evaluación, los módulos sin licencia se ocultan del menú y sus operaciones (incluido el acceso directo) se rechazan con "Este módulo no está activo en tu licencia."; ventas y cancelaciones omiten sin error las partes de Inventario y Turnos bloqueados; los datos se conservan.

**Independent Test**: evaluación vencida con un solo módulo: los demás desaparecen del menú, sus casos de uso devuelven `ModuleNotLicensed` sin tocar datos, la venta se completa sin turno ni movimientos de inventario, y al reactivar los datos siguen ahí.

### Tests for User Story 4

- [X] T039 [P] [US4] Reescribir `tests/Pos.Application.Tests/Licensing/LicenseBlockingTests.cs`: `AccessControl` rechaza un permiso de módulo inactivo con `ModuleNotLicensed` **antes** de revisar roles y permite `Sell`; `HasAsync` devuelve falso para permisos de módulos inactivos
- [X] T040 [P] [US4] Crear `tests/Pos.Infrastructure.Tests/Licensing/ModuleInteractionTests.cs` con SQLite real (patrón de `OpenShiftLicenseTests.cs`): `ConfirmSale` con Turnos bloqueado vende sin turno (`CashShiftId = null`); con Inventario bloqueado no valida existencias ni genera movimientos (**consistencia de inventario**, obligatoria); `CancelSale` con Inventario bloqueado no repone existencias; con ambos activos el comportamiento no cambia; con Turnos inactivo y un turno ya abierto, el cierre del turno se rechaza con `ModuleNotLicensed` y el turno y sus datos se conservan; los datos del módulo bloqueado quedan intactos tras bloquear y reactivar (SC-005)
- [X] T041 [US4] Reemplazar `tests/Pos.Infrastructure.Tests/Licensing/OpenShiftLicenseTests.cs`: eliminar los casos de modo lectura de 011 y cubrir que abrir turno con Turnos inactivo devuelve `ModuleNotLicensed` por `AccessControl`

### Implementation for User Story 4

- [X] T042 [US4] Modificar `src/Pos.Application/Users/Access/AccessControl.cs`: `CheckAsync` consulta `ModuleAccess.Required(permiso)` y, si `ILicenseState.IsModuleActive` es falso, devuelve `ModuleNotLicensed` antes de revisar roles, sin tocar datos y registrando el rechazo sin datos sensibles; `HasAsync` devuelve falso; retirar `IsBlocked`/`LicenseExpired` (depende de T004, T008, T012)
- [X] T043 [US4] Modificar `src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs`: si Turnos está inactivo omitir `RequireOwnOpenShiftAsync` y guardar `CashShiftId = null`; si Inventario está inactivo no consultar existencias, no validar stock y no generar movimientos; sin error en ningún caso (FR-021, aclaración 4) (depende de T008)
- [X] T044 [US4] Modificar `src/Pos.Application/Sales/CancelSale/CancelSaleHandler.cs`: con Inventario inactivo omitir la reposición de existencias; con Turnos inactivo omitir la regla "solo en el turno de la venta" (depende de T008)
- [X] T045 [US4] Modificar `src/Pos.Application/CashShifts/OpenShift/OpenShiftHandler.cs`: retirar la verificación explícita de licencia de 011 (la cubre `AccessControl`) (depende de T042)
- [X] T046 [P] [US4] Modificar `src/Pos.Desktop/Navigation/MenuViewModel.cs`: ocultar entradas cuyo `Permission` pertenece a un módulo inactivo (reusa `ModuleAccess`), omitir grupos sin hijos visibles y reconstruir el menú con `ILicenseState.Changed` (depende de T004, T008)
- [X] T047 [P] [US4] Modificar `src/Pos.Desktop/Navigation/Navigator.cs`: rechazar el acceso directo/atajo a un módulo inactivo con el mensaje estándar sin modificar nada; retirar la pantalla segura "Inicio" por modo lectura de 011 (depende de T004, T008)
- [X] T048 [P] [US4] Modificar `src/Pos.Desktop/Reports/AlertsCard.cs`: tolerar `ModuleNotLicensed` ocultándose sin mostrar error y retirar el uso de `LicenseExpired`/`IsBlocked` si lo hay (depende de T012)
- [X] T049 [P] [US4] Modificar `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs` y `src/Pos.Desktop/CashShifts/OpenShiftViewModel.cs`: retirar el manejo de `LicenseExpired` de 011 y mostrar el mensaje estándar ante `ModuleNotLicensed` (depende de T012)
- [X] T050 [US4] Actualizar `src/Pos.Desktop/Common/UseCases.cs` para quitar usos de `LicenseExpired`/`IsBlocked` y mapear `ModuleNotLicensed` al mensaje estándar (depende de T012)

**Checkpoint**: el bloqueo por módulo funciona de punta a punta; la venta básica nunca se detiene

---

## Phase 7: User Story 5 - Avisos antes del vencimiento (Priority: P1)

**Goal**: Inicio muestra "Te quedan 5 días de acceso a todos los módulos." con exactamente 5 días y "Mañana vence el período de evaluación." con exactamente 1; ningún otro aviso.

**Independent Test**: simular 5 y 1 día restantes y comprobar cada aviso; con 4, 3, 2, más de 5 o evaluación vencida no aparece ninguno.

### Tests for User Story 5

- [X] T051 [P] [US5] Crear `tests/Pos.Application.Tests/Licensing/GetLicenseStatusHandlerTests.cs`: `Warning` es `Near` solo con 5 días, `Urgent` solo con 1, `None` con 2–4, más de 5 y en modo modular; el DTO incluye fase, días y módulos activos (depende de T027)

### Implementation for User Story 5

- [X] T052 [US5] Reescribir `src/Pos.Desktop/Licensing/LicenseCard.cs` (tarjeta de Inicio): tarjeta de evaluación con días restantes, aviso de 5 días y aviso de 1 día con los textos fijos; sin aviso de cuenta atrás en modo modular; ocultar tarjetas de Reportes/Inventario de Inicio sin error si su módulo está inactivo (depende de T027, T038)
- [X] T053 [US5] Modificar `src/Pos.Desktop/Shell/RootViewModel.cs`: retirar el aviso rojo de modo lectura del login de 011 y cualquier referencia a `IsReadOnly` (depende de T008)

**Checkpoint**: avisos exactos en Inicio; login sin avisos de licencia

---

## Phase 8: User Story 6 - Activación inmediata sin reiniciar (Priority: P2)

**Goal**: tras importar una licencia válida, los módulos aparecen en el menú y se pueden usar sin cerrar la aplicación (en menos de 5 s).

**Independent Test**: con la aplicación abierta y un módulo bloqueado, importar una licencia que lo activa y comprobar que aparece en el menú y opera sin reiniciar.

### Implementation for User Story 6

- [X] T054 [US6] Verificar el flujo completo en `src/Pos.Desktop/About/AboutViewModel.cs`, `src/Pos.Desktop/Navigation/MenuViewModel.cs` y `src/Pos.Desktop/Home/` tras importar: `ILicenseState.Changed` reconstruye el menú, refresca la tarjeta de licencia y las tarjetas de Inicio sin reiniciar; corregir cualquier suscripción faltante o fuga de suscripción (depende de T036, T046, T052)

**Checkpoint**: compra → menú actualizado sin reinicio (SC-004)

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: retiros finales de 011, validación y documentación

- [X] T056 Buscar y eliminar restos de 011 (research §10): `grep -rn "IsBlocked\|LicenseExpired\|LicenseKind\|ValidUntil\|LicenseGrant\|LicenseImportRejection.Older" src tests --include=*.cs --include=*.axaml --include=*.resx` y `grep -rn "IsReadOnly" src/Pos.Domain/Licensing src/Pos.Application/Licensing src/Pos.Desktop/Licensing src/Pos.Desktop/Shell` y corregir cada coincidencia (`IsReadOnly` de `FileInfo` en `SqliteDatabaseMaintenance.cs` y de `TextBox` en `CashCountReportView.axaml` no son de licencia: no tocarlos)
- [X] T057 Confirmar en `src/Pos.Application/Licensing/ExportLicenseRequest/ExportLicenseRequestHandler.cs` y en la exportación de diagnóstico que `license.lic` y los GUID no se exportan ni se registran en bitácora (escenario 17; Principios VIII y IX)
- [X] T058 Ejecutar `dotnet build -v q` en la raíz hasta que no haya errores ni advertencias
- [X] T059 Ejecutar `dotnet test tests/Pos.Domain.Tests --verbosity quiet`, `dotnet test tests/Pos.Application.Tests --verbosity quiet`, `dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet`, `dotnet test tests/Pos.Desktop.Tests --verbosity quiet` y `dotnet test tests/Pos.ArchitectureTests --verbosity quiet`; corregir fallos
- [ ] T060 Recorrer los 17 escenarios manuales de [quickstart.md](quickstart.md) con `POS_DATA_DIR` aislado (incluidos migración 011 y recuperación de archivo) y anotar cualquier discrepancia; además confirmar que el control de licencia no abre conexiones de red (FR-020) y que importar y ver el menú actualizado tarda menos de 5 s

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup; **bloquea** todas las historias
- **US1 (Phase 3)**: depende de Foundational; es el MVP (la evaluación y la recuperación son la base de todo)
- **US2 (Phase 4)**: depende de Foundational; independiente de US1 (verificador y canónico)
- **US3 (Phase 5)**: depende de Foundational y de US2 (`ILicenseVerifier` real) y de US1 para el estado cargado
- **US4 (Phase 6)**: depende de Foundational; independiente de US2/US3 en código, pero para probarlo con módulos comprados usa el estado en memoria
- **US5 (Phase 7)**: depende de Foundational y de T027 (US1)
- **US6 (Phase 8)**: depende de US3, US4 y US5
- **Polish (Phase 9)**: depende de las historias deseadas

### User Story Dependencies

- **US1 (P1)**: tras Foundational
- **US2 (P1)**: tras Foundational; puede ir en paralelo con US1
- **US3 (P1)**: tras US1 y US2
- **US4 (P1)**: tras Foundational; puede ir en paralelo con US1 y US2
- **US5 (P1)**: tras US1 (T027)
- **US6 (P2)**: tras US3, US4 y US5 (sin pruebas de ViewModels, Principio VI)

### Within Each User Story

- Pruebas primero (deben fallar antes de implementar)
- Dominio → puertos → casos de uso → infraestructura → interfaz
- Historia completa antes de pasar a la siguiente prioridad

### Parallel Opportunities

- Fundacional: T002, T004 y T008–T011, T013, T015 en paralelo una vez resueltas sus dependencias; T019–T023 (pruebas) en paralelo entre sí
- US2 y US4 pueden desarrollarse en paralelo con US1 por personas distintas
- En US4: T046, T047, T048 y T049 (Desktop) en paralelo; T043 y T044 (handlers de ventas) en paralelo

---

## Parallel Example: Foundational

```bash
# Dominio y puertos independientes entre sí:
Task: "Crear enum LicensedModule en src/Pos.Domain/Licensing/LicensedModule.cs"
Task: "Crear ILicenseSealStore en src/Pos.Application/Licensing/ILicenseSealStore.cs"
Task: "Cambiar ILicenseState en src/Pos.Application/Licensing/ILicenseState.cs"

# Pruebas de dominio una vez listo el dominio:
Task: "ModuleCatalogTests en tests/Pos.Domain.Tests/Licensing/ModuleCatalogTests.cs"
Task: "ModuleAccessTests en tests/Pos.Domain.Tests/Licensing/ModuleAccessTests.cs"
```

## Parallel Example: User Story 4

```bash
Task: "MenuViewModel oculta módulos inactivos en src/Pos.Desktop/Navigation/MenuViewModel.cs"
Task: "Navigator rechaza acceso directo en src/Pos.Desktop/Navigation/Navigator.cs"
Task: "ConfirmSaleHandler omite turno e inventario en src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs"
Task: "CancelSaleHandler omite reposición en src/Pos.Application/Sales/CancelSale/CancelSaleHandler.cs"
```

---

## Implementation Strategy

### MVP First (US1)

1. Fase 1 (Setup) y Fase 2 (Foundational)
2. Fase 3 (US1): evaluación de 30 días, sello protegido, recuperación y migración 011
3. **Detenerse y validar**: escenarios 1, 2, 10–12, 14 y 15 del quickstart
4. Nota: sin US4 el bloqueo aún no se aplica; el MVP comercial real es US1 + US4

### Incremental Delivery

1. Setup + Foundational → base lista
2. US1 → evaluación y recuperación (MVP técnico)
3. US4 → bloqueo por módulo (MVP comercial)
4. US2 + US3 → archivo protegido y compra por importación
5. US5 → avisos; US6 → activación inmediata
6. Polish → retiro de restos de 011 y validación manual

### Parallel Team Strategy

Tras Foundational: A toma US1→US5; B toma US2→US3; C toma US4; US6 al final cuando A, B y C terminen.

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes
- La migración `ModularLicense` es aditiva (solo `CREATE TABLE`); revisar el SQL antes de integrar
- Los GUID de `ModuleCatalog` se generan una sola vez y se entregan al proveedor fuera del repositorio
- Crédito y clientes y Devoluciones quedan definidos en el catálogo pero sin funcionalidad que bloquear hasta que existan (research §2)
- Riesgo documentado: con Inventario bloqueado el inventario queda desfasado hasta reactivarlo; no se reconcilia automáticamente
- T024 depende de cómo estén organizadas hoy las bases de ejemplo de migración; revisar antes de escribir el caso
