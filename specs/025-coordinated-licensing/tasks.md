---

description: "Lista de tareas de 025-coordinated-licensing"
---

# Tasks: Licenciamiento coordinado con OctopusAdmin

**Input**: Documentos de diseño en `/specs/025-coordinated-licensing/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (y `contracts/` de la raíz), quickstart.md

**Tests**: Incluidas. El plan (Constitution Check, Principio VI) exige pruebas de reglas: evaluador,
antigüedad, verificador v3 + vector del contrato, consistencia catálogo↔JSON, bloqueo en
`AccessControl` y venta en curso, persistencia de `InstalledLicenses` en SQLite real y migración.
**Sin pruebas de UI ni de ViewModels.**

**Organization**: Tareas agrupadas por historia de usuario. Todas las historias H1–H8 son P1 y se
implementan en el orden del plan ("Implementation order"); H9 es P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Puede ejecutarse en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: Historia a la que pertenece (US1…US9)
- Comandos: compilar `dotnet build -v q`; probar `dotnet test --verbosity quiet`

## Path Conventions

Aplicación de escritorio en capas: `src/Pos.Domain`, `src/Pos.Application`,
`src/Pos.Infrastructure`, `src/Pos.Desktop`; pruebas en `tests/Pos.*.Tests`. Contratos
compartidos con OctopusAdmin en `contracts/` (raíz), ya creados: `module-catalog.json`,
`license-format.md`, `license-request.md`. Contrato interno: `specs/025-coordinated-licensing/contracts/blocked-mode.md`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Recurso embebido del catálogo y verificación de los contratos ya creados

- [X] T001 Revisar que `contracts/module-catalog.json` tenga `catalogVersion: 1` y los 9 módulos con los GUID, `key`, `order` e `isBase` exactos de la tabla `ModuleCatalog` de data-model.md (POS `4c2517f5-096a-460e-8ee7-b09e46572952` base; Proveedores `1e6111ca-514b-497e-b553-154960c946f3`; Categorías `7b4ae9e2-6e3f-4006-b259-b7d4dcd06933`); corregir el JSON solo si difiere de data-model.md
- [X] T002 Agregar `<EmbeddedResource Include="..\..\contracts\module-catalog.json" LogicalName="Pos.ModuleCatalog.json" />` en src/Pos.Infrastructure/Pos.Infrastructure.csproj y comprobar con `dotnet build -v q`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Nuevos tipos de Domain y la forma nueva de los puertos de Application. Al terminar,
la solución compila con el modelo nuevo (formato 3, `TrialRecord`, `LicenseStatus` rediseñado).

**⚠️ CRITICAL**: Ninguna historia puede empezar hasta terminar esta fase.

### Domain

- [X] T003 Ampliar el enum `LicensedModule` en src/Pos.Domain/Licensing/LicensedModule.cs a `Pos`, `Inventory`, `AdvancedReports`, `CreditAndCustomers`, `CashShifts`, `Returns`, `Suppliers`, `Discounts`, `Categories` (el valor numérico no se persiste; la identidad es el GUID)
- [X] T004 Reescribir src/Pos.Domain/Licensing/ModuleCatalog.cs con las constantes de los 9 módulos (GUID, key, orden, base) de data-model.md y la API `All` (en orden del catálogo), `Base` (= `Pos`), `IdOf`, `KeyOf`, `OrderOf`, `TryGetModule(Guid)`; los 6 GUID existentes no cambian (FR-003)
- [X] T005 [P] Crear `ModuleGrant` en src/Pos.Domain/Licensing/ModuleGrant.cs: record `(LicensedModule Module, DateOnly ActivatesOn, DateOnly? ExpiresOn)`; `ExpiresOn` "`null` = indefinido. Inclusivo."; `IsActiveOn(DateOnly d) => ActivatesOn <= d && (ExpiresOn is null || d <= ExpiresOn)`
- [X] T006 Crear (después de T005, usa `ModuleGrant`) `SignedLicense` en src/Pos.Domain/Licensing/SignedLicense.cs: record `(Guid LicenseId, DateTime IssuedAtUtc, string MachineId, string CustomerName, IReadOnlyList<ModuleGrant> Grants)` con `IsAcceptableReplacementFor(SignedLicense? current)` → `current is null` o `LicenseId == current.LicenseId` o `IssuedAtUtc > current.IssuedAtUtc`
- [X] T007 [P] Crear `TrialRecord` en src/Pos.Domain/Licensing/TrialRecord.cs (reemplaza a `LicenseRecord`): `MachineId`, `FirstRunUtc` ("Nunca aumenta; si la copia protegida está alterada = `DateTime.MinValue`"), `LastSeenUtc` ("Nunca retrocede"), `TrialDays` (30), `LicenseImportedUtc` (`DateTime?`; "Se fija al aceptar la primera licencia y nunca se borra; si tiene valor, la prueba ya no aplica", FR-026a); sin `Modules`. Eliminar src/Pos.Domain/Licensing/LicenseRecord.cs
- [X] T008 Rediseñar src/Pos.Domain/Licensing/LicenseStatus.cs: enums `LicenseOverall` (`Trial`, `Licensed`, `Blocked`), `LicenseBlockReason` (`TrialExpired`, `LicenseInvalid`, `BaseNotLicensed`, `BasePending`, `BaseExpired`), `ModuleState` (`Active`, `Pending`, `Expired`, `NotLicensed`), `LicenseWarning` (`None`, `Near`, `Urgent`); record `ModuleStatus(Module, State, DateOnly? ActivatesOn, DateOnly? ExpiresOn)`; record `LicenseStatus` con `Overall`, `BlockReason` (solo con `Blocked`), `TrialDaysRemaining` (0 fuera de prueba), `TrialWarning`, `CustomerName`, `Modules` (los 9 en orden del catálogo), `ExpiringSoon`, `ClockBehind`, `StoredLicenseRejected`; `IsBlocked => Overall == Blocked`; `IsModuleActive(m)`. Eliminar `LicensePhase`
- [X] T009 Rediseñar src/Pos.Domain/Licensing/LicenseEvaluator.cs con la firma `Evaluate(TrialRecord trial, SignedLicense? license, bool storedLicenseRejected, DateOnly today, DateOnly lastSeen)` siguiendo los pasos 1–5 de data-model.md (intersección `today`/`lastSeen` si `today < lastSeen`; `ClockBehind` si atraso > 1 día; licencia → `Licensed` o `Blocked` según el módulo base; sin licencia y con `storedLicenseRejected` **o** `trial.LicenseImportedUtc` → `Blocked/LicenseInvalid` (la prueba no se reanuda); sin licencia, sin licencia guardada rechazada y sin `LicenseImportedUtc` → prueba con días = `TrialDays − (max(today, lastSeen) − fecha(FirstRunUtc))`, avisos exactos de 5 y 1 día, `≤ 0` → `Blocked/TrialExpired`); estado por módulo con varias entradas según la regla de `ModuleStatus` (Active > Pending más próxima > Expired más reciente; una entrada con `ExpiresOn < ActivatesOn` cuenta siempre como Expired, nunca como Pending); `ExpiringSoon` = activos con `ExpiresOn` en `[hoy, hoy + 7]`
- [X] T010 [P] Agregar al final del enum `ManageCategories` y `ExportBackup` en src/Pos.Domain/Users/Permission.cs y asignarlos en src/Pos.Domain/Users/RolePermissions.cs (`ManageCategories` = mismos roles que `ManageProducts`; `ExportBackup` = solo Administrador)
- [X] T011 Crear `LicenseLock` en src/Pos.Domain/Licensing/LicenseLock.cs con `ExemptPermissions`: `ManageLicense`, `ExportBackup`, `Sell`, `ApplyDiscounts`, `ApproveDiscounts`, `SellOnCredit`, `ApproveCreditOverLimit`, `OperateShift`, `ManageShifts` (requiere T010)

### Application (puertos y estado)

- [X] T012 Agregar el error `SystemNotActivated(LicenseBlockReason Reason)` en src/Pos.Application/Abstractions/Error.cs junto a `ModuleNotLicensed`. Agregar `AccessControl.CheckSessionAsync(CancellationToken)` en src/Pos.Application/Users/Access/AccessControl.cs: exige solo sesión iniciada (mismo rechazo que `CheckAsync` sin sesión), sin regla de permiso, de bloqueo ni de módulo; lo usa `ExportLicenseRequest` (T040)
- [X] T013 Cambiar src/Pos.Application/Licensing/ILicenseVerifier.cs a `Verify(string content, string machineId) : LicenseVerification` con `Valid(SignedLicense)` / `Rejected(LicenseImportRejection)` y `LicenseImportRejection` = `Unreadable`, `UnsupportedFormat`, `BadSignature`, `OtherMachine`, `NotNewer`
- [X] T014 [P] Crear src/Pos.Application/Licensing/IInstalledLicenseStore.cs: `Task<string?> ReadAsync(CancellationToken)`, `Task ReplaceAsync(string content, DateTime importedAtUtc, CancellationToken)`
- [X] T015 [P] Crear src/Pos.Application/Licensing/IModuleCatalogInfo.cs: `int CatalogVersion`, `string NameOf(LicensedModule)`, `string DescriptionOf(LicensedModule)`
- [X] T016 Cambiar src/Pos.Application/Licensing/ILicenseStore.cs para guardar y leer `TrialRecord` (sin módulos) y eliminar `LicenseLoadResult.LegacyV1`; el resultado de lectura válido incluye `HadLegacyModules : bool` (archivo de prueba v2 con módulos de 011/012, FR-021); ajustar src/Pos.Application/Licensing/ILicenseSealStore.cs a `TrialRecord`
- [X] T017 Reescribir `ILicenseState`/`LicenseState` en src/Pos.Application/Licensing/ILicenseState.cs: `Current : LicenseStatus` (evalúa con `IClock` y fecha local; cacheado por fecha local y por registro), `Trial : TrialRecord?`, `License : SignedLicense?`, `Set(TrialRecord, SignedLicense?, bool storedRejected)`, `Refresh()` que compara una huella completa (estado general + estado de cada módulo + avisos) y dispara `Changed` solo si cambió; quitar `Record` y `EnabledModules` si dejan de usarse
- [X] T018 Adaptar a los tipos nuevos (sin cambiar aún su comportamiento de negocio) todos los consumidores para que la solución compile: src/Pos.Application/Licensing/LicenseBootstrapper.cs, src/Pos.Application/Licensing/ImportLicense/ImportLicenseHandler.cs, src/Pos.Application/Licensing/GetLicenseStatus/GetLicenseStatusHandler.cs, src/Pos.Infrastructure/Licensing/LicenseFileStore.cs, src/Pos.Infrastructure/Licensing/LicenseSealStore.cs, src/Pos.Infrastructure/Licensing/EcdsaLicenseVerifier.cs (stub que devuelve `UnsupportedFormat` hasta US2), src/Pos.Desktop/Licensing/LicenseCard.cs, src/Pos.Desktop/Licensing/LicenseMessages.cs, src/Pos.Desktop/Home/HomeViewModel.cs, src/Pos.Desktop/About/AboutViewModel.cs, src/Pos.Desktop/Navigation/MenuViewModel.cs, src/Pos.Desktop/Navigation/Navigator.cs, src/Pos.Desktop/Navigation/NavigationRegistry.cs (reemplazar usos de `LicensePhase`, `LicenseRecord` y `EnabledModules`)
- [X] T019 Actualizar los dobles de prueba al modelo nuevo para que compilen: tests/Pos.Application.Tests/Licensing/LicenseTestDoubles.cs, tests/Pos.Infrastructure.Tests/Licensing/TestLicenseIssuer.cs (emitir sobres formato 3 `{format, payload, signature}` con claves en memoria), tests/Pos.Infrastructure.Tests/TestSupport/SalesTestSupport.cs, tests/Pos.Infrastructure.Tests/TestSupport/ShiftTestSupport.cs, tests/Pos.Infrastructure.Tests/TestSupport/ReturnsTestSupport.cs; eliminar tests/Pos.Infrastructure.Tests/Licensing/LegacyFile.cs y las pruebas de formato 2 / `LegacyV1` que ya no aplican
- [X] T020 Crear pruebas del evaluador base en tests/Pos.Domain.Tests/Licensing/LicenseEvaluatorTests.cs (reemplazar las de 012): licencia con POS activo → `Licensed`; sin licencia día 1 → `Trial` con 30 días y 9 módulos activos; `Modules` siempre con 9 en orden del catálogo
- [X] T021 Ejecutar `dotnet build -v q` y `dotnet test --verbosity quiet`; dejar la solución en verde (suite completa justificada: la Fase 2 modifica todos los proyectos de pruebas)

**Checkpoint**: Modelo nuevo compilando; las historias pueden comenzar.

---

## Phase 3: User Story 1 - Catálogo compartido de módulos (Priority: P1) 🎯 MVP

**Goal**: Catálogo único de 9 módulos idéntico al contrato; Proveedores y Categorías controlados por licencia.

**Independent Test**: La prueba de consistencia catálogo↔JSON pasa; con una licencia sin Proveedores/Categorías, sus opciones desaparecen y sus operaciones se rechazan con `ModuleNotLicensed` sin modificar datos.

### Tests for User Story 1

- [X] T022 [P] [US1] Prueba de consistencia en tests/Pos.Infrastructure.Tests/Licensing/ModuleCatalogContractTests.cs: lee el recurso embebido `Pos.ModuleCatalog.json` y exige igualdad exacta con `ModuleCatalog` (cantidad 9, GUID, key, orden, único base = POS) y que los 6 GUID históricos (Inventory, AdvancedReports, CreditAndCustomers, CashShifts, Returns, Discounts) no cambien
- [X] T023 [P] [US1] Actualizar tests/Pos.Domain.Tests/Licensing/ModuleCatalogTests.cs (`All` en orden, `Base`, `TryGetModule` de GUID desconocido → falso) y tests/Pos.Domain.Tests/Licensing/ModuleAccessTests.cs con la tabla completa de `RequiredModules` de data-model.md (incluye `RegisterPurchases`/`VoidPurchases` → Suppliers + Inventory; `ManageCategories` → Categories; permisos sin módulo → vacío)
- [X] T024 [P] [US1] Pruebas en tests/Pos.Application.Tests/Licensing/LicenseBlockingTests.cs: con Suppliers inactivo `CheckAsync(ManageSuppliers)` y `CheckAsync(RegisterPurchases)` → `ModuleNotLicensed(Suppliers)`; con Inventory inactivo y Suppliers activo `RegisterPurchases` → `ModuleNotLicensed(Inventory)`; con Categories inactivo `ManageCategories` → `ModuleNotLicensed(Categories)`
- [X] T025 [P] [US1] Pruebas SQLite real en tests/Pos.Infrastructure.Tests/Categories/CategoriesLicenseTests.cs: con Categories inactivo, `ListCategoryOptions` devuelve vacío, crear/editar categoría se rechaza sin cambios en la base y `UpdateProduct` conserva el `CategoryId` actual del producto

### Implementation for User Story 1

- [X] T026 [US1] Reemplazar `ModuleAccess.Required` por `RequiredModules(Permission) : IReadOnlyList<LicensedModule>` (vacío = siempre disponible) con la tabla de data-model.md en src/Pos.Domain/Licensing/ModuleAccess.cs
- [X] T027 [US1] Usar `RequiredModules` en src/Pos.Application/Users/Access/AccessControl.cs (`CheckAsync` y `HasAsync`: el primer módulo requerido inactivo → `ModuleNotLicensed(module)`) y en los demás usos de `ModuleAccess.Required` (src/Pos.Application/DependencyInjection.cs, src/Pos.Desktop/Navigation/NavigationRegistry.cs, src/Pos.Desktop/Navigation/Navigator.cs, src/Pos.Desktop/Navigation/NavigationServiceCollectionExtensions.cs)
- [X] T028 [P] [US1] Crear `EmbeddedModuleCatalogInfo : IModuleCatalogInfo` en src/Pos.Infrastructure/Licensing/EmbeddedModuleCatalogInfo.cs que lee `Pos.ModuleCatalog.json` del ensamblado una vez (versión, nombre y descripción por GUID) y registrarlo como singleton en la composición de Infrastructure
- [X] T029 [US1] Cambiar `Permission.ManageProducts` → `Permission.ManageCategories` en todos los handlers de src/Pos.Application/Categories/ (CreateCategory, UpdateCategory, DeleteCategory, GetCategory, SearchCategories, SetCategoryActive) y sus comentarios
- [X] T030 [US1] `ListCategoryOptions` en src/Pos.Application/Categories/ListCategoryOptions/ devuelve lista vacía si `ILicenseState.Current.IsModuleActive(Categories)` es falso
- [X] T031 [US1] En src/Pos.Application/Products/UpdateProduct/ conservar el `CategoryId` actual del producto (ignorar el enviado) cuando Categories está inactivo; revisar CreateProduct para no asignar categoría en ese caso
- [X] T032 [US1] En src/Pos.Desktop/Categories/ y src/Pos.Desktop/Products/ ocultar el campo y los filtros de categoría cuando Categories está inactivo; la página de Categorías usa `ManageCategories` como permiso de navegación
- [X] T033 [US1] Ejecutar solo las pruebas de los proyectos modificados en US1 (Principio VI): `dotnet test tests/Pos.Domain.Tests --verbosity quiet`, `dotnet test tests/Pos.Application.Tests --verbosity quiet` y `dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet`

**Checkpoint**: Catálogo de 9 módulos activo y verificado contra el contrato.

---

## Phase 4: User Story 2 - Contrato del formato de licencia (Priority: P1)

**Goal**: El POS verifica exactamente lo que OctopusAdmin firma (formato 3, sin canonicalización).

**Independent Test**: El vector de prueba de `contracts/license-format.md` §7 se acepta con su clave de prueba; cambiar un byte de `payload` lo rechaza.

### Tests for User Story 2

- [X] T034 [P] [US2] Reescribir tests/Pos.Infrastructure.Tests/Licensing/EcdsaLicenseVerifierTests.cs: vector del contrato §7 (clave pública de prueba, machineId `3f5a…2468`) → `Valid` con POS indefinido e Inventory hasta 2027-09-30; un byte de `payload` cambiado → `BadSignature`; cada paso 1–6 de §5 con su rechazo (archivo > 64 KiB / no JSON / sin `format` → `Unreadable`; un `.lic` de formato 2 real de 012 (`{format: 2, machineId, issuedUtc, modules, signature}`, sin `payload`) → `UnsupportedFormat`; `format: 3` sin `payload` o sin `signature` → `Unreadable`; Base64 inválido o firma ≠ 64 bytes → `Unreadable`; firma DER → `Unreadable` o `BadSignature`; contenido sin campo obligatorio, `issuedAtUtc` sin formato `yyyy-MM-ddTHH:mm:ssZ` o `formatVersion` ≠ 3 → `Unreadable`; otro `machineId` → `OtherMachine`); GUID de módulo desconocido ignorado (FR-022); `expiresOn` nulo o ausente = indefinido
- [X] T035 [P] [US2] Pruebas en tests/Pos.Domain.Tests/Licensing/ModuleGrantTests.cs: vigencia inclusiva (vence el 15 → activo el 15, inactivo el 16), sin vencimiento siempre activo desde la activación, `ExpiresOn < ActivatesOn` nunca activo

### Implementation for User Story 2

- [X] T036 [US2] Reescribir src/Pos.Infrastructure/Licensing/EcdsaLicenseVerifier.cs según `contracts/license-format.md` §2–§5 pasos 1–6: sobre JSON ≤ 64 KiB, `format` = 3 comprobado antes de exigir `payload`/`signature` (§5 paso 2), Base64 estándar, firma P1363 de 64 bytes con `ECDsa.VerifyData(bytes, firma, HashAlgorithmName.SHA256)` sobre `Base64Decode(payload)`, lectura del contenido después de verificar (fechas con formatos exactos, `licenseId` GUID `D`, `customerName` 1–200 caracteres), `machineId` ordinal; GUID desconocidos descartados con `ModuleCatalog.TryGetModule`; mantener `POS_LICENSE_DEV_PUBLIC_KEY` solo en DEBUG
- [X] T037 [US2] Eliminar src/Pos.Infrastructure/Licensing/LicenseCanonical.cs y `ExtendedGrant` (y cualquier referencia restante)
- [X] T038 [US2] Ejecutar las pruebas de US2

**Checkpoint**: Verificador v3 interoperable con el contrato.

---

## Phase 5: User Story 3 - Solicitud de licencia (Priority: P1)

**Goal**: Generar `.octoreq` (formato 2) y copiar el ID de máquina desde "Ayuda > Licencia".

**Independent Test**: El `machineId` del `.octoreq` es igual al mostrado en pantalla y al que usa el verificador.

### Tests for User Story 3

- [X] T039 [P] [US3] Prueba en tests/Pos.Application.Tests/Licensing/ExportLicenseRequestHandlerTests.cs (conformidad con el contrato compartido `contracts/license-request.md`, que OctopusAdmin lee; no es una prueba de mapeo): el archivo contiene `requestFormat: 2`, `machineId` igual al de `IMachineIdProvider` (FR-016), `businessName` del perfil del negocio (texto vacío si no hay), `appVersion`, `catalogVersion` de `IModuleCatalogInfo` y `createdAtUtc` con formato `yyyy-MM-ddTHH:mm:ssZ`; JSON UTF-8 sin BOM, sin cifrar ni firmar; cualquier usuario con sesión la genera, sin `ManageLicense` y también en bloqueo; sin sesión → rechazo

### Implementation for User Story 3

- [X] T040 [US3] Actualizar src/Pos.Application/Licensing/ExportLicenseRequest/ExportLicenseRequestHandler.cs al formato de `contracts/license-request.md` (`requestFormat` 2, `businessName` de `IBusinessProfileRepository`, `catalogVersion` de `IModuleCatalogInfo`); cambia `CheckAsync(ManageLicense)` por `CheckSessionAsync` (T012): la solicitud no es secreta (FR-014; blocked-mode §1)
- [X] T041 [US3] Crear la página `help.license` (grupo Ayuda, orden 0; "Acerca de" pasa a orden 1; sin permiso de navegación) en src/Pos.Desktop/Licensing/LicenseViewModel.cs, src/Pos.Desktop/Licensing/LicenseView.axaml(+.cs) y registrarla en src/Pos.Desktop/Licensing/LicenseModule.cs con: ID de máquina + botón "Copiar" (portapapeles) y botón "Generar solicitud" (diálogo con extensión `.octoreq`, visible para cualquier usuario con sesión)
- [X] T042 [US3] Quitar de src/Pos.Desktop/About/AboutViewModel.cs y src/Pos.Desktop/About/AboutView.axaml las acciones de importar licencia y generar solicitud; conservar el ID de máquina y agregar un enlace a "Ayuda > Licencia"; cambiar el orden en src/Pos.Desktop/About/AboutModule.cs
- [X] T043 [US3] Agregar los textos de la página (títulos, "Copiar", "Generar solicitud") en src/Pos.Desktop/Resources/Strings.resx

**Checkpoint**: Solicitud generable desde la nueva página.

---

## Phase 6: User Story 4 - Importar y verificar la licencia (Priority: P1)

**Goal**: Importar la licencia, guardarla tal cual en `InstalledLicenses` y reverificarla en cada arranque.

**Independent Test**: Importar licencias válida, alterada, de otra máquina, no más reciente, de formato 2 y la misma otra vez; editar `InstalledLicenses.Content` y reiniciar.

### Tests for User Story 4

- [X] T044 [P] [US4] Reescribir tests/Pos.Application.Tests/Licensing/ImportLicenseHandlerTests.cs: orden de verificación (firma → máquina → antigüedad) y primer fallo con su rechazo; `NotNewer` con otro id y `issuedAtUtc` igual o anterior; reimportación del mismo id aceptada sin cambios; reemplazo completo (Devoluciones desaparece al importar una más reciente sin él, FR-012); formato 2 → `UnsupportedFormat`; en cada rechazo la licencia vigente y el almacén no cambian; auditoría de la importación; `Changed` disparado al aceptar
- [X] T045 [P] [US4] Reescribir tests/Pos.Application.Tests/Licensing/LicenseBootstrapperTests.cs: licencia guardada válida → `Licensed`; guardada alterada o de otra máquina → no habilita módulos, `StoredLicenseRejected = true`, `Blocked/LicenseInvalid` (la prueba no se reanuda, porque `LicenseImportedUtc` tiene valor) y se registra `LicenseStoredRejected` en la bitácora; fila de `InstalledLicenses` borrada con `LicenseImportedUtc` fijado → `Blocked/LicenseInvalid`; licencia guardada válida con `LicenseImportedUtc` nulo (corte entre guardar la licencia y el sello) → `Licensed` y el bootstrapper fija y guarda `LicenseImportedUtc`; licencia guardada rechazada con `LicenseImportedUtc` nulo y sello ilegible (cambio de hardware) → `Blocked/LicenseInvalid`, no `TrialExpired`
- [X] T046 [P] [US4] Prueba SQLite real en tests/Pos.Infrastructure.Tests/Licensing/InstalledLicenseStoreTests.cs: `ReadAsync` sin fila → null; `ReplaceAsync` guarda el texto exacto byte a byte y deja una sola fila; un segundo `ReplaceAsync` reemplaza en una transacción
- [X] T047 [P] [US4] Prueba de migración en tests/Pos.Infrastructure.Tests/SampleDatabases/InstalledLicensesMigrationTests.cs: la base de ejemplo migra sin errores y la tabla `InstalledLicenses` existe vacía

### Implementation for User Story 4

- [X] T048 [P] [US4] Crear `InstalledLicenseEntity` en src/Pos.Infrastructure/Licensing/InstalledLicenseEntity.cs (`Id` GUID PK con `Guid.CreateVersion7()`, "una sola fila"; `Content` TEXT no nulo, "Texto exacto del `.lic` importado (≤ 64 KiB)"; `ImportedAtUtc` UTC) y su configuración en src/Pos.Infrastructure/Persistence/Configurations/InstalledLicenseConfiguration.cs (sin auditoría, borrado lógico ni versión, como `LicenseSealConfiguration`)
- [X] T049 [US4] Agregar `DbSet<InstalledLicenseEntity> InstalledLicenses` en src/Pos.Infrastructure/Persistence/PosDbContext.cs y generar la migración `InstalledLicenses` en src/Pos.Infrastructure/Persistence/Migrations/ (actualiza `PosDbContextModelSnapshot.cs`)
- [X] T050 [US4] Crear `InstalledLicenseStore : IInstalledLicenseStore` en src/Pos.Infrastructure/Licensing/InstalledLicenseStore.cs (reemplazo atómico de la única fila) y registrarlo en la composición de Infrastructure
- [X] T051 [US4] Reescribir src/Pos.Application/Licensing/ImportLicense/ImportLicenseHandler.cs: exige `ManageLicense`; lee el archivo; `ILicenseVerifier.Verify` (pasos 1–6); paso 7 con `SignedLicense.IsAcceptableReplacementFor(ILicenseState.License)` → `NotNewer`; guarda el texto exacto con `IInstalledLicenseStore.ReplaceAsync`; si `TrialRecord.LicenseImportedUtc` es nulo, lo fija con la hora actual y lo guarda en archivo y sello (FR-026a); `ILicenseState.Set(trial, license, false)`; audita y registra en log la importación y los rechazos; ya no toca módulos del `TrialRecord`
- [X] T052 [US4] Extender src/Pos.Application/Licensing/LicenseBootstrapper.cs para leer `IInstalledLicenseStore`, reverificar (pasos 1–6) con el ID de máquina actual y llamar a `ILicenseState.Set(trial, licenseOrNull, storedRejected)`; si la licencia guardada es válida y `TrialRecord.LicenseImportedUtc` es nulo, fijarlo con la hora actual y guardarlo en archivo y sello (FR-026a); registrar `LicenseStoredRejected` en la bitácora y log estructurado; nunca lanzar (se mantiene el `try/catch`)
- [X] T053 [US4] Agregar a la página src/Pos.Desktop/Licensing/LicenseViewModel.cs y LicenseView.axaml el botón "Importar licencia" (diálogo `.lic`, solo con `ManageLicense`) y mostrar los mensajes de rechazo de blocked-mode.md §3 ("Rechazos de importación" + "Se conserva la licencia actual."); si la importación se acepta y el sistema queda activo, mostrar "Licencia importada. Los módulos se aplicaron."; si se acepta pero el estado resultante es `Blocked` (p. ej., licencia ya caducada por completo o sin POS), mostrar "Licencia importada, pero el sistema sigue bloqueado: {causa}." (blocked-mode §3, "Importación aceptada") desde src/Pos.Desktop/Licensing/LicenseMessages.cs y src/Pos.Desktop/Resources/Strings.resx
- [X] T054 [US4] En Inicio (src/Pos.Desktop/Licensing/LicenseCard.cs) y en la página de licencia, la licencia guardada rechazada se comunica solo con el mensaje de bloqueo `LicenseInvalid` de blocked-mode §3 (con `StoredLicenseRejected` el estado siempre es `Blocked/LicenseInvalid`); quitar de src/Pos.Desktop/Licensing/LicenseMessages.cs y src/Pos.Desktop/Resources/Strings.resx el aviso aparte de 012, si existe, para no duplicar el mensaje

**Checkpoint**: Licencia importable, persistida y reverificada al arrancar.

---

## Phase 7: User Story 5 - Periodo de prueba (Priority: P1)

**Goal**: 30 días con todos los módulos, avisos de 5 y 1 día, fecha de inicio protegida.

**Independent Test**: Días 25, 29, 30 y 31; alterar `LicenseSeals`.

### Tests for User Story 5

- [X] T055 [P] [US5] Pruebas en tests/Pos.Domain.Tests/Licensing/LicenseEvaluatorTests.cs: día 1 → 30 días; 5 días restantes → `Near`; 1 día → `Urgent`; otros días → `None`; día 31 → `Blocked/TrialExpired`; `FirstRunUtc = DateTime.MinValue` → `TrialExpired`; licencia presente → la prueba deja de aplicar aunque le queden días; día 10 sin licencia válida y con `LicenseImportedUtc` → `Blocked/LicenseInvalid` (no vuelve la prueba); día 10 con `storedLicenseRejected = true` y `LicenseImportedUtc` nulo → `Blocked/LicenseInvalid`
- [X] T056 [P] [US5] Actualizar tests/Pos.Infrastructure.Tests/Licensing/LicenseFileStoreTests.cs: escribe/lee el archivo de prueba v3 sin módulos (con `LicenseImportedUtc`); un archivo de prueba v2 conserva fechas, descarta módulos y devuelve `HadLegacyModules = true` si tenía alguno (`false` si estaba vacío); el archivo de prueba v1 ya no se lee
- [X] T057 [P] [US5] Actualizar tests/Pos.Infrastructure.Tests/Licensing/LicenseSealStoreTests.cs: distingue fila ausente de fila alterada (`LicenseSealReadResult`); guarda y lee `LicenseImportedUtc`; un sello de 012 sin ese campo se lee como `Valid` con `LicenseImportedUtc` nulo
- [X] T058 [P] [US5] Pruebas de reconciliación en tests/Pos.Application.Tests/Licensing/LicenseBootstrapperTests.cs: sello alterado → `FirstRunUtc = DateTime.MinValue`; solo archivo alterado → se recupera desde el sello; faltan ambos → se usa `IInstallationAgeReader`; instalación existente conserva su `FirstRunUtc` (FR-026); `LicenseImportedUtc` presente en el archivo o en el sello se conserva (nunca vuelve a nulo); archivo de prueba v2 con `HadLegacyModules` → `LicenseImportedUtc` = ahora, `StoredLicenseRejected = true`, `Blocked/LicenseInvalid` y `LicenseStoredRejected` en la bitácora (caso límite "licencia guardada de formato 2"); archivo de prueba v2 sin módulos → sigue la prueba sin aviso

### Implementation for User Story 5

- [X] T059 [US5] src/Pos.Infrastructure/Licensing/LicenseFileStore.cs: archivo de prueba v3 con `MachineId`, `FirstRunUtc`, `LastSeenUtc`, `TrialDays`, `LicenseImportedUtc`; lee el archivo de prueba v2 descartando módulos y marcando `HadLegacyModules` si tenía alguno; deja de leer el archivo de prueba v1; renombrar `AppSecret` → `KeyContext` con comentario de que solo detecta ediciones casuales (research §4)
- [X] T060 [US5] src/Pos.Infrastructure/Licensing/LicenseSealStore.cs: `AppSecret` → `KeyContext`; agregar `DateTime? LicenseImportedUtc` a `SealContent` (JSON cifrado dentro de `Payload`: no requiere migración; ausente = nulo); devolver `LicenseSealReadResult` (`Missing`, `Tampered`, `Valid(TrialRecord)`) y ajustar `ILicenseSealStore` en src/Pos.Application/Licensing/ILicenseSealStore.cs
- [X] T061 [US5] Reconciliación en src/Pos.Application/Licensing/LicenseBootstrapper.cs: sello `Tampered` → prueba vencida (`FirstRunUtc = DateTime.MinValue`); `FirstRunUtc` nunca aumenta, `LastSeenUtc` nunca retrocede y `LicenseImportedUtc` nunca vuelve a nulo (se toma de la copia que lo tenga); con `HadLegacyModules` fija `LicenseImportedUtc`, reescribe el archivo de prueba como v3 y el sello, y pasa `storedRejected = true` a `ILicenseState.Set`
- [X] T062 [US5] Mantener los avisos de prueba (`License_NearExpiry`, `License_NearExpiryOne`) con el nuevo `TrialWarning` en src/Pos.Desktop/Licensing/LicenseCard.cs y src/Pos.Desktop/Licensing/LicenseMessages.cs
- [X] T063 [US5] Revisar/actualizar tests/Pos.Infrastructure.Tests/SampleDatabases/ModularLicenseMigrationTests.cs para que siga pasando con el modelo sin módulos en el registro de prueba

**Checkpoint**: Prueba de 30 días protegida.

---

## Phase 8: User Story 6 - Bloqueo total sin módulo POS (Priority: P1)

**Goal**: Sin POS activo solo quedan inicio de sesión, licencia, respaldo, la venta en curso y el cierre del turno abierto.

**Independent Test**: Las cinco causas de bloqueo (`TrialExpired`, `LicenseInvalid`, `BaseNotLicensed`, `BasePending`, `BaseExpired`); toda operación no exenta se rechaza por menú y por invocación directa; la venta en curso se puede terminar y el turno cerrar.

### Tests for User Story 6

- [X] T064 [P] [US6] Pruebas del evaluador en tests/Pos.Domain.Tests/Licensing/LicenseEvaluatorTests.cs: licencia sin POS → `BaseNotLicensed`; POS solo futuro → `BasePending`; POS vencido ayer con Inventory vigente → `BaseExpired`; POS con única entrada `ExpiresOn < ActivatesOn` y activación futura → `BaseExpired`; con `Blocked` los módulos conservan su estado individual
- [X] T065 [P] [US6] Pruebas en tests/Pos.Application.Tests/Licensing/LicenseBlockingTests.cs: en bloqueo `CheckAsync` de cada permiso no exento → `SystemNotActivated(reason)` y `HasAsync` → falso; los de `LicenseLock.ExemptPermissions` pasan a la regla de rol y de módulo (p. ej. `SellOnCredit` sigue exigiendo CreditAndCustomers); `CheckToFinishAsync` en bloqueo y con el módulo inactivo → permitido si el rol tiene el permiso, rechazado sin sesión o sin rol; apoyos de la venta en curso en bloqueo (blocked-mode §1): `ResolveCoupon`, `ApproveDiscount` y `GetDiscountSettings` con Discounts activo → permitidos, con Discounts inactivo → `ModuleNotLicensed(Discounts)`; `FindCustomersForSale` y `GetCustomerCreditStatus` con CreditAndCustomers activo → permitidos, inactivo → `ModuleNotLicensed(CreditAndCustomers)`
- [X] T066 [P] [US6] Pruebas SQLite real en tests/Pos.Infrastructure.Tests/Licensing/BlockedSaleInProgressTests.cs: en bloqueo `SaveSaleDraft`/`ConfirmSale` con el `DraftId` del borrador guardado con líneas → éxito (venta confirmada y cobrada); con otro `DraftId` → `SystemNotActivated` sin cambios; `DiscardSaleDraft` permitido; venta abierta antes de un reinicio se puede terminar; `PrintTicket` original de esa venta → éxito y reimpresión → `SystemNotActivated`; borrador a crédito con cliente asignado y borrador con descuento aplicado se cobran con CreditAndCustomers/Discounts vencidos, pero aplicar un descuento nuevo o cambiar a crédito respecto del borrador guardado → `ModuleNotLicensed`; el mismo caso **sin bloqueo** (POS activo y Discounts vencido con el borrador abierto) → se cobra con su descuento
- [X] T067 [P] [US6] Pruebas SQLite real en tests/Pos.Infrastructure.Tests/Licensing/OpenShiftLicenseTests.cs: en bloqueo `OpenShift` y `RegisterCashMovement` → `SystemNotActivated`; flujo completo del turno abierto `GetCurrentShift` → `CountShiftCash` → `CloseShift` → `PrintTicket` original del corte → éxito, **también con CashShifts inactivo** (licencia sin POS ni CashShifts importada con turno abierto) y sin bloqueo con solo CashShifts vencido; reimpresión del corte → rechazo; `OpenCashDrawer` sin venta, `SearchShifts` y `GetShiftCut` de un turno cerrado → `SystemNotActivated`
- [X] T068 [P] [US6] Prueba en tests/Pos.Infrastructure.Tests/Backup/ExportBackupTests.cs: `ExportBackupHandler` copia la base completa al destino (abrible y con los mismos datos) en bloqueo; exige `ExportBackup`

### Implementation for User Story 6

- [X] T069 [US6] Regla de bloqueo en src/Pos.Application/Users/Access/AccessControl.cs: si `Current.IsBlocked` y el permiso no está en `LicenseLock.ExemptPermissions` → `SystemNotActivated(BlockReason)` (antes de la regla de módulo); `HasAsync` → falso; log estructurado del rechazo. Agregar `CheckToFinishAsync(Permission)` (sesión y rol, sin regla de bloqueo ni de módulo; blocked-mode §1 "Terminar trabajo ya iniciado")
- [X] T070 [US6] En src/Pos.Application/Sales/SaveSaleDraft/ y src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs: en bloqueo solo aceptar el `DraftId` del borrador guardado en `ISaleDraftStore` con líneas; otro → `SystemNotActivated`. Siempre (con o sin bloqueo), para el borrador guardado comparar el comando con el borrador de `ISaleDraftStore`: las partes que coinciden (descuentos, cliente, crédito) se autorizan con `CheckToFinishAsync`; las agregadas o cambiadas, con `CheckAsync` (blocked-mode §1 "Partes ya capturadas"). En src/Pos.Application/Printing/PrintTicket/PrintTicketHandler.cs, la impresión original de la venta propia (`ViewOwnSales`) y la del corte del turno recién cerrado (`ShiftCutSource`) usan `CheckToFinishAsync`; en bloqueo, las reimpresiones y las impresiones de turnos anteriores → `SystemNotActivated`
- [X] T071 [US6] En src/Pos.Application/CashShifts/OpenShift/ y src/Pos.Application/CashShifts/RegisterCashMovement/: en bloqueo rechazar con `SystemNotActivated`. En src/Pos.Application/CashShifts/GetCurrentShift/, GetShiftDetail (solo el turno abierto), src/Pos.Application/CashShifts/CountShiftCash/ y src/Pos.Application/CashShifts/CloseShift/, para el turno abierto usar `CheckToFinishAsync` siempre, con o sin bloqueo (se cierra aunque CashShifts esté inactivo). En bloqueo rechazar con `SystemNotActivated`: src/Pos.Application/Printing/OpenCashDrawer/ sin venta, SearchShifts, SearchShiftCuts, GetShiftCut y GetShiftDetail de turnos cerrados, src/Pos.Application/Reports/ListMyShifts/ y src/Pos.Application/Reports/GetMyShiftSummary/
- [X] T072 [P] [US6] Crear `ExportBackupCommand(DestinationFilePath)` y `ExportBackupHandler` en src/Pos.Application/Backup/ExportBackup/ (permiso `ExportBackup`, usa `IBackupService.CreateTemporaryCopyAsync` y mueve la copia al destino; log y auditoría) y registrarlo en src/Pos.Application/DependencyInjection.cs
- [X] T073 [US6] Menú en bloqueo en src/Pos.Desktop/Navigation/NavigationRegistry.cs, src/Pos.Desktop/Navigation/MenuViewModel.cs y src/Pos.Desktop/Navigation/Navigator.cs: solo "Inicio" (página de entrada en bloqueo, con el mensaje de activación de T074) y "Ayuda > Licencia", y además "Punto de venta" mientras el usuario tenga borrador con líneas y "Turno" mientras haya turno abierto; las páginas de arqueo y cierre de caja (src/Pos.Desktop/CashShifts/CashModule.cs, permiso `OperateShift`) se muestran mientras haya turno abierto sin aplicar la regla de módulo, con o sin bloqueo (blocked-mode §2); reconstruir con `ILicenseState.Changed`
- [X] T074 [US6] Mensajes de bloqueo de blocked-mode.md §3 (cinco causas con los pasos de activación, "El sistema no está activado. Abre Ayuda > Licencia para activarlo.", "La licencia venció. Puedes terminar y cobrar esta venta y cerrar el turno; no se pueden iniciar ventas nuevas.") en src/Pos.Desktop/Resources/Strings.resx, src/Pos.Desktop/Licensing/LicenseMessages.cs, src/Pos.Desktop/Licensing/LicenseCard.cs (Inicio) y src/Pos.Desktop/Sales/PointOfSaleViewModel.cs (venta en curso / rechazo de venta nueva)
- [X] T075 [US6] Botón "Exportar respaldo" (solo con `ExportBackup`, diálogo de guardar) en src/Pos.Desktop/Licensing/LicenseView.axaml(+ViewModel) y en src/Pos.Desktop/About/AboutView.axaml(+ViewModel)

**Checkpoint**: Bloqueo efectivo en casos de uso y menú, con las garantías del Principio I.

---

## Phase 9: User Story 7 - Vencimiento y activación por módulo (Priority: P1)

**Goal**: Vigencia por módulo recalculada al arrancar, al iniciar sesión y al cambiar de día; avisos de vencimiento en Inicio.

**Independent Test**: Módulo con activación futura y otro por vencer; cruzar la medianoche con la app abierta.

### Tests for User Story 7

- [X] T076 [P] [US7] Pruebas en tests/Pos.Domain.Tests/Licensing/LicenseEvaluatorTests.cs: `Pending` antes de `ActivatesOn` y `Active` ese día; `Expired` el día siguiente al vencimiento; `NotLicensed` sin entrada; varias entradas (Active si alguna; si no Pending la más próxima; si no Expired la más reciente); entrada con `ExpiresOn < ActivatesOn` → `Expired` también antes de su `ActivatesOn` (caso límite de la spec); `ExpiringSoon` incluye vencimientos en `[hoy, hoy + 7]` y excluye `hoy + 8` y los no activos
- [X] T077 [P] [US7] Prueba en tests/Pos.Application.Tests/Licensing/LicenseStateTests.cs: `Current` cambia al avanzar el `IClock` a otro día; `Refresh` dispara `Changed` solo si cambió la huella (estado general, módulo o aviso), no en cada llamada
- [X] T078 [P] [US7] Prueba SQLite real en tests/Pos.Infrastructure.Tests/Licensing/ModuleInteractionTests.cs: con CreditAndCustomers vencido sus clientes y saldos siguen en la base y vuelven a estar disponibles al importar una licencia que lo renueva (FR-033); conservar los casos FR-036 existentes

### Implementation for User Story 7

- [X] T079 [US7] `LicenseBootstrapper.TouchAsync` en src/Pos.Application/Licensing/LicenseBootstrapper.cs: guarda `LastSeenUtc` (nunca retrocede) en archivo y sello y llama a `ILicenseState.Refresh()`
- [X] T080 [US7] src/Pos.Desktop/Composition/LicenseClockScheduler.cs: disparo en la próxima medianoche local + 5 s y además cada hora, llamando a `TouchAsync`; nunca lanza
- [X] T081 [US7] Llamar a `TouchAsync` al iniciar sesión desde la Shell en src/Pos.Desktop/Shell/
- [X] T082 [US7] Avisos "{Módulo} vence el {fecha}." (uno por módulo de `ExpiringSoon`, nombre de `IModuleCatalogInfo`) en src/Pos.Desktop/Licensing/LicenseCard.cs y src/Pos.Desktop/Home/HomeViewModel.cs, con texto en src/Pos.Desktop/Resources/Strings.resx

**Checkpoint**: Activaciones y vencimientos automáticos sin reiniciar.

---

## Phase 10: User Story 8 - Protección contra reloj atrasado (Priority: P1)

**Goal**: Atrasar el reloj nunca reactiva módulos ni extiende la prueba; aviso si el atraso es > 1 día.

**Independent Test**: Módulo vencido y prueba vencida; atrasar el reloj varios días.

### Tests for User Story 8

- [X] T083 [P] [US8] Pruebas en tests/Pos.Domain.Tests/Licensing/LicenseEvaluatorTests.cs: módulo vencido el 15, `lastSeen` 20, `today` 10 → sigue `Expired` y `ClockBehind`; prueba vencida con reloj atrasado dentro de los 30 días → sigue `TrialExpired`; atraso de 1 día → sin `ClockBehind` y sin reactivación; módulo que activa el 18 con `lastSeen` 20 y `today` 17 → no activo (intersección); `today ≥ lastSeen` → evaluación normal; días de prueba calculados con `max(today, lastSeen)`

### Implementation for User Story 8

- [X] T084 [US8] Confirmar en src/Pos.Domain/Licensing/LicenseEvaluator.cs la intersección y la bandera `ClockBehind` (`lastSeen.DayNumber - today.DayNumber > 1`) según research §8 y ajustar lo que falle con T083
- [X] T085 [US8] Aviso "La fecha del equipo es anterior a la última fecha registrada ({fecha}). Corrige la fecha y la hora del equipo." en src/Pos.Desktop/Licensing/LicenseCard.cs (Inicio) y en la página de licencia, con texto en src/Pos.Desktop/Resources/Strings.resx

**Checkpoint**: Reloj atrasado sin efecto sobre la vigencia.

---

## Phase 11: User Story 9 - Pantalla de licencia (Priority: P2)

**Goal**: "Ayuda > Licencia" muestra estado general, cliente, avisos y la tabla de los 9 módulos.

**Independent Test**: Licencia con módulos en los cuatro estados; prueba con 12 días; sistema bloqueado.

### Tests for User Story 9

- [X] T086 [P] [US9] Eliminar tests/Pos.Application.Tests/Licensing/GetLicenseStatusHandlerTests.cs (si existe) sin reemplazarla: `GetLicenseStatusHandler` solo arma el DTO a partir de `ILicenseState`, sin regla de acceso ni cálculo, y el Principio VI no pide probar mapeos ni DTOs. Las reglas que muestra la pantalla ya se prueban en el evaluador (T055, T064, T076, T083)

### Implementation for User Story 9

- [X] T087 [US9] Extender `LicenseStatusDto` y src/Pos.Application/Licensing/GetLicenseStatus/GetLicenseStatusHandler.cs con todo `LicenseStatus` + nombres del catálogo + ID de máquina + contacto; sin verificación de permiso
- [X] T088 [US9] Completar src/Pos.Desktop/Licensing/LicenseViewModel.cs y LicenseView.axaml: encabezado ("En prueba: N días restantes", "Licenciado", "Bloqueado: {causa}", cliente), avisos (reloj atrasado, prueba por vencer; la licencia guardada rechazada se muestra como causa de bloqueo) y tabla Módulo | Estado (Activo, Pendiente de activación, Vencido, No contratado) | Activación | Vencimiento ("Indefinido" si no tiene); se refresca con `ILicenseState.Changed`
- [X] T089 [US9] Textos de estados y columnas en src/Pos.Desktop/Resources/Strings.resx

**Checkpoint**: Todas las historias funcionales.

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Limpieza, arquitectura, documentación y validación final

- [X] T090 [P] Revisar tests/Pos.ArchitectureTests/LayerDependencyTests.cs: Domain no depende de JSON/BCL de criptografía ni de Infrastructure; agregar regla si falta para `Pos.Domain.Licensing`. Agregar la regla de que `Pos.Application.Licensing` y `Pos.Infrastructure.Licensing` no dependen de `System.Net.Http` ni de `System.Net.Sockets` (FR-042, sin conexión). `System.Net.NetworkInformation` sí se permite, porque `MachineIdProvider` lee la MAC localmente. Comprobar con `grep -rn "PRIVATE KEY\|ImportPkcs8PrivateKey\|ImportECPrivateKey" src/` que no haya claves privadas en el código de producción (FR-041); las claves de prueba se generan en memoria en tests/
- [X] T091 [P] Buscar y eliminar código muerto de 011/012: `LicensePhase`, `LicenseRecord`, `LegacyV1`, `LicenseCanonical`, `ExtendedGrant`, suma de módulos (`grep -rn` en src/ y tests/)
- [X] T092 [P] Actualizar el manual de usuario (sección de licencia) con "Ayuda > Licencia", solicitud, importación, bloqueo y exportación de respaldo en docs/manual-usuario/ (capítulo de licencia; regenerar con docs/manual-usuario/construir.py)
- [X] T093 Dejar el marcador/TODO documentado para `EcdsaLicenseVerifier.ProductionPublicKey` en src/Pos.Infrastructure/Licensing/EcdsaLicenseVerifier.cs: reemplazar con la clave pública de OctopusAdmin cuando se entregue (research §15; bloquea solo el release). Igual con `VendorContact.Default` en src/Pos.Application/Licensing/VendorContact.cs: hoy tiene los marcadores `"[teléfono]"` y `"[email]"`, que se ven en los mensajes de bloqueo y en "Ayuda > Licencia"; reemplazarlos con el contacto real del proveedor antes del release
- [X] T094 `dotnet build -v q` sin advertencias y `dotnet test --verbosity quiet` en verde
- [ ] T095 Ejecutar los escenarios manuales 1–14 de specs/025-coordinated-licensing/quickstart.md y la comprobación de interoperabilidad (§4)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias.
- **Foundational (Fase 2)**: depende de Setup; BLOQUEA todas las historias.
- **US1 (Fase 3)**: depende de Foundational.
- **US2 (Fase 4)**: depende de Foundational (usa `ModuleCatalog.TryGetModule`).
- **US3 (Fase 5)**: depende de Foundational y de `EmbeddedModuleCatalogInfo` (T028, US1). Crea la página `help.license`.
- **US4 (Fase 6)**: depende de US2 (verificador v3) y de la página de US3 (T041).
- **US5 (Fase 7)**: depende de Foundational; independiente de US2–US4.
- **US6 (Fase 8)**: depende de Foundational; los botones de UI usan la página de US3.
- **US7 (Fase 9)**: depende de Foundational; T078 usa la importación de US4.
- **US8 (Fase 10)**: depende de Foundational (evaluador).
- **US9 (Fase 11)**: depende de la página de US3; muestra datos de US4–US8.
- **Polish (Fase 12)**: depende de todas las historias.

### Archivos compartidos (no paralelizar entre historias)

- src/Pos.Domain/Licensing/LicenseEvaluator.cs y tests/Pos.Domain.Tests/Licensing/LicenseEvaluatorTests.cs (T009, T020, T055, T064, T076, T083, T084)
- src/Pos.Application/Users/Access/AccessControl.cs (T027, T069)
- src/Pos.Application/Licensing/LicenseBootstrapper.cs (T052, T061, T079)
- src/Pos.Desktop/Licensing/LicenseView*.cs/axaml, LicenseCard.cs, LicenseMessages.cs y Strings.resx (US3, US4, US5, US6, US7, US8, US9)

### Within Each User Story

- Pruebas primero y en rojo; luego implementación.
- Domain → Application → Infrastructure → Desktop.

### Parallel Opportunities

- Fase 2: T005, T007 y T010 (archivos distintos de Domain); T006 tras T005 y T011 tras T010; T014, T015.
- Pruebas marcadas [P] de cada historia entre sí.
- Tras Foundational: US2, US5 y US8 pueden avanzar en paralelo con US1 (tocan archivos distintos salvo `LicenseEvaluatorTests.cs`, que conviene editar en serie).

---

## Parallel Example: User Story 1

```bash
# Pruebas de US1 en paralelo:
Task: "Prueba de consistencia catálogo↔JSON en tests/Pos.Infrastructure.Tests/Licensing/ModuleCatalogContractTests.cs"
Task: "ModuleCatalogTests y ModuleAccessTests en tests/Pos.Domain.Tests/Licensing/"
Task: "Bloqueo por módulo de Proveedores/Categorías en tests/Pos.Application.Tests/Licensing/LicenseBlockingTests.cs"
Task: "Categorías con módulo inactivo en tests/Pos.Infrastructure.Tests/Categories/CategoriesLicenseTests.cs"

# Implementación independiente:
Task: "EmbeddedModuleCatalogInfo en src/Pos.Infrastructure/Licensing/EmbeddedModuleCatalogInfo.cs"
```

## Parallel Example: User Story 6

```bash
Task: "Bloqueo en AccessControl — tests/Pos.Application.Tests/Licensing/LicenseBlockingTests.cs"
Task: "Venta en curso en bloqueo — tests/Pos.Infrastructure.Tests/Licensing/BlockedSaleInProgressTests.cs"
Task: "Turno en bloqueo — tests/Pos.Infrastructure.Tests/Licensing/OpenShiftLicenseTests.cs"
Task: "Exportar respaldo — tests/Pos.Infrastructure.Tests/Backup/ExportBackupTests.cs"
Task: "ExportBackupHandler en src/Pos.Application/Backup/ExportBackup/"
```

---

## Implementation Strategy

### MVP

El valor mínimo entregable es el ciclo completo de licencia coordinada: **US1 + US2 + US4 + US6**
(catálogo, verificación v3, importación persistida y bloqueo sin POS), con US5 para no romper la
prueba de 30 días de instalaciones nuevas. US1 sola es el primer incremento verificable (prueba
de consistencia del contrato).

### Incremental Delivery

1. Setup + Foundational → modelo nuevo compilando.
2. US1 → catálogo verificado contra el contrato (Proveedores/Categorías por licencia).
3. US2 → vector del contrato aceptado.
4. US3 + US4 → solicitud e importación desde "Ayuda > Licencia".
5. US5 → prueba de 30 días protegida.
6. US6 → bloqueo total con garantías del Principio I.
7. US7 + US8 → vigencia por módulo y reloj atrasado.
8. US9 → pantalla completa.
9. Polish; liberación a producción solo con la clave pública de OctopusAdmin (T093).

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes.
- Las fechas de módulo son `DateOnly` de calendario local; los instantes, UTC.
- Versiones (glosario de data-model.md): "licencia formato 2/3" = `.lic` del proveedor; "archivo de prueba v1/v2/v3" = `license.lic` local de la prueba; "solicitud v2" = `.octoreq`. No usar "formato" ni "versión" sin el sustantivo.
- Ningún fallo de carga o verificación de licencia cierra la aplicación (Principio I).
- Commit tras cada tarea o grupo lógico.

## Notas de implementación

- `LicenseEvaluator.Evaluate` recibe `nowUtc` y la zona horaria en lugar de `today`/`lastSeen` (T009): necesita la
  zona para convertir también `FirstRunUtc` y `LastSeenUtc` a fecha local. Las reglas son las de data-model.
- Venta a crédito ya capturada (T066/T070): el borrador durable (`ISaleDraftStore`) guarda líneas y descuentos, pero no
  el cliente ni la forma de pago, así que la venta a crédito sigue la regla de módulo (`CheckAsync(SellOnCredit)`). Los
  descuentos sí se comparan con el borrador. Guardar el cliente en el borrador exigiría una migración y queda fuera.
- El cupón de una venta conservada se revalida al retomarla (015) con `ResolveCoupon`, que sigue la regla de módulo: con
  Descuentos vencido, el cupón se quita al retomar la venta. Los descuentos manuales se conservan.
- `GetSaleDraft` ya no quita los descuentos con el módulo Descuentos inactivo (FR-030a); se eliminó
  `RecoveredDraft.DiscountsDropped`.
- El menú en bloqueo lo decide `LicenseMenuPolicy` (Desktop), que consulta el borrador y el turno abierto al cambiar la
  licencia y al navegar.
