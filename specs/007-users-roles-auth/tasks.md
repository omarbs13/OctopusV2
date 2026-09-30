---

description: "Lista de tareas de la funcionalidad 007: usuarios, inicio de sesión y roles"
---

# Tareas: Usuarios, inicio de sesión y roles

**Entrada**: documentos de diseño en `specs/007-users-roles-auth/`

**Requisitos previos**: plan.md, spec.md, research.md, data-model.md, contracts/application-ports.md, contracts/ui.md, quickstart.md

**Pruebas**: se incluyen solo las que exige la política mínima del Principio VI (research §16). No hay pruebas de ViewModels, vistas, `IdleMonitor` ni del filtrado del menú; esas se validan a mano con quickstart.md.

**Organización**: las tareas se agrupan por historia de usuario. Cada historia se puede probar sola una vez terminada la Fase 2.

## Formato: `- [ ] [ID] [P?] [Historia] Descripción con ruta`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes).
- **[USn]**: historia de usuario de spec.md (solo en las fases de historia).
- Rutas relativas a la raíz del repositorio. Los textos de la interfaz van en español en `src/Pos.Desktop/Resources/Strings.resx`; el código, en inglés.
- Comandos: compilar con `dotnet build -v q`; probar con `dotnet test --verbosity quiet` (al implementar, solo el proyecto de pruebas modificado).

---

## Fase 1: Preparación

**Propósito**: dejar lista la línea base **antes** de tocar el esquema.

- [x] T001 Verificar la línea base: `dotnet build -v q` sin errores ni advertencias y `dotnet test --verbosity quiet` en verde, en la raíz del repositorio.
- [x] T002 Generar la base de ejemplo `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.5.0.db` **con el esquema actual (migración `BusinessProfile`), antes de crear `UsersAndRoles`**: amplía `SampleData` en `tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseGenerator.cs` para que la base traiga una fila en `SaleDrafts` (venta en curso con al menos una línea) además de las ventas y movimientos existentes, y ejecuta `POS_GENERATE_SAMPLE_DB=1 dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.SampleDatabases.SampleDatabaseGenerator"` (ver docs/migraciones.md). Confirma que el archivo `v0.5.0.db` quedó creado y en el repositorio.

---

## Fase 2: Fundamentos (bloquea todas las historias)

**Propósito**: dominio, puertos, esquema, sesión, control de acceso y estructura de la ventana principal que todas las historias necesitan.

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta terminar esta fase.

### Dominio (Pos.Domain)

- [x] T003 [P] Crear `src/Pos.Domain/Users/UserRole.cs`: enum `UserRole { Admin, Cashier }` con códigos de texto `ADMIN` y `CASHIER` (mismo estilo que `SaleStatus` en `src/Pos.Domain/Sales/SaleStatus.cs`).
- [x] T004 [P] Crear `src/Pos.Domain/Users/Permission.cs`: enum con exactamente `Sell`, `ViewOwnSales`, `ViewAllSales`, `ViewProducts`, `ManageProducts`, `ViewInventory`, `RegisterMovements`, `CancelSales`, `OpenDrawerWithoutSale`, `ManageUsers`, `ViewAuditLog`, `ManageSettings`, `ExportDiagnostics`.
- [x] T005 [P] Crear `src/Pos.Domain/Users/UserNameRules.cs`: estáticos `Normalize(string)` (= `Trim()` + `ToUpperInvariant()`) e `IsValid(string)`. Regla verbatim: "de 3 a 40 caracteres; solo letras, dígitos, `.`, `_` y `-`", sin espacios. Constantes `UserNameMinLength = 3`, `UserNameMaxLength = 40`.
- [x] T006 Crear `src/Pos.Domain/Users/RolePermissions.cs` (depende de T003, T004): única tabla rol → permisos (FR-012). `Admin` tiene todos los permisos; `Cashier` exactamente `Sell`, `ViewOwnSales`, `ViewProducts`, `ViewInventory`. Métodos `bool Has(UserRole, Permission)`, `IReadOnlySet<Permission> For(UserRole)` e `bool IsAuthorizable(Permission)` (verdadero solo para `CancelSales` y `OpenDrawerWithoutSale`).
- [x] T007 Crear `src/Pos.Domain/Users/User.cs` (depende de T003, T005). Campos y reglas de data-model.md: `Id` GUID v7; `FullName` "Obligatorio, sin espacios al inicio ni al final, de 1 a 100 caracteres" (`FullNameMaxLength = 100`); `UserName` (3 a 40); `NormalizedUserName` (`UserName.ToUpperInvariant()`, 40); `Role`; `IsActive`; `IsSystem`; `PasswordHash` (nulo solo en "Sistema", 200); `MustChangePassword`; `FailedLoginCount`; `LockoutEndsAt` (UTC); `LastLoginAt`; campos de auditoría (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `DeletedAt` siempre nulo) y `Version`. Seguir el patrón de `src/Pos.Domain/Products/Product.cs` para auditoría/versión. Métodos: `Create(fullName, userName, role, passwordHash)` (activo, `MustChangePassword = true`), `CreateFirstAdmin(fullName, userName, passwordHash)` (`Admin`, `MustChangePassword = false`), `Rename`, `ChangeUserName`, `ChangeRole`, `Activate`, `Deactivate`, `SetPassword(hash, mustChange)`, `IsLockedOut(nowUtc)`, `RegisterFailedLogin(nowUtc)` (devuelve `true` si con este fallo queda bloqueado), `RegisterSuccessfulLogin(nowUtc)`. Constantes `PasswordMinLength = 8`, `MaxFailedAttempts = 5`, `LockoutDuration = 5 min`. Al quinto fallo consecutivo `LockoutEndsAt = now + 5 min`; un acceso correcto reinicia el contador; al vencer el bloqueo el siguiente intento empieza con el contador en cero.
- [x] T008 [P] Modificar `src/Pos.Domain/Audit/AuditEntry.cs`: agregar `AuthorizedBy Guid?` y el parámetro opcional `authorizedBy = null` en `AuditEntry.Create(action, entityType, entityId, details, authorizedBy = null)`. Sigue siendo inmutable.
- [x] T009 [P] Modificar `src/Pos.Domain/Sales/SaleDraft.cs`: reemplazar `Slot` por `UserId` (`Guid`); `SaleDraft.Create(userId, draftId, linesJson, utcNow)`; `Replace` no cambia.

### Pruebas de dominio (política mínima)

- [x] T010 [P] Crear `tests/Pos.Domain.Tests/Users/UserNameRulesTests.cs`: normalización sin distinguir mayúsculas ("Maria" == "MARIA"), acentos (`MARÍA` vs `maría` se normalizan igual), y caso límite de longitud 3/40 y caracteres inválidos/espacios.
- [x] T011 [P] Crear `tests/Pos.Domain.Tests/Users/UserLockoutTests.cs`: 4 fallos no bloquean; el 5.º bloquea 5 minutos (`RegisterFailedLogin` devuelve `true`); acceso correcto reinicia el contador; tras vencer el bloqueo el siguiente fallo cuenta desde 1.
- [x] T012 [P] Crear `tests/Pos.Domain.Tests/Users/RolePermissionsTests.cs`: el Cajero tiene exactamente `Sell`, `ViewOwnSales`, `ViewProducts`, `ViewInventory`; el Administrador tiene todos los valores de `Permission`; `IsAuthorizable` es verdadero solo para `CancelSales` y `OpenDrawerWithoutSale`.

### Application: abstracciones y puertos

- [x] T013 Modificar `src/Pos.Application/Abstractions/Error.cs`: agregar `Forbidden(Permission Permission, bool CanBeAuthorized)`, `InvalidCredentials`, `LockedOut(DateTime UntilUtc)` y `LastAdministrator`, siguiendo el estilo de los errores existentes; reutilizar `ValidationFailed`, `Duplicate`, `NotFound`, `Conflict`, `InvalidState`.
- [x] T014 [P] Modificar `src/Pos.Application/Abstractions/IAuditLog.cs`: `void Add(string action, string entityType, Guid entityId, string? details, Guid? authorizedBy = null)`; actualizar `src/Pos.Infrastructure/Audit/AuditLog.cs` para pasar `authorizedBy` a `AuditEntry.Create`.
- [x] T015 [P] Crear `src/Pos.Application/Audit/AuditActions.cs`: constantes de texto `LOGIN_SUCCEEDED`, `LOGIN_FAILED`, `USER_LOCKED_OUT`, `LOGOUT`, `USER_CREATED`, `USER_UPDATED`, `USER_DEACTIVATED`, `USER_ACTIVATED`, `PASSWORD_RESET`, `PASSWORD_CHANGED`, `ADMIN_AUTHORIZATION_GRANTED`, `ADMIN_AUTHORIZATION_DENIED`, `HELD_SALE_DISCARDED`, más las existentes `SALE_CANCELLED` y `DRAWER_OPENED` (buscar los literales en `src/Pos.Application/Sales/CancelSale/CancelSaleHandler.cs` y `src/Pos.Application/Printing/OpenCashDrawer/OpenCashDrawerHandler.cs`, y cualquier otro literal de acción en Application) y un mapa `AuditActions.All` con el texto en español de cada una para la UI. Hacer que los handlers existentes usen estas constantes.
- [x] T016 [P] Crear `src/Pos.Application/Users/SessionUser.cs` y `src/Pos.Application/Users/UserDtos.cs`: `SessionUser(Id, FullName, UserName, Role, Initials)`, `UserDto` (sin hash; incluye `Version`), `UserListItem`, `UserSearch(Text, IncludeInactive, Page)`, `UserPage` (100 por página), `UserOption`, `SignInOutcome(SessionUser, MustChangePassword)`, `SetupState(NeedsFirstAdmin)`, `AccessDecision(Allowed, AuthorizedBy, Error)`.
- [x] T017 [P] Crear `src/Pos.Application/Users/IUserRepository.cs` con la interfaz exacta de contracts/application-ports.md (`GetAsync`, `FindByUserNameAsync`, `AnyRealUserAsync`, `CountActiveAdminsAsync`, `SearchAsync`, `ListCashiersAsync`, `Add`, `SaveChangesAsync`, `RecordLoginAttemptAsync`).
- [x] T018 [P] Crear `src/Pos.Application/Users/IPasswordHasher.cs` (`string Hash(string)`, `PasswordCheck Verify(string, string)` con `PasswordCheck { Failed, Succeeded, SucceededRehashNeeded }`).
- [x] T019 [P] Crear `src/Pos.Application/Users/UserFields.cs` y `src/Pos.Application/Users/UserMessages.cs` (nombres de campos y textos en español de validación, p. ej. "Usuario o contraseña incorrectos", mensaje de bloqueo, mínimo 8 caracteres, confirmación distinta, usuario duplicado), siguiendo `src/Pos.Application/Products/ProductFields.cs` y `ProductMessages.cs`.
- [x] T020 Crear `src/Pos.Application/Users/Session/UserSession.cs` (depende de T016): singleton que implementa `IUserSession : ICurrentUser` (`User`, `event Changed`). `UserId` devuelve `SystemUser.Id` cuando no hay sesión. Métodos internos de Application para fijar y limpiar la sesión. Modificar `src/Pos.Application/Abstractions/SystemUser.cs`: conservar `Id` y `DisplayName`; quitar `NameOf` **solo cuando ya no tenga usos** (T082).
- [x] T021 [P] Crear `src/Pos.Application/Users/Access/AuthorizationGrants.cs` (`IAuthorizationGrants`: `Issue`, `TryConsume`): diccionario en memoria, concesión de un solo uso, vence en 2 minutos, ligada a permiso y solicitante; usa `IClock`.
- [x] T022 [P] Crear `src/Pos.Application/Users/Access/LoginThrottle.cs`: singleton con contador en memoria por nombre normalizado inexistente (5 fallos → bloqueo de 5 minutos, mismas constantes de `User`), para no revelar que el usuario no existe (research §9).
- [x] T023 Crear `src/Pos.Application/Users/Access/IAccessControl.cs` y `AccessControl.cs` (depende de T006, T013, T017, T020, T021): `CheckAsync(permission, ct)` y `CheckAsync(permission, grantId, ct)`. Relee al usuario por llave primaria en cada verificación; rechaza si no existe o está inactivo; permite si `RolePermissions.Has`; si trae `grantId` válido para ese permiso y solicitante, lo consume y devuelve el `AuthorizedBy`. Devuelve `Forbidden(permission, RolePermissions.IsAuthorizable(permission))`. Registra en Serilog el rechazo con usuario y operación, nunca contraseñas (Principio VIII).
- [x] T024 [P] Crear `src/Pos.Application/Audit/IAuditLogReader.cs` con `SearchAsync(AuditSearch, ct)`, y `AuditSearch`/`AuditPage`/`AuditRow` (fecha, acción, usuario, autorizador, entidad, detalles; 100 por página) en `src/Pos.Application/Audit/AuditDtos.cs`.
- [x] T025 Modificar `src/Pos.Application/Sales/ISaleDraftStore.cs` y `src/Pos.Application/Sales/SaveSaleDraft/`, `GetSaleDraft/`, `DiscardSaleDraft/` si hace falta (depende de T020): el almacén opera sobre `ICurrentUser.UserId`; agregar `void RemoveFor(Guid userId)` y `Task ReassignAsync(Guid fromUserId, Guid toUserId, CancellationToken ct)`.
- [x] T026 Modificar `src/Pos.Application/DependencyInjection.cs` (depende de T020 a T023): registrar `UserSession` como singleton de `IUserSession` y `ICurrentUser`, `AccessControl`, `AuthorizationGrants`, `LoginThrottle`, y cada caso de uso nuevo a medida que se cree. `SavePrintingSettings` pasa de singleton a scoped (consulta la base para el permiso). `AccessControl` se registra *scoped* (depende de `IUserRepository`). Revisar el ciclo de vida de **todos** los handlers que reciben `IAccessControl` (p. ej. `ListPrinters`, `PrintTicket`, `OpenCashDrawer`, `ExportDiagnostics`, `SaveBusinessProfile`) y pasar a scoped los que estén como singleton; `dotnet build` y el arranque con validación de ámbitos (`ValidateScopes`) no deben fallar.

### Infrastructure: esquema, migración e implementaciones

- [x] T027 [P] Crear `src/Pos.Infrastructure/Security/Pbkdf2PasswordHasher.cs`: `Rfc2898DeriveBytes.Pbkdf2` SHA-256, sal de 16 bytes, hash de 32 bytes, 600 000 iteraciones, formato `pbkdf2-sha256$<iter>$<sal base64>$<hash base64>`, verificación con `CryptographicOperations.FixedTimeEquals`, y `SucceededRehashNeeded` si las iteraciones guardadas son menores que las vigentes.
- [x] T028 [P] Crear `src/Pos.Infrastructure/Persistence/Configurations/UserConfiguration.cs`: tabla `Users` con las longitudes de data-model.md (`FullName` 100, `UserName` 40, `NormalizedUserName` 40, `Role` 10 como texto, `PasswordHash` 200), **índice único** `IX_Users_NormalizedUserName`, `Version` como token de concurrencia, `HasData` de "Sistema" con id `00000000-0000-7000-8000-000000000001`, `UserName = "Sistema"`, `NormalizedUserName = "SISTEMA"`, `IsSystem = true`, `IsActive = false`, `PasswordHash = null`, `Role = Admin`, `MustChangePassword = false`. Agregar `DbSet<User> Users` en `src/Pos.Infrastructure/Persistence/PosDbContext.cs`.
- [x] T029 [P] Modificar `src/Pos.Infrastructure/Persistence/Configurations/AuditEntryConfiguration.cs`: columna `AuthorizedBy` nula e índices `IX_AuditEntries_CreatedAt` e `IX_AuditEntries_CreatedBy`.
- [x] T030 [P] Modificar `src/Pos.Infrastructure/Persistence/Configurations/SaleDraftConfiguration.cs`: llave primaria `UserId`, `HasDefaultValue(SystemUser.Id)` para que la fila existente reciba el valor en la reconstrucción; eliminar `Slot` y `CK_SaleDrafts_Slot`.
- [x] T031 [P] Modificar `src/Pos.Infrastructure/Persistence/Configurations/SaleConfiguration.cs`: índice `IX_Sales_CreatedBy_CreatedAt` sobre (`CreatedBy`, `CreatedAt`).
- [x] T032 Generar la migración (depende de T028 a T031): `dotnet ef migrations add UsersAndRoles --project src/Pos.Infrastructure` y el script `dotnet ef migrations script BusinessProfile UsersAndRoles --project src/Pos.Infrastructure -o <scratchpad>/migracion.sql`. **Revisar el SQL** según quickstart.md §2: `CREATE TABLE "Users"` con índice único y `INSERT` de "Sistema"; `ALTER TABLE "AuditEntries" ADD "AuthorizedBy"` sin reconstruir; reconstrucción de `SaleDrafts` cuyo `INSERT INTO "ef_temp_SaleDrafts" … SELECT` copie `DraftId`, `LinesJson`, `UpdatedAt` y deje `UserId` con el id de "Sistema"; `CREATE INDEX "IX_Sales_CreatedBy_CreatedAt"`; ninguna otra tabla se reconstruye. Si el SQL no copia la fila de `SaleDrafts`, ajustar T030 y regenerar (las migraciones no se editan a mano).
- [x] T033 [P] Crear `src/Pos.Infrastructure/Users/UserRepository.cs` (depende de T017, T028): implementa `IUserRepository`; `RecordLoginAttemptAsync` con `ExecuteUpdateAsync` sin tocar `Version` ni auditoría; `SaveChangesAsync` traduce la violación del índice único a `Duplicate` y la versión a `Conflict` (seguir `ProductRepository`); `SearchAsync` usa `TextNormalizer` sin acentos ni mayúsculas, nunca incluye "Sistema", 100 por página, filtro de inactivos igual que productos; `ListCashiersAsync` excluye "Sistema".
- [x] T034 [P] Modificar `src/Pos.Infrastructure/Sales/SqliteSaleDraftStore.cs` (depende de T009, T025): una fila por `ICurrentUser.UserId`; implementar `RemoveFor` y `ReassignAsync`.
- [x] T035 [P] Crear `src/Pos.Infrastructure/Audit/AuditLogReader.cs` (depende de T024, T029): consulta con `LEFT JOIN Users` para nombres; filtros de rango (límite superior exclusivo), acción y "usuario involucrado" (`CreatedBy`, `AuthorizedBy` o entrada `EntityType = 'User'` con `EntityId` igual); orden `CreatedAt DESC`; páginas de 100.
- [x] T036 Modificar `src/Pos.Infrastructure/DependencyInjection.cs` (depende de T027, T033 a T035): registrar `IPasswordHasher`, `IUserRepository`, `IAuditLogReader`; **eliminar** el registro de `SystemCurrentUser`; eliminar `src/Pos.Infrastructure/Platform/SystemCurrentUser.cs` y actualizar cualquier referencia (incluidas pruebas) para usar `UserSession` o un `FixedCurrentUser` de pruebas. Verificar con `dotnet build -v q` que `AuditingInterceptor` sigue recibiendo `ICurrentUser`.

### Migración: prueba obligatoria

- [x] T037 Actualizar `tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests.cs` (depende de T032): incluir `v0.5.0.db` en la migración de todas las bases de ejemplo y verificar: existe "Sistema" con `IsSystem = 1`, `IsActive = 0` y sin hash; ventas, movimientos y productos intactos (totales sin cambio) y con `CreatedBy` = "Sistema"; el borrador de `v0.5.0.db` queda bajo `UserId = Sistema` con su `LinesJson`; `PRAGMA foreign_key_check` limpio; ausencia de `CK_SaleDrafts_Slot`.

### Desktop: estructura de la ventana principal

- [x] T038 Extraer el contenido de `src/Pos.Desktop/Shell/MainWindow.axaml(.cs)` a `src/Pos.Desktop/Shell/MainView.axaml(.cs)` y crear `src/Pos.Desktop/Shell/RootViewModel.cs` con la propiedad `Content` que alterna entre asistente, inicio de sesión, cambio de contraseña obligatorio y shell de sesión (contracts/ui.md §Flujo). `MainWindow.DataContext` pasa a ser `RootViewModel`. En esta fase `Content` usa contenedores vacíos (marcadores) para asistente, inicio de sesión y cambio de contraseña: las vistas reales se crean en T045, T052 y T053 y se conectan en T046 y T054. Sin sesión no hay menú ni atajo F9 (SC-001). Mantener el comportamiento actual al cerrar la ventana (confirmar cambios sin guardar, después respaldo).
- [x] T039 Crear `src/Pos.Desktop/Shell/SessionScope.cs` y ajustar `src/Pos.Desktop/Composition/HostBuilder.cs`, `src/Pos.Desktop/Navigation/NavigationServiceCollectionExtensions.cs` y `src/Pos.Desktop/Composition/App.axaml.cs` (research §12): cada inicio de sesión crea un ámbito de DI; `MainViewModel`, `Navigator`, `NavigationRegistry`, `MenuViewModel`, las pantallas (`AddPage`), las tarjetas de Inicio y `SalesDashboardSource` se registran *scoped*; `UseCases`, `OperationRunner`, `DialogService`, `TicketPrintingService` y la cola de impresión siguen singleton. Al desechar el ámbito la siguiente sesión empieza limpia. Ajustar las pruebas de `tests/Pos.Desktop.Tests/` que resolvían pantallas como singleton al registro scoped (sin agregar pruebas de ViewModels).
- [x] T040 Agregar las cadenas en español de toda la funcionalidad a `src/Pos.Desktop/Resources/Strings.resx` a medida que se creen las vistas (este es el punto de partida: títulos y botones de asistente, inicio de sesión, cambio de contraseña y menú de usuario, según contracts/ui.md).
- [x] T041 Dejar la base compilando y en verde. `SystemUser.NameOf` se conserva hasta T082, donde se crea el `JOIN` con `Users` y se reemplazan todos sus usos; confirmar con `dotnet build -v q` y `dotnet test --verbosity quiet` (proyectos Domain, Application, Infrastructure y Architecture) que todo compila y pasa. Las pruebas de Application existentes que construyen handlers deben seguir compilando: agregar en `tests/Pos.Application.Tests/TestSupport/` un `AllowAllAccessControl` (implementa `IAccessControl` devolviendo `Allowed`) y un `FixedCurrentUser` si no existe, para las pruebas que no verifican permisos.

**Punto de control**: fundamentos listos; las historias pueden empezar.

---

## Fase 3: Historia 1 - Primer arranque: crear el administrador (Prioridad: P1) 🎯 MVP

**Objetivo**: en una instalación sin usuarios solo se ofrece el asistente; al terminarlo se puede entrar.

**Prueba independiente**: iniciar con una base vacía: solo aparece "Crear administrador"; con contraseña de 7 caracteres o confirmación distinta no se crea nada; con datos válidos se pasa al inicio de sesión con el usuario escrito.

### Pruebas de la historia 1

- [x] T042 [P] [US1] Crear `tests/Pos.Infrastructure.Tests/Users/UserPersistenceTests.cs` (SQLite real) con: (a) asistente ejecutado dos veces en paralelo: el segundo recibe `InvalidState` y solo queda un usuario real. Las pruebas de índice único y bloqueo persistente se agregan en US5 y US2.

### Implementación de la historia 1

- [x] T043 [P] [US1] Crear `src/Pos.Application/Users/GetSetupState/GetSetupStateHandler.cs`: devuelve `SetupState { NeedsFirstAdmin }` = no existe ningún usuario con `IsSystem = false` (`IUserRepository.AnyRealUserAsync`).
- [x] T044 [US1] Crear `src/Pos.Application/Users/CreateFirstAdmin/CreateFirstAdminCommand.cs`, `CreateFirstAdminValidator.cs` (FluentValidation: nombre completo 1 a 100, usuario según `UserNameRules`, contraseña "mínimo 8 caracteres", confirmación igual) y `CreateFirstAdminHandler.cs`: dentro de una única transacción de escritura (`IWriteTransactions`, `BEGIN IMMEDIATE`) comprueba que no hay usuarios reales (si hay: `InvalidState`), hashea, crea con `User.CreateFirstAdmin`, reasigna a este administrador el borrador que tenga "Sistema" (`ISaleDraftStore.ReassignAsync(SystemUser.Id, admin.Id)`) y audita `USER_CREATED`. Registrarlo en `DependencyInjection.cs`.
- [x] T045 [P] [US1] Crear `src/Pos.Desktop/Auth/FirstAdminView.axaml(.cs)` y `FirstAdminViewModel.cs`: título "Crear administrador", texto "Es la primera vez que se usa el sistema. Crea la cuenta del administrador.", campos Nombre completo, Usuario, Contraseña y Confirmar contraseña con errores por campo (patrón de `ProductEditorViewModel` / `FormViewModel`), botón "Crear y continuar", sin forma de omitir. Al terminar pasa a `LoginView` con el usuario escrito.
- [x] T046 [US1] Integrar en `RootViewModel` y en `src/Pos.Desktop/Startup/StartupPresenter.cs`: tras la pantalla de carga llamar a `GetSetupState`; si `NeedsFirstAdmin` mostrar `FirstAdminView`, si no `LoginView`.

**Punto de control**: US1 funcional. (El inicio de sesión llega en US2; hasta entonces se valida hasta la creación del usuario.)

---

## Fase 4: Historia 2 - Iniciar sesión (Prioridad: P1)

**Objetivo**: usuario y contraseña, sin distinguir mayúsculas en el usuario, mensaje genérico, bloqueo de 5 intentos / 5 minutos persistente, cambio obligatorio de contraseña.

**Prueba independiente**: acceso correcto, incorrecto, bloqueo (también tras reiniciar) e inactivo.

### Pruebas de la historia 2

- [x] T047 [P] [US2] Crear `tests/Pos.Application.Tests/Users/SignInHandlerTests.cs` con un `IPasswordHasher` rápido de pruebas: usuario inexistente, contraseña errónea, usuario inactivo y "Sistema" devuelven el mismo `InvalidCredentials`; 5 fallos bloquean y el 6.º con contraseña correcta devuelve `LockedOut`; un acceso correcto reinicia el contador; el nombre inexistente repetido 5 veces devuelve `LockedOut` (sin revelar inexistencia); ninguna entrada de bitácora contiene la contraseña.
- [x] T048 [P] [US2] Agregar a `tests/Pos.Infrastructure.Tests/Users/UserPersistenceTests.cs` la prueba de bloqueo persistente entre contextos (un segundo `PosDbContext` ve `LockoutEndsAt`/`FailedLoginCount` escritos con `RecordLoginAttemptAsync`), y crear `tests/Pos.Infrastructure.Tests/Users/Pbkdf2PasswordHasherTests.cs`: verifica la contraseña correcta, rechaza la errónea, el hash no contiene la contraseña en claro (SC-008) y reporta `SucceededRehashNeeded` con menos iteraciones.

### Implementación de la historia 2

- [x] T049 [US2] Crear `src/Pos.Application/Users/SignIn/SignInCommand.cs`, `SignInValidator.cs` y `SignInHandler.cs` con el orden de research §9: buscar por `NormalizedUserName`; inexistente o "Sistema": verificar contra un hash ficticio, sumar a `LoginThrottle`, `InvalidCredentials` (o `LockedOut` si el contador lo bloquea); bloqueado: `LockedOut` sin verificar; verificar contraseña (fallo: `RegisterFailedLogin`, `RecordLoginAttemptAsync`, auditar `LOGIN_FAILED` y, si queda bloqueado, `USER_LOCKED_OUT`); inactivo: mensaje genérico; éxito: reiniciar contador, regenerar el hash si `SucceededRehashNeeded`, auditar `LOGIN_SUCCEEDED`, devolver `SignInOutcome` y **sellar en memoria** al usuario autenticado para `StartSession`. Registra en Serilog sin contraseñas. El contador de fallos y la entrada de bitácora (`LOGIN_FAILED` y, si aplica, `USER_LOCKED_OUT`) se escriben en **una sola transacción** (Principio I); `RecordLoginAttemptAsync` debe poder participar en ella.
- [x] T050 [P] [US2] Crear `src/Pos.Application/Users/StartSession/StartSessionHandler.cs` (solo acepta el usuario sellado por `SignIn`; fija `UserSession`) y `src/Pos.Application/Users/EndSession/EndSessionHandler.cs` (audita `LOGOUT`, limpia `UserSession`).
- [x] T051 [P] [US2] Crear `src/Pos.Application/Users/ChangeOwnPassword/ChangeOwnPasswordCommand.cs`, `ChangeOwnPasswordValidator.cs` y `ChangeOwnPasswordHandler.cs`: contraseña actual (se omite en el cambio obligatorio, donde se acaba de verificar), "mínimo 8 caracteres", confirmación y distinta de la actual; `SetPassword(hash, false)`; audita `PASSWORD_CHANGED`. Sirve a FR-025 y al cambio obligatorio.
- [x] T052 [P] [US2] Crear `src/Pos.Desktop/Auth/LoginView.axaml(.cs)` y `LoginViewModel.cs`: campos Usuario y Contraseña, Enter en Contraseña ejecuta "Entrar", foco en Usuario (o Contraseña si Usuario viene lleno); mensajes "Usuario o contraseña incorrectos" y "Usuario bloqueado temporalmente por intentos fallidos. Intenta de nuevo en N minutos."; tras un error se limpia la contraseña y el foco vuelve a ella.
- [x] T053 [P] [US2] Crear `src/Pos.Desktop/Auth/ChangePasswordView.axaml(.cs)` y `ChangePasswordViewModel.cs` compartida: modo obligatorio (Nueva y Confirmar, "Guardar y continuar", "Salir" regresa a `LoginView` sin abrir la sesión) y modo voluntario como diálogo (Contraseña actual, Nueva, Confirmar; "Guardar" y "Cancelar").
- [x] T054 [US2] Integrar el flujo en `RootViewModel`: `SignIn` → si `MustChangePassword` mostrar cambio obligatorio **sin crear el ámbito de sesión**; si no (o tras completarlo) `StartSession` + crear `SessionScope` + shell en Inicio. Al cerrar sesión: `EndSession`, desechar el ámbito, volver a `LoginView`.

**Punto de control**: US1 + US2 funcionan: se puede crear el administrador, entrar y salir.

---

## Fase 5: Historia 4 - Permisos por rol (Prioridad: P1)

**Objetivo**: toda operación restringida se rechaza en el caso de uso para el Cajero, y las ventas del Cajero se limitan a las propias. (Se hace antes que US3 porque el menú usa los mismos permisos.)

**Prueba independiente**: invocar cada operación restringida con un cajero y verificar `Forbidden` sin efectos, sin pasar por la interfaz.

### Pruebas de la historia 4

- [x] T055 [P] [US4] Crear `tests/Pos.Application.Tests/Security/RestrictedOperationsTests.cs`: prueba parametrizada que invoca con un Cajero cada handler restringido de contracts/application-ports.md (§Casos de uso existentes que cambian, y los de usuarios/bitácora/seguridad cuando existan) y verifica `Forbidden` y que no hubo escrituras (SC-002). Usar SQLite real o dobles con contadores de escritura, según el estilo de las pruebas existentes en `tests/Pos.Application.Tests/`. Incluir un caso positivo: el mismo handler con un Administrador se ejecuta. Los handlers de usuarios, bitácora y seguridad se agregan a esta prueba en T104, cuando existan.
- [x] T056 [P] [US4] Crear `tests/Pos.Application.Tests/Sales/SalesOwnershipTests.cs`: el Cajero consulta su propia venta y recibe `Forbidden` al consultar/reimprimir la de otro; `SearchSales` fuerza `CashierId` al usuario actual y devuelve `Forbidden` si pide otro; el Administrador ve todas.

### Implementación de la historia 4 (verificación `IAccessControl` antes de validar o abrir transacción)

- [x] T057 [P] [US4] Agregar la verificación `ManageProducts` a `src/Pos.Application/Products/CreateProduct/CreateProductHandler.cs`, `UpdateProduct/UpdateProductHandler.cs`, `DeleteProduct/DeleteProductHandler.cs` y `PrepareProductImage/PrepareProductImageHandler.cs`; `ViewProducts` a `SearchProducts/SearchProductsHandler.cs`, `GetProduct/GetProductHandler.cs` y `CountActiveProducts/CountActiveProductsHandler.cs`. Devolver `Forbidden` sin efectos.
- [x] T058 [P] [US4] Agregar `ViewInventory` a `src/Pos.Application/Inventory/SearchStock/SearchStockHandler.cs`, `SearchMovements/SearchMovementsHandler.cs` y `GetStockAlerts/GetStockAlertsHandler.cs`; `RegisterMovements` a `RegisterMovement/RegisterMovementHandler.cs`.
- [x] T059 [P] [US4] Agregar `Sell` a `src/Pos.Application/Sales/FindProductsForSale/`, `ReviewSale/`, `ConfirmSale/`, `SaveSaleDraft/`, `GetSaleDraft/` y `DiscardSaleDraft/` (los handlers); `ViewAllSales` a `GetSalesDashboard/GetSalesDashboardHandler.cs`.
- [x] T060 [US4] Modificar `src/Pos.Application/Sales/SearchSales/SearchSalesHandler.cs`/`SearchSalesQuery.cs` y `src/Pos.Application/Sales/GetSale/GetSaleHandler.cs` (y `SaleDtos.cs`, `ISaleRepository`): permiso `ViewOwnSales`; `SaleSearch.CashierId Guid?`; sin `ViewAllSales` el filtro se fuerza al usuario actual y pedir otro devuelve `Forbidden`; `GetSale` de una venta ajena sin `ViewAllSales` devuelve `Forbidden` (regla de propiedad por `Sale.CreatedBy`).
- [x] T061 [P] [US4] Agregar `ManageSettings` a `src/Pos.Application/Business/SaveBusinessProfile/SaveBusinessProfileHandler.cs`, `Printing/SavePrintingSettings/SavePrintingSettingsHandler.cs` y `Printing/ListPrinters/ListPrintersHandler.cs`; `ExportDiagnostics` a `Diagnostics/ExportDiagnostics/ExportDiagnosticsHandler.cs`. `GetBusinessProfile`, `GetPrintingSettings` y `GetAppInfo` no requieren permiso.
- [x] T062 [US4] Modificar `src/Pos.Application/Printing/PrintTicket/PrintTicketHandler.cs`: ticket de una venta requiere `ViewOwnSales` con la regla de propiedad de `GetSale` (la impresión automática tras cobrar es sobre la venta propia); el ticket de prueba requiere `ManageSettings`.
- [x] T063 [US4] Modificar `src/Pos.Application/Sales/CancelSale/CancelSaleCommand.cs`/`CancelSaleHandler.cs` y `src/Pos.Application/Printing/OpenCashDrawer/OpenCashDrawerCommand.cs`/`OpenCashDrawerHandler.cs`: `CancelSales` y `OpenDrawerWithoutSale` (autorizables), con `Guid? AuthorizationGrantId` en `CancelSaleCommand` y `Manual(reason, grantId?)` en la apertura manual; la apertura al cobrar (`ForSale`) requiere `Sell`. `Forbidden` lleva `CanBeAuthorized = true`. Las entradas `SALE_CANCELLED` y `DRAWER_OPENED` llevan `AuthorizedBy` cuando hubo concesión.
- [x] T064 [US4] Actualizar las pruebas existentes de `tests/Pos.Application.Tests/` afectadas por los nuevos parámetros de constructor de los handlers (usar `AllowAllAccessControl` de T041) y ejecutar `dotnet test --verbosity quiet` en `tests/Pos.Application.Tests`.

**Punto de control**: el Cajero es rechazado en el 100 % de operaciones restringidas (SC-002).

---

## Fase 6: Historia 3 - Menú según rol y usuario visible (Prioridad: P1)

**Objetivo**: menú y pantallas filtrados por rol, sección de usuario en el menú, cerrar sesión, cambiar de usuario y cambiar contraseña.

**Prueba independiente**: entrar con un cajero y con un administrador y comparar los menús con contracts/ui.md §Menú según rol.

- [x] T065 [US3] Modificar `src/Pos.Desktop/Navigation/NavigationModels.cs`, `NavigationRegistry.cs`, `NavigationServiceCollectionExtensions.cs` y `Navigator.cs`: `NavigationEntry` y `AddPage(..., permission: …)` reciben `Permission?`; `NavigationRegistry` (scoped) filtra con el rol de la sesión (`RolePermissions.Has`) y omite los grupos sin opciones (FR-008); `FindEntry` devuelve nulo para una opción filtrada. Declarar el permiso de cada opción en `src/Pos.Desktop/Sales/SalesModule.cs` (`Sell`, `ViewOwnSales`), `Products/ProductsModule.cs` (`ViewProducts`), `Inventory/InventoryModule.cs` (`ViewInventory`), `Settings/SettingsModule.cs` (`ManageSettings`) y `About/AboutModule.cs` / `Home/HomeModule.cs` (sin permiso), según la tabla de contracts/ui.md.
- [x] T066 [P] [US3] Agregar `Permission?` a `src/Pos.Desktop/Home/DashboardCard.cs` y filtrar las tarjetas en `HomeViewModel.cs`: ventas de hoy, últimos 7 días y más vendidos requieren `ViewAllSales` (`src/Pos.Desktop/Sales/SalesCards.cs`); sin existencia y existencia baja, `ViewInventory` (`Inventory/InventoryCards.cs`); productos activos, `ViewProducts` (`Home/Cards/ActiveProductsCard.cs`).
- [x] T067 [P] [US3] Agregar propiedades `Can…` calculadas con `RolePermissions.Has(session.Role, …)` y ocultar con ellas: Nuevo/Editar/Eliminar en `src/Pos.Desktop/Products/ProductsView.axaml` + `ProductsViewModel.cs`; "Registrar movimiento" en `Inventory/MovementsView.axaml` + `MovementsViewModel.cs`; "Probar impresora" en `Settings/PrinterSettingsViewModel.cs`; "Exportar diagnóstico" en `About/AboutView.axaml` + `AboutViewModel.cs`. "Cancelar venta" (`Sales/SaleDetailView`) y "Abrir cajón" (`Sales/PointOfSaleView`) **siguen visibles** para el Cajero porque permiten solicitar autorización. Cuidar que un producto en solo lectura no abra el editor con doble clic.
- [x] T068 [US3] Crear la sección de usuario en `src/Pos.Desktop/Navigation/MenuView.axaml` y `MenuViewModel.cs` (pie, sobre el botón de contraer): expandido muestra nombre completo y rol ("Administrador" o "Cajero"); contraído, círculo con iniciales y tooltip "Nombre completo · Rol"; clic abre menú flotante con "Cambiar contraseña" (diálogo de T053), "Cambiar de usuario" y "Cerrar sesión" (ambos vuelven a `LoginView`; "Cambiar de usuario" deja el usuario vacío y enfocado). Las cadenas van en `Strings.resx`.
- [x] T069 [US3] Confirmación al salir con venta en curso (se comparte con US8): en `RootViewModel`, si el Punto de venta tiene líneas, mostrar "Hay una venta en curso. Se guardará y se te ofrecerá al volver a entrar. ¿Continuar?" con "Sí" y "No"; con "No" nada cambia. Antes de desechar el ámbito, esperar el guardado del borrador (`src/Pos.Desktop/Sales/DraftAutosaver.cs`) para no perder la venta (Principio I).

**Punto de control**: US3 funcional; validar con quickstart §5.4–5.5.

---

## Fase 7: Historia 5 - Administración de usuarios (Prioridad: P1)

**Objetivo**: listado, alta, edición, restablecer contraseña y desactivación, con protección del último administrador.

**Prueba independiente**: crear, editar, desactivar y restablecer la contraseña de un usuario; intentar desactivar al último administrador.

### Pruebas de la historia 5

- [x] T070 [P] [US5] Crear `tests/Pos.Application.Tests/Users/UpdateUserHandlerTests.cs`: no se puede desactivar ni quitar el rol al último administrador activo (`LastAdministrator`); un administrador no puede cambiarse su propio rol ni desactivarse; al desactivar a un usuario con borrador se borra su fila de `SaleDrafts` y se audita `HELD_SALE_DISCARDED` en la misma transacción; `Conflict` con versión desactualizada.
- [x] T071 [P] [US5] Agregar a `tests/Pos.Infrastructure.Tests/Users/UserPersistenceTests.cs`: índice único sin distinguir mayúsculas con dos altas simultáneas ("Maria" y "MARIA"): una gana y la otra recibe `Duplicate`.

### Implementación de la historia 5

- [x] T072 [P] [US5] Crear `src/Pos.Application/Users/SearchUsers/SearchUsersHandler.cs` y `SearchUsersQuery.cs` (`ManageUsers`, 100 por página, nunca "Sistema") y `src/Pos.Application/Users/GetUser/GetUserHandler.cs` (`ManageUsers`; `NotFound` para "Sistema").
- [x] T073 [P] [US5] Crear `src/Pos.Application/Users/CreateUser/CreateUserCommand.cs`, `CreateUserValidator.cs` y `CreateUserHandler.cs` (`ManageUsers`): nombre completo (1 a 100), usuario (3 a 40, `UserNameRules`), rol, activo, contraseña inicial "mínimo 8 caracteres" y confirmación; `Duplicate(UserName)` (también por el índice único); `MustChangePassword = true`; audita `USER_CREATED`; devuelve `Result<Guid>`.
- [x] T074 [US5] Crear `src/Pos.Application/Users/UpdateUser/UpdateUserCommand.cs`, `UpdateUserValidator.cs` y `UpdateUserHandler.cs` (`ManageUsers`): id, versión esperada, nombre completo, usuario, rol, activo. Dentro de la transacción (`BEGIN IMMEDIATE`): `CountActiveAdminsAsync` para la protección del último administrador y `LastAdministrator` también cuando un administrador intenta desactivarse o cambiarse el rol a sí mismo; al desactivar, `ISaleDraftStore.RemoveFor(userId)` y `HELD_SALE_DISCARDED`; audita `USER_UPDATED` y `USER_DEACTIVATED` o `USER_ACTIVATED`.
- [x] T075 [P] [US5] Crear `src/Pos.Application/Users/ResetUserPassword/ResetUserPasswordCommand.cs`, `ResetUserPasswordValidator.cs` y `ResetUserPasswordHandler.cs` (`ManageUsers`): `InvalidState` si es uno mismo; `SetPassword(hash, true)`; reinicia el bloqueo (`FailedLoginCount = 0`, `LockoutEndsAt = null`); audita `PASSWORD_RESET`.
- [x] T076 [P] [US5] Crear `src/Pos.Application/Users/ListCashiers/ListCashiersHandler.cs` (`ViewAllSales`): usuarios con ventas o activos, sin "Sistema".
- [x] T077 [US5] Registrar todos los handlers de US5 en `src/Pos.Application/DependencyInjection.cs`.
- [x] T078 [US5] Crear el módulo `src/Pos.Desktop/Administration/AdministrationModule.cs`: grupo "Administración" con orden 70 (antes de Configuración, 80) y las entradas Usuarios (`ManageUsers`) y Bitácora (`ViewAuditLog`, se implementa en US6).
- [x] T079 [P] [US5] Crear `src/Pos.Desktop/Administration/UsersView.axaml(.cs)` y `UsersViewModel.cs` (patrón de `src/Pos.Desktop/Products/ProductsView*`): columnas Nombre completo, Usuario, Rol y Estado; búsqueda; casilla "Mostrar inactivos"; paginación de 100; inactivos atenuados con `InactiveOpacityConverter`; botones "Nuevo usuario", "Editar" (o doble clic) y "Restablecer contraseña" (deshabilitado sobre uno mismo).
- [x] T080 [P] [US5] Crear `src/Pos.Desktop/Administration/UserEditorView.axaml(.cs)` y `UserEditorViewModel.cs` (`FormViewModel`, patrón `ProductEditorViewModel`): Nombre completo, Usuario, Rol (Administrador o Cajero), Activo; en alta, además Contraseña inicial y Confirmar; en edición de uno mismo Rol y Activo están deshabilitados con un texto que explica por qué; al desactivar a un usuario con venta conservada se pide confirmación: "Se descartará la venta en curso que {usuario} dejó guardada".
- [x] T081 [P] [US5] Crear `src/Pos.Desktop/Administration/ResetPasswordView.axaml(.cs)` y `ResetPasswordViewModel.cs`: diálogo con Contraseña temporal y Confirmar; al terminar informa "{usuario} deberá cambiarla al iniciar sesión".

**Punto de control**: US5 funcional; validar con quickstart §5.

---

## Fase 8: Historia 6 - Usuario real en toda la auditoría (Prioridad: P1)

**Objetivo**: cada registro queda con el usuario real; las ventas muestran al cajero (detalle, listado con filtro y ticket); bitácora consultable.

**Prueba independiente**: operar con dos usuarios y verificar que cada registro queda con el suyo; migrar una base con datos previos (cubierto por T037).

- [x] T082 [US6] Modificar `src/Pos.Infrastructure/Sales/SaleRepository.cs` e `src/Pos.Infrastructure/Inventory/InventoryRepository.cs`: resolver nombres con `LEFT JOIN Users` (si no se encuentra, los primeros 8 caracteres del id, como hacía `SystemUser.NameOf`); `SaleDetailDto.CreatedByName` y `CancelledByName` desde `Users.FullName`; `SaleListItemDto.CashierName`; filtro `CashierId` en la búsqueda (aprovecha `IX_Sales_CreatedBy_CreatedAt`); `SearchMovements` y `RegisterMovement` resuelven `CreatedByName` con `Users`. Actualizar `src/Pos.Application/Sales/SaleDtos.cs`. Ahora se puede eliminar `SystemUser.NameOf` (T020) y compilar.
- [x] T083 [P] [US6] Modificar `src/Pos.Application/Printing/Ticket/TicketBuilder.cs` (y `TicketDocument.cs` si hace falta): línea "Cajero: {nombre}" bajo la fecha, recortada al ancho del ticket; actualizar `specs/006-ticket-printing/contracts/ticket-format.md` con esta línea. Ajustar las pruebas del ticket existentes en `tests/Pos.Application.Tests/Printing/`.
- [x] T084 [P] [US6] Modificar `src/Pos.Desktop/Sales/SalesHistoryView.axaml` y `SalesHistoryViewModel.cs`: columna "Cajero"; filtro "Cajero" (`ListCashiers` más "Todos") visible solo con `ViewAllSales`. Modificar `SaleDetailView.axaml` y `SaleDetailViewModel.cs`: "Cajero: {nombre}" y, si está cancelada, "Cancelada por {nombre}".
- [x] T085 [P] [US6] Crear `src/Pos.Application/Audit/SearchAuditLog/SearchAuditLogQuery.cs`, `SearchAuditLogValidator.cs` y `SearchAuditLogHandler.cs` (`ViewAuditLog`): desde/hasta UTC (límite superior exclusivo), `UserId?`, `Action?`, página de 100 del más reciente al más antiguo; `ValidationFailed` si el rango es inválido. Registrar en `DependencyInjection.cs`.
- [x] T086 [US6] Crear `src/Pos.Desktop/Administration/AuditLogView.axaml(.cs)` y `AuditLogViewModel.cs` (solo lectura): filtros Desde/Hasta (fecha local convertida a UTC con el mismo helper de Ventas realizadas), Usuario (todos, incluido "Sistema") y Tipo de evento (`AuditActions` con texto en español); columnas Fecha y hora local, Evento, Usuario, Autorizó y Detalle; 100 por página; sin acciones de edición ni borrado.
- [x] T087 [US6] Verificar con `dotnet build -v q` y las pruebas de Infrastructure/Application que los casos de uso existentes no cambiaron salvo por los permisos (Historia 6, escenario 5) y que `AuditingInterceptor` registra el `UserId` de `UserSession`.

**Punto de control**: US6 funcional; validar con quickstart §6.

---

## Fase 9: Historia 7 - Autorización de administrador (Prioridad: P2)

**Objetivo**: el cajero cancela una venta o abre el cajón con credenciales de un administrador sin cerrar su sesión; ambos usuarios quedan en la bitácora.

**Prueba independiente**: con sesión de cajero, cancelar una venta con credenciales de administrador y revisar la bitácora.

- [x] T088 [P] [US7] Crear `tests/Pos.Application.Tests/Users/AuthorizeAdminHandlerTests.cs`: concesión de un solo uso (la segunda `TryConsume` falla); ligada al permiso y al solicitante (otro permiso u otro solicitante la rechazan); vence a los 2 minutos; credenciales de un no administrador → `InvalidCredentials`; permiso no autorizable → `InvalidState`; 5 fallos de un administrador lo bloquean también para iniciar sesión (contador compartido, FR-005); `CancelSaleHandler` con la concesión deja `AuthorizedBy` en `SALE_CANCELLED` y `CancelledBy` = cajero (SC-006).
- [x] T089 [US7] Crear `src/Pos.Application/Users/AuthorizeAdmin/AuthorizeAdminCommand.cs`, `AuthorizeAdminValidator.cs` y `AuthorizeAdminHandler.cs`: `InvalidState` si `!RolePermissions.IsAuthorizable(permission)`; mismas reglas de acceso que `SignIn` (bloqueo, contador compartido, inactivo, "Sistema"); verifica que sea Administrador (`InvalidCredentials` si no); audita `ADMIN_AUTHORIZATION_GRANTED` (con `AuthorizedBy` = administrador, `CreatedBy` = cajero) o `ADMIN_AUTHORIZATION_DENIED`; emite la concesión con `IAuthorizationGrants.Issue` y devuelve `Result<Guid>`. El contador y la bitácora de cada intento se escriben en una sola transacción, igual que en T049. Extraer la lógica común de contador/bloqueo de `SignInHandler` a un servicio interno de Application para no duplicarla. Registrar en `DependencyInjection.cs`.
- [x] T090 [P] [US7] Crear `src/Pos.Desktop/Auth/AdminAuthorizationView.axaml(.cs)` y `AdminAuthorizationViewModel.cs` (diálogo): "Esta operación requiere autorización de un administrador: {Cancelar venta | Abrir cajón sin venta}"; campos Usuario y Contraseña; "Autorizar" y "Cancelar"; errores "Usuario o contraseña incorrectos, o el usuario no es administrador" o el mensaje de bloqueo.
- [x] T091 [US7] Integrar en `src/Pos.Desktop/Sales/CancelSaleViewModel.cs` / `SaleDetailViewModel.cs` y `PointOfSaleViewModel.cs` (apertura de cajón): ante `Forbidden { CanBeAuthorized: true }` ofrecer "Solicitar autorización"; con éxito cerrar el diálogo y repetir el comando con el `grantId`; la sesión del cajero no cambia. Si el usuario ya tiene el permiso (administrador) no se pide nada.

**Punto de control**: US7 funcional; validar con quickstart §7.

---

## Fase 10: Historia 8 - Cambio de usuario y venta en curso (Prioridad: P2)

**Objetivo**: la venta en curso se conserva por usuario, sobrevive al reinicio y se ofrece al volver a entrar; se descarta al desactivar al usuario.

**Prueba independiente**: armar una venta, cambiar de usuario y volver; cerrar la aplicación y volver a entrar.

- [x] T092 [US8] Verificar/ajustar `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs` y `DraftAutosaver.cs`: el guardado usa el `DraftId` y el usuario conectado; al iniciar sesión el Punto de venta ofrece recuperar **solo su** borrador con el mecanismo actual de recuperación; al recuperarla vuelve a ser la venta en curso (misma fila). Una falla al guardar sigue sin interrumpir la venta (Principio I).
- [x] T093 [US8] Completar el flujo de salida de T069 en `RootViewModel`: "Cerrar sesión", "Cambiar de usuario" y cierre de la ventana auditan `LOGOUT` si hay sesión; la venta ya está en el borrador del usuario. Validar con quickstart §8 (pasos 1 a 4): otro usuario no ve la venta, sobrevive al reinicio y se descarta al desactivar al usuario (cubierto por T070).

**Punto de control**: US8 funcional.

---

## Fase 11: Historia 9 - Bloqueo por inactividad (Prioridad: P3)

**Objetivo**: la sesión se bloquea tras un tiempo sin actividad y se desbloquea con la contraseña del mismo usuario, sin perder pantalla ni venta.

**Prueba independiente**: configurar 1 minuto, esperar y desbloquear.

- [x] T094 [P] [US9] Crear `src/Pos.Application/Security/SecuritySettings.cs` (`IdleLockMinutes`: 0 = desactivado, por defecto 15, máximo 240), `ISecuritySettingsStore.cs`, `GetSecuritySettings/GetSecuritySettingsHandler.cs` y `SaveSecuritySettings/SaveSecuritySettingsHandler.cs` (`ManageSettings`; `ValidationFailed` fuera de 0 a 240). Crear `src/Pos.Infrastructure/Security/PreferencesSecuritySettingsStore.cs` sobre `IPreferencesStore` (`preferences/security.json`, seguir `src/Pos.Infrastructure/Printing/PreferencesPrintingSettingsStore.cs`). Registrar en ambos `DependencyInjection.cs`.
- [x] T095 [P] [US9] Crear `src/Pos.Application/Users/VerifySessionPassword/VerifySessionPasswordHandler.cs`: verifica la contraseña del usuario de la sesión; `InvalidCredentials` y `LockedOut`; los fallos cuentan para el bloqueo de FR-005 (mismo servicio interno de T089). Registrar en `DependencyInjection.cs`.
- [x] T096 [P] [US9] Crear `src/Pos.Desktop/Settings/SecuritySettingsView.axaml(.cs)` y `SecuritySettingsViewModel.cs`: "Bloquear la sesión tras [ 15 ] minutos sin actividad" con casillero "Activado" (desactivado guarda 0), rango de 1 a 240; registrar la entrada "Seguridad" en `SettingsModule.cs` con `ManageSettings`.
- [x] T097 [US9] Crear `src/Pos.Desktop/Shell/IdleMonitor.cs` (manejador de túnel de teclado y puntero de la ventana principal + `DispatcherTimer`; lee `IdleLockMinutes`; 0 = nunca) y `LockOverlay.axaml(.cs)` con su ViewModel: cubre todo el contenido del shell, incluido el menú, con fondo opaco; "Sesión bloqueada", nombre del usuario, campo Contraseña, botones "Desbloquear" y "Cambiar de usuario" (este sigue el flujo de US8). Mostrarla desde `RootViewModel` **sin desechar** el ámbito de sesión.
- [x] T098 [US9] Modificar `src/Pos.Desktop/Common/DialogService.cs` (y `DialogWindow.cs`): registrar cada diálogo en el monitor (su actividad cuenta); al bloquear, ocultar el diálogo abierto sin cerrarlo y restaurarlo al desbloquear, sin perder lo capturado.

**Punto de control**: US9 funcional; validar con quickstart §9.

---

## Fase 12: Pulido y transversales

- [x] T099 [P] Crear `docs/usuarios-y-permisos.md`: guía de soporte con roles y permisos, primer arranque, desbloqueo de un usuario (esperar 5 minutos o restablecer contraseña), restablecer contraseña, bitácora y eventos, venta conservada y configuración de inactividad.
- [x] T100 [P] Actualizar `docs/migraciones.md` con la nota de la reconstrucción de `SaleDrafts` (`UserId` con valor por defecto, patrón de `Products.UnitCode`) y la base de ejemplo `v0.5.0.db`; actualizar `docs/ventas.md` y `docs/impresion.md` si describen al usuario de sistema o el ticket.
- [x] T101 Ejecutar `dotnet build -v q` en la raíz (0 errores y 0 advertencias) y `dotnet test --verbosity quiet` en cada proyecto modificado: `Pos.Domain.Tests`, `Pos.Application.Tests`, `Pos.Infrastructure.Tests`, `Pos.Desktop.Tests` y `Pos.ArchitectureTests` (las reglas existentes deben cubrir las carpetas nuevas `Users`, `Security` y `Audit`).
- [x] T102 Revisión de seguridad: buscar con `grep` que ninguna contraseña (ni la errónea) llega a `Details` de la bitácora, a Serilog ni a excepciones (SC-008, Principio VIII); confirmar que no hay usuario ni contraseña por defecto en el repositorio (Principio IX).
- [x] T104 Ampliar `tests/Pos.Application.Tests/Security/RestrictedOperationsTests.cs` (T055) con los handlers creados después: `SearchUsers`, `GetUser`, `CreateUser`, `UpdateUser`, `ResetUserPassword`, `ListCashiers`, `SearchAuditLog`, `GetSecuritySettings`/`SaveSecuritySettings` (según su permiso) y `AuthorizeAdmin`; cada uno devuelve `Forbidden` para el Cajero sin escrituras (SC-002). Ejecutar antes de T101.
- [ ] T103 Validar a mano todo quickstart.md (§3 a §10) en Linux, con especial atención a SC-001, SC-007 (login → Punto de venta en menos de 15 s) y al bloqueo persistente tras reiniciar.

---

## Dependencias y orden de ejecución

### Dependencias entre fases

- **Preparación (Fase 1)**: sin dependencias. **T002 debe hacerse antes de T032** (la base de ejemplo se genera con el esquema previo).
- **Fundamentos (Fase 2)**: depende de la Fase 1; **bloquea todas las historias**.
- **Historias (Fases 3 a 11)**: todas dependen de la Fase 2.
- **Pulido (Fase 12)**: depende de las historias deseadas.

### Dependencias entre historias

- **US1 (P1)**: tras la Fase 2; sin dependencias de otras historias (MVP de entrada).
- **US2 (P1)**: tras la Fase 2; se integra con US1 en `RootViewModel`.
- **US4 (P1)**: tras la Fase 2; independiente de la interfaz.
- **US3 (P1)**: tras US2 y US4 (usa sesión y permisos).
- **US5 (P1)**: tras US4 (usa `ManageUsers`) y US2; necesita el módulo de navegación de US3 (T065).
- **US6 (P1)**: tras US4; T086 comparte `AdministrationModule` con US5 (T078).
- **US7 (P2)**: tras US2 y US4.
- **US8 (P2)**: tras US2 y US5 (desactivar descarta el borrador).
- **US9 (P3)**: tras US2; T095 reutiliza el servicio de T089 (si US7 aún no está hecho, extraer el servicio en T095 y reutilizarlo en T089).

### Dentro de cada historia

- Pruebas de la política mínima junto a la implementación (la de SC-002 y las de propiedad fallan hasta completar los handlers).
- Dominio → casos de uso → repositorio → interfaz.
- Completar la historia antes de pasar a la siguiente prioridad.

### Oportunidades de paralelismo

- Fase 2: T003, T004, T005, T008, T009 en paralelo; luego T010 a T012 en paralelo; T014 a T019, T021, T022, T024 en paralelo; T027 a T031, T033 a T035 en paralelo.
- US4: T057, T058, T059, T061 en paralelo (archivos distintos).
- US5: T072, T073, T075, T076, T079, T080, T081 en paralelo.
- US2: T052 y T053 (vistas) en paralelo con T049 a T051 (casos de uso).

## Ejemplo de paralelismo: Fase 2 (dominio)

```text
Tarea: "Crear UserRole en src/Pos.Domain/Users/UserRole.cs"
Tarea: "Crear Permission en src/Pos.Domain/Users/Permission.cs"
Tarea: "Crear UserNameRules en src/Pos.Domain/Users/UserNameRules.cs"
Tarea: "Modificar AuditEntry en src/Pos.Domain/Audit/AuditEntry.cs"
Tarea: "Modificar SaleDraft en src/Pos.Domain/Sales/SaleDraft.cs"
```

## Estrategia de implementación

### MVP primero (US1 + US2 + US4)

1. Fase 1 y Fase 2 (incluida la migración y `v0.5.0.db`).
2. US1 (asistente) y US2 (inicio de sesión).
3. US4 (permisos en los casos de uso): ya hay seguridad real aunque el menú aún no filtre.
4. **Detenerse y validar**: quickstart §3, §4 y la prueba de SC-002.

### Entrega incremental

1. Fundamentos → US1 → US2 → US4 → US3 (menú) → US5 (usuarios) → US6 (auditoría y bitácora).
2. Después US7 (autorización), US8 (venta conservada) y US9 (bloqueo por inactividad).
3. Cada historia agrega valor sin romper las anteriores.

## Notas

- **[P]** = archivos distintos y sin dependencias pendientes.
- Las migraciones no se editan a mano: si el SQL de `SaleDrafts` no copia la fila, se ajusta la configuración y se regenera antes de integrar.
- Los campos de auditoría quedan sin llave foránea hacia `Users` (research §3); no agregar FKs.
- No agregar dependencias externas (Principio VII); PBKDF2 viene en el runtime.
- Confirmar con commit después de cada tarea o grupo lógico, solo cuando el usuario lo pida.
