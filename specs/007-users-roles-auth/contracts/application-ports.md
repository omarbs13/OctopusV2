# Contrato: casos de uso y puertos de Application

Interfaz que Desktop consume e Infrastructure implementa. Las firmas son orientativas; los nombres
exactos se fijan al implementar. Todos los casos de uso devuelven `Result` o `Result<T>` y
reservan las excepciones para fallas inesperadas (Principio III).

## Errores nuevos (`Abstractions/Error.cs`)

| Error | Significado |
|---|---|
| `Forbidden(Permission Permission, bool CanBeAuthorized)` | El usuario conectado no tiene el permiso. `CanBeAuthorized` = `RolePermissions.IsAuthorizable(permission)` |
| `InvalidCredentials` | Mensaje genérico "Usuario o contraseña incorrectos" (FR-004). Cubre inexistente, contraseña errónea, inactivo y "Sistema" |
| `LockedOut(DateTime UntilUtc)` | Bloqueo temporal por intentos fallidos (FR-005) |
| `LastAdministrator` | La operación dejaría el sistema sin administrador activo, o un administrador intenta desactivarse o quitarse el rol (FR-018) |

Se reutilizan `ValidationFailed`, `Duplicate(UserName)`, `NotFound`, `Conflict` e `InvalidState`.

## Casos de uso nuevos (`Pos.Application/Users/…`)

| Caso de uso | Permiso | Entrada | Salida | Errores y reglas |
|---|---|---|---|---|
| `GetSetupState` | — | — | `SetupState { NeedsFirstAdmin }` | Verdadero si no existe ningún usuario con `IsSystem = false` |
| `CreateFirstAdmin` | — (solo sin usuarios) | nombre completo, usuario, contraseña y confirmación | `Result` | `ValidationFailed`; `InvalidState` si ya existe un usuario (dentro de la transacción). Reasigna a este administrador el borrador que tenga "Sistema". Audita `USER_CREATED` |
| `SignIn` | — | usuario y contraseña | `Result<SignInOutcome { SessionUser, MustChangePassword }>` | `InvalidCredentials`, `LockedOut`. Orden de evaluación en research §9. Si tiene éxito, **no** abre la sesión: la abre `StartSession` tras el cambio obligatorio |
| `StartSession` | — | `SessionUser` devuelto por `SignIn` | `Result` | Fija `UserSession`. Solo acepta el usuario que acaba de autenticarse (lo sella `SignIn` en memoria) |
| `EndSession` | — | — | `Result` | Audita `LOGOUT` y limpia `UserSession`. La venta en curso ya está en el borrador del usuario |
| `VerifySessionPassword` | — | contraseña | `Result` | Desbloqueo por inactividad. `InvalidCredentials` y `LockedOut`; los fallos cuentan para el bloqueo |
| `ChangeOwnPassword` | — (sesión o cambio obligatorio) | contraseña actual (se omite en el cambio obligatorio), nueva y confirmación | `Result` | `ValidationFailed` (mínimo 8, confirmación, distinta de la actual, actual incorrecta). Apaga `MustChangePassword`. Audita `PASSWORD_CHANGED` |
| `SearchUsers` | `ManageUsers` | texto, `IncludeInactive`, página | `UserPage` (100 por página) | Nunca incluye a "Sistema". Busca en nombre completo y usuario sin acentos ni mayúsculas (`TextNormalizer`) |
| `GetUser` | `ManageUsers` | id | `UserDto` (sin hash) | `NotFound` para "Sistema" |
| `CreateUser` | `ManageUsers` | nombre completo, usuario, rol, activo, contraseña inicial y confirmación | `Result<Guid>` | `ValidationFailed`, `Duplicate(UserName)` (también por el índice único). `MustChangePassword = true`. Audita `USER_CREATED` |
| `UpdateUser` | `ManageUsers` | id, versión esperada, nombre completo, usuario, rol, activo | `Result` | `ValidationFailed`, `Duplicate`, `Conflict`, `NotFound`, `LastAdministrator`. Al desactivar, borra la venta conservada y audita `HELD_SALE_DISCARDED`. Audita `USER_UPDATED` y `USER_DEACTIVATED` o `USER_ACTIVATED` |
| `ResetUserPassword` | `ManageUsers` | id, contraseña temporal y confirmación | `Result` | `ValidationFailed`; `InvalidState` si es uno mismo. `MustChangePassword = true` y reinicia el bloqueo. Audita `PASSWORD_RESET` |
| `ListCashiers` | `ViewAllSales` | — | `IReadOnlyList<UserOption>` | Usuarios con ventas o activos, sin "Sistema", para el filtro de ventas |
| `AuthorizeAdmin` | — (lo pide un usuario sin el permiso) | `Permission`, usuario y contraseña | `Result<Guid grantId>` | `InvalidState` si el permiso no es autorizable; `InvalidCredentials` (incluye "no es administrador"); `LockedOut`. Audita `ADMIN_AUTHORIZATION_GRANTED` o `_DENIED`. Research §8 |
| `SearchAuditLog` | `ViewAuditLog` | desde y hasta (UTC, el límite superior es exclusivo), `UserId?`, `Action?`, página | `AuditPage` (100 por página, del más reciente al más antiguo) | `ValidationFailed` si el rango es inválido. Cada fila: fecha, acción (texto en español en la UI), usuario, autorizador, entidad y detalles |
| `GetSecuritySettings` / `SaveSecuritySettings` | — / `ManageSettings` | `SecuritySettings(IdleLockMinutes)` | `SecuritySettings` / `Result` | `ValidationFailed` fuera de 0 a 240 |

## Casos de uso existentes que cambian

Solo se agrega la verificación de permiso al inicio del handler, antes de validar o escribir. La
lógica de negocio no cambia (Historia 6, escenario 5).

| Caso de uso | Permiso | Cambio adicional |
|---|---|---|
| `CreateProduct`, `UpdateProduct`, `DeleteProduct`, `PrepareProductImage` | `ManageProducts` | — |
| `SearchProducts`, `GetProduct`, `CountActiveProducts` | `ViewProducts` | — |
| `SearchStock`, `SearchMovements`, `GetStockAlerts` | `ViewInventory` | `SearchMovements` resuelve `CreatedByName` con `Users` (ya no con `SystemUser.NameOf`) |
| `RegisterMovement` | `RegisterMovements` | Igual que el anterior para el nombre |
| `FindProductsForSale`, `ReviewSale`, `ConfirmSale`, `SaveSaleDraft`, `GetSaleDraft`, `DiscardSaleDraft` | `Sell` | El borrador es el del usuario conectado |
| `SearchSales` | `ViewOwnSales` | `CashierId?` nuevo. Sin `ViewAllSales` se fuerza al usuario actual, y pedir otro devuelve `Forbidden`. Cada fila incluye `CashierName` |
| `GetSale` | `ViewOwnSales` | Sin `ViewAllSales` y con la venta de otro usuario, devuelve `Forbidden`. `CreatedByName` y `CancelledByName` desde `Users` |
| `GetSalesDashboard` | `ViewAllSales` | — |
| `CancelSale` | `CancelSales` (autorizable) | `CancelSaleCommand` + `Guid? AuthorizationGrantId`. `SALE_CANCELLED` lleva `AuthorizedBy` |
| `OpenCashDrawer` manual | `OpenDrawerWithoutSale` (autorizable) | `Manual(reason, grantId?)`. `DRAWER_OPENED` lleva `AuthorizedBy`. La apertura al cobrar (`ForSale`) requiere `Sell` |
| `PrintTicket` de una venta | `ViewOwnSales` | Aplica la regla de propiedad de `GetSale`. El ticket de prueba requiere `ManageSettings` |
| `SaveBusinessProfile`, `SavePrintingSettings`, `ListPrinters` | `ManageSettings` | `SavePrintingSettings` pasa de singleton a scoped porque consulta la base para el permiso |
| `ExportDiagnostics` | `ExportDiagnostics` | — |
| `GetBusinessProfile`, `GetPrintingSettings`, `GetAppInfo` | — | Los usa el ticket y "Acerca de" |

## Puertos nuevos o modificados

```csharp
// Abstractions — sin cambio de firma; la implementación pasa a ser UserSession.
public interface ICurrentUser
{
    Guid UserId { get; }                 // SystemUser.Id si no hay sesión
}

// Users/Session — sesión de la aplicación (singleton, implementa ICurrentUser).
public interface IUserSession : ICurrentUser
{
    SessionUser? User { get; }
    event EventHandler? Changed;
}

// Users — verificación central de permisos (research §6).
public interface IAccessControl
{
    Task<AccessDecision> CheckAsync(Permission permission, CancellationToken ct);
    Task<AccessDecision> CheckAsync(Permission permission, Guid? authorizationGrantId, CancellationToken ct);
}

// Users — repositorio por agregado (Principio VII).
public interface IUserRepository
{
    Task<User?> GetAsync(Guid id, CancellationToken ct);                         // con seguimiento
    Task<User?> FindByUserNameAsync(string normalizedUserName, CancellationToken ct);
    Task<bool> AnyRealUserAsync(CancellationToken ct);                          // IsSystem = false
    Task<int> CountActiveAdminsAsync(CancellationToken ct);
    Task<UserPage> SearchAsync(UserSearch search, CancellationToken ct);
    Task<IReadOnlyList<UserOption>> ListCashiersAsync(CancellationToken ct);
    void Add(User user);
    Task<SaveOutcome> SaveChangesAsync(CancellationToken ct);                   // Duplicate por índice único, Conflict por versión

    // Contador de bloqueo sin versión ni auditoría de fila (research §9).
    Task RecordLoginAttemptAsync(Guid id, int failedCount, DateTime? lockoutEndsAt, DateTime? lastLoginAt, CancellationToken ct);
}

// Users — contraseñas (research §1).
public interface IPasswordHasher
{
    string Hash(string password);
    PasswordCheck Verify(string password, string storedHash);  // Failed | Succeeded | SucceededRehashNeeded
}

// Users — concesiones de autorización en memoria (singleton).
public interface IAuthorizationGrants
{
    Guid Issue(Permission permission, Guid requestedBy, Guid authorizedBy);
    Guid? TryConsume(Guid grantId, Permission permission, Guid requestedBy);  // devuelve AuthorizedBy
}

// Audit — lectura de la bitácora (FR-027).
public interface IAuditLogReader
{
    Task<AuditPage> SearchAsync(AuditSearch search, CancellationToken ct);
}

// Abstractions/IAuditLog — se agrega el autorizador opcional.
void Add(string action, string entityType, Guid entityId, string? details, Guid? authorizedBy = null);

// Sales/ISaleDraftStore — por usuario.
Task<StoredDraft?> LoadAsync(CancellationToken ct);                // del usuario actual
Task SaveAsync(Guid draftId, IReadOnlyList<DraftLineDto> lines, CancellationToken ct);
void Remove();                                                     // del usuario actual
void RemoveFor(Guid userId);                                       // desactivación
Task ReassignAsync(Guid fromUserId, Guid toUserId, CancellationToken ct);  // primer administrador
Task DiscardAsync(CancellationToken ct);                           // del usuario actual

// Security/ISecuritySettingsStore — preferencias locales (research §13).
SecuritySettings Load();
void Save(SecuritySettings settings);
```

## Implementaciones (Pos.Infrastructure)

| Puerto | Implementación |
|---|---|
| `IPasswordHasher` | `Security/Pbkdf2PasswordHasher` |
| `IUserRepository` | `Users/UserRepository` (`ExecuteUpdateAsync` para el contador) |
| `IAuditLogReader` | `Audit/AuditLogReader` (`LEFT JOIN Users` para los nombres) |
| `ISecuritySettingsStore` | `Security/PreferencesSecuritySettingsStore` sobre `IPreferencesStore` |
| `ICurrentUser` | **ya no** `SystemCurrentUser` (se elimina). Se registra `UserSession` de Application |

`UserSession`, `AccessControl`, `AuthorizationGrants` y `LoginThrottle` (contador de nombres
inexistentes) son clases de Application sin dependencias de infraestructura, registradas en
`AddApplication()`.
