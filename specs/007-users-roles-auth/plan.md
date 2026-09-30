# Implementation Plan: Usuarios, inicio de sesión y roles

**Branch**: `007-users-roles-auth` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/007-users-roles-auth/spec.md`

## Summary

Cada operación queda ligada a la persona que la hizo, y cada rol solo puede hacer lo que tiene
permitido. El usuario de sistema fijo deja de ser el único usuario.

1. **Usuarios**: una tabla `Users` con la fila "Sistema" sembrada, que reutiliza el id que ya
   tienen todos los registros previos. Con eso, la migración no actualiza datos (research §2).
   - Las contraseñas se guardan con PBKDF2-SHA256 del runtime, sin paquetes nuevos (research §1).
   - El nombre de usuario es único sin distinguir mayúsculas (research §10).
   - El bloqueo tras 5 fallos dura 5 minutos y se persiste (research §9).
2. **Sesión**: `UserSession`, un singleton de Application, implementa el `ICurrentUser` existente.
   Los casos de uso y el interceptor de auditoría no cambian (research §4).
3. **Permisos**:
   - Una sola tabla `RolePermissions` en el dominio (research §5).
   - Cada caso de uso restringido la verifica con `IAccessControl`, que relee al usuario (activo
     y rol) de la base y devuelve `Forbidden` sin efectos (research §6).
   - El cajero solo accede a sus propias ventas (research §7).
4. **Autorización de administrador**: una concesión en memoria, de un solo uso y ligada a permiso
   y solicitante. La bitácora registra al solicitante y al autorizador (`AuditEntries.AuthorizedBy`)
   (research §8).
5. **Venta conservada**: el borrador durable de 005 pasa a ser una fila por usuario. Sobrevive al
   reinicio y se descarta al desactivar al usuario (research §11).
6. **Interfaz**:
   - Un `RootViewModel` alterna entre asistente, inicio de sesión, cambio de contraseña y shell.
   - Cada sesión tiene su propio ámbito de DI, con menú y pantallas limpios y filtrados por rol
     (research §12).
   - Bloqueo por inactividad con capa sobre el shell (research §13).
   - Pantallas nuevas: Usuarios, Bitácora y Seguridad (research §14, [contracts/ui.md](contracts/ui.md)).

Hay una migración nueva, `UsersAndRoles`. Reconstruye solo `SaleDrafts`, una tabla de una fila.
No se agrega ninguna dependencia externa.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12.1.3, CommunityToolkit.Mvvm, Hosting, Serilog,
EF Core 10 Sqlite, FluentValidation). **No se agrega ninguna**: PBKDF2 viene en
`System.Security.Cryptography` (research §1).

**Storage**:

- SQLite, migración `UsersAndRoles`:
  - Tabla `Users` con "Sistema" sembrado.
  - `AuditEntries.AuthorizedBy` y dos índices.
  - `SaleDrafts` con llave primaria `UserId` (reconstrucción de una tabla de una fila).
  - Índice `IX_Sales_CreatedBy_CreatedAt`.
- Preferencias JSON: `preferences/security.json`, con el tiempo de inactividad.

**Testing**: xUnit v3, según la política mínima de la constitución v1.2.0 (detalle en research §16):

- **Domain**:
  - Reglas del nombre de usuario.
  - Regla de bloqueo.
  - Matriz de permisos del cajero.
- **Application**:
  - Rechazo del cajero en **todas** las operaciones restringidas, con una prueba parametrizada
    (SC-002).
  - Inicio de sesión (mensaje genérico, bloqueo).
  - Protección del último administrador.
  - Descarte de la venta conservada.
  - Concesión de autorización.
  - Propiedad de las ventas.
- **Infrastructure** (SQLite real):
  - Migración de las bases de ejemplo, más la nueva `v0.5.0.db`.
  - Índice único con altas simultáneas.
  - Asistente ejecutado dos veces.
  - Bloqueo persistente.
  - Hasher PBKDF2.
- **Arquitectura**: sin cambios.
- Sin pruebas de ViewModels ni de vistas.

**Target Platform**: Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Inicio de sesión menor a 1 s en un equipo de caja modesto (PBKDF2 con 600 000 iteraciones,
  alrededor de 0.5 s).
- Del inicio de sesión al Punto de venta, menos de 15 s (SC-007).
- La verificación de permisos es una lectura por llave primaria (menos de 5 ms).
- La consulta de la bitácora, 100 filas indexadas por fecha, en menos de 200 ms con 100 000
  eventos.

**Constraints**:

- Funciona sin conexión.
- 0 advertencias.
- Ninguna contraseña en claro, ni en la base, ni en la bitácora, ni en los logs (SC-008, Principio
  VIII).
- El respaldo y la migración siguen el orden obligatorio del Principio IV.
- Una falla al guardar el borrador sigue sin interrumpir la venta.

**Scale/Scope**:

- 2 roles y 13 permisos.
- Usuarios: decenas por instalación.
- Pantallas nuevas: asistente, inicio de sesión, cambio de contraseña, bloqueo, Usuarios con su
  formulario, Bitácora, Seguridad y el diálogo de autorización.
- Alrededor de 20 casos de uso existentes reciben la verificación de permiso.

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó tras el diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Autenticación local, sin red. La venta en curso nunca se pierde: está en el borrador del usuario al cerrar sesión, cambiar de usuario, reiniciar o bloquear por inactividad (la capa no desecha el estado). Las escrituras de usuarios con varios registros (desactivar + descartar borrador + bitácora) van en una sola transacción. |
| II. Capas | `User`, `Permission` y `RolePermissions` están en Domain. Los casos de uso, los puertos (`IUserRepository`, `IPasswordHasher`, `IAuditLogReader`, `ISecuritySettingsStore`), `UserSession` y `AccessControl` están en Application. Las implementaciones EF Core y PBKDF2 están en Infrastructure. Desktop solo invoca casos de uso. Las pruebas de arquitectura existentes cubren las carpetas nuevas. |
| III. Lógica en el núcleo | Bloqueo, normalización del nombre, matriz de permisos y protección del último administrador están en Domain y Application. Los ViewModels solo usan `RolePermissions.Has` para ocultar botones; la protección real está en el caso de uso (FR-011). |
| IV. Integridad de datos | `User` tiene GUID v7, fechas UTC, auditoría, `Version` y `DeletedAt`. Los usuarios no se borran (FR-017). "Sistema" es un dato fijo sembrado con `HasData`; el primer administrador es un dato de la instalación y se crea en el asistente. La migración es EF Core y su SQL se revisa, sobre todo la reconstrucción de `SaleDrafts` (data-model). La base de ejemplo `v0.5.0.db` se agrega a la prueba de migración. |
| V. Multiplataforma | Sin código de plataforma. PBKDF2 y `IPreferencesStore` son multiplataforma. |
| VI. Calidad verificable | Solo se prueban reglas y validaciones de integridad: bloqueo, permisos, último administrador, propiedad de ventas y unicidad. También la migración (obligatoria) y la arquitectura (sin cambios). Las pruebas de persistencia usan SQLite real. |
| VII. Simplicidad | Sin dependencias nuevas, sin ASP.NET Identity, sin permisos editables ni roles personalizados. El repositorio es específico de `User`. La concesión de autorización es un diccionario en memoria. El ámbito de DI por sesión reemplaza la limpieza manual por pantalla. |
| VIII. Soporte | Los inicios de sesión, fallos y rechazos por permiso se registran en Serilog con el usuario y la operación, nunca con contraseñas. La bitácora es consultable en la aplicación. |
| IX. Seguridad local | PBKDF2 (hash robusto). Las anulaciones y la apertura del cajón sin venta llevan al solicitante y al autorizador en la bitácora. Todos los eventos de seguridad de FR-024 quedan en la bitácora inmutable. No hay secretos en el repositorio: no existe usuario ni contraseña por defecto, porque el administrador se crea en el asistente. |

**Resultado**: sin violaciones. Hay dos decisiones que conviene hacer explícitas (no son
violaciones):

- Los campos de auditoría quedan sin llave foránea hacia `Users` para no reconstruir cinco tablas
  (research §3).
- `SecuritySettings` va en preferencias locales, igual que la impresión, y no en SQLite
  (research §13).

## Project Structure

### Documentation (this feature)

```text
specs/007-users-roles-auth/
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
│   ├── Users/
│   │   ├── User.cs                                # reglas de nombre, bloqueo, activación
│   │   ├── UserRole.cs                            # Admin | Cashier (+ códigos)
│   │   ├── UserNameRules.cs
│   │   ├── Permission.cs
│   │   └── RolePermissions.cs                     # único punto rol → permisos (FR-012)
│   ├── Audit/AuditEntry.cs                        # + AuthorizedBy
│   └── Sales/SaleDraft.cs                         # Slot → UserId
├── Pos.Application/
│   ├── Abstractions/
│   │   ├── Error.cs                               # + Forbidden, InvalidCredentials, LockedOut, LastAdministrator
│   │   ├── IAuditLog.cs                           # + authorizedBy
│   │   └── SystemUser.cs                          # se conserva el Id; NameOf deja de usarse
│   ├── Audit/
│   │   ├── AuditActions.cs                        # catálogo de eventos
│   │   ├── IAuditLogReader.cs
│   │   └── SearchAuditLog/
│   ├── Users/
│   │   ├── IUserRepository.cs  IPasswordHasher.cs  UserDtos.cs  UserMessages.cs  UserFields.cs
│   │   ├── Session/                               # UserSession (ICurrentUser), SessionUser
│   │   ├── Access/                                # IAccessControl, AccessControl, AuthorizationGrants, LoginThrottle
│   │   ├── GetSetupState/  CreateFirstAdmin/  SignIn/  StartSession/  EndSession/
│   │   ├── VerifySessionPassword/  ChangeOwnPassword/  AuthorizeAdmin/
│   │   ├── SearchUsers/  GetUser/  CreateUser/  UpdateUser/  ResetUserPassword/  ListCashiers/
│   ├── Security/
│   │   ├── SecuritySettings.cs  ISecuritySettingsStore.cs
│   │   └── GetSecuritySettings/  SaveSecuritySettings/
│   ├── Products/ Inventory/ Sales/ Printing/ Business/ Diagnostics/   # + verificación de permiso
│   ├── Sales/SearchSales/                         # + CashierId, propiedad
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── Users/UserRepository.cs
│   ├── Security/Pbkdf2PasswordHasher.cs  PreferencesSecuritySettingsStore.cs
│   ├── Audit/AuditLogReader.cs
│   ├── Platform/SystemCurrentUser.cs              # se elimina
│   ├── Sales/SqliteSaleDraftStore.cs  SaleRepository.cs   # por usuario; nombres con JOIN Users
│   ├── Inventory/InventoryRepository.cs           # nombres con JOIN Users
│   ├── Persistence/Configurations/UserConfiguration.cs  (+ AuditEntry, SaleDraft, Sale)
│   ├── Persistence/Migrations/…_UsersAndRoles.cs
│   └── DependencyInjection.cs
├── Pos.Desktop/
│   ├── Shell/          RootViewModel, SessionScope, IdleMonitor, LockOverlay*, MainView (extraída de MainWindow)
│   ├── Auth/           FirstAdmin*, Login*, ChangePassword*, AdminAuthorization*, UserSectionViewModel
│   ├── Administration/ AdministrationModule, Users*, UserEditor*, ResetPassword*, AuditLog*
│   ├── Settings/       + SecuritySettings*
│   ├── Navigation/     + Permission en NavigationEntry/AddPage; registro y menú scoped por sesión
│   ├── Sales/          + filtro y columna Cajero; autorización al cancelar y abrir cajón
│   ├── Products/ Inventory/ About/ Home/   # botones Can…; tarjetas con permiso
│   ├── Common/DialogService.cs             # ocultar y restaurar diálogos al bloquear
│   └── Composition/    HostBuilder (páginas scoped), App (RootViewModel)
tests/
├── Pos.Domain.Tests/Users/            UserNameRulesTests, UserLockoutTests, RolePermissionsTests
├── Pos.Application.Tests/Users/       SignInHandlerTests, UpdateUserHandlerTests, AuthorizeAdminHandlerTests
├── Pos.Application.Tests/Security/    RestrictedOperationsTests (SC-002)
├── Pos.Application.Tests/Sales/       SalesOwnershipTests
├── Pos.Infrastructure.Tests/Users/    UserPersistenceTests, Pbkdf2PasswordHasherTests
└── Pos.Infrastructure.Tests/SampleDatabases/  v0.5.0.db + casos en SampleDatabaseUpgradeTests
docs/
├── usuarios-y-permisos.md             # guía de soporte: roles, desbloqueo, restablecer, bitácora
└── migraciones.md                     # + nota de la reconstrucción de SaleDrafts
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de 001 a 006.

- `Users` y `Security` son carpetas nuevas en Domain, Application e Infrastructure.
- En Desktop:
  - `Auth` agrupa las vistas previas a la sesión.
  - `Administration` es un módulo de navegación nuevo, con el grupo "Administración" (orden 70,
    antes de Configuración, que tiene 80).
  - `Shell` pasa a hospedar el `RootViewModel`.
- Las pruebas de Desktop existentes que usan `FixedCurrentUser` siguen siendo válidas. Las que
  resuelven pantallas como singletons se ajustan al registro scoped. No se agregan pruebas de
  ViewModels.

## Complexity Tracking

Sin violaciones de la constitución; no aplica.
