# Modelo de datos: Usuarios, inicio de sesión y roles

**Funcionalidad**: `007-users-roles-auth` | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

Migración nueva: `UsersAndRoles`. Resumen de cambios de esquema:

| Tabla | Cambio | ¿Reconstrucción? |
|---|---|---|
| `Users` | nueva, con la fila "Sistema" sembrada (`HasData`) | no |
| `AuditEntries` | + `AuthorizedBy` (nula), + índices `CreatedAt` y `CreatedBy` | no (`ADD COLUMN`) |
| `SaleDrafts` | llave primaria `Slot` → `UserId` | **sí**: revisar el SQL (ver abajo) |
| `Sales` | + índice `IX_Sales_CreatedBy_CreatedAt` | no |

---

## User (Domain: `Pos.Domain/Users/User.cs`)

Persona que opera el sistema, más la fila especial "Sistema". Nunca se borra físicamente (FR-017).

| Campo | Tipo (C# / SQLite) | Reglas |
|---|---|---|
| `Id` | `Guid` / TEXT | GUID v7; llave primaria. "Sistema" = `00000000-0000-7000-8000-000000000001` |
| `FullName` | `string` / TEXT(100) | Obligatorio, sin espacios al inicio ni al final, de 1 a 100 caracteres |
| `UserName` | `string` / TEXT(40) | Tal como se capturó; de 3 a 40 caracteres; solo letras, dígitos, `.`, `_` y `-` |
| `NormalizedUserName` | `string` / TEXT(40) | `UserName.ToUpperInvariant()`; **índice único** `IX_Users_NormalizedUserName` |
| `Role` | `UserRole` / TEXT(10) | `Admin` o `Cashier`, como código de texto (igual que `SaleStatus`) |
| `IsActive` | `bool` / INTEGER | Un usuario inactivo no puede iniciar sesión ni autorizar |
| `IsSystem` | `bool` / INTEGER | Verdadero solo en "Sistema"; se excluye de listados, del inicio de sesión y del filtro por cajero |
| `PasswordHash` | `string?` / TEXT(200) | `pbkdf2-sha256$<iter>$<sal>$<hash>`; nulo solo en "Sistema" |
| `MustChangePassword` | `bool` / INTEGER | Verdadero al crear el usuario y al restablecer su contraseña |
| `FailedLoginCount` | `int` / INTEGER | Fallos consecutivos; se actualiza con `ExecuteUpdate`, sin versión |
| `LockoutEndsAt` | `DateTime?` / TEXT (UTC) | Fin del bloqueo temporal; se actualiza con `ExecuteUpdate` |
| `LastLoginAt` | `DateTime?` / TEXT (UTC) | Informativo; se actualiza con `ExecuteUpdate` |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | auditoría | Los asigna `AuditingInterceptor` |
| `DeletedAt` | `DateTime?` | Se incluye por uniformidad con el Principio IV; siempre nulo (se desactiva, no se borra) |
| `Version` | `int` | Concurrencia optimista del formulario de edición |

**Métodos de dominio**:

- `User.Create(fullName, userName, role, passwordHash)` crea el usuario activo con
  `MustChangePassword = true`.
- `User.CreateFirstAdmin(fullName, userName, passwordHash)` crea un administrador activo con
  `MustChangePassword = false`: la contraseña la eligió él mismo.
- `Rename(fullName)`, `ChangeUserName(userName)`, `ChangeRole(role)`, `Activate()` y
  `Deactivate()`.
- `SetPassword(hash, mustChange)` se usa en el restablecimiento (`mustChange = true`) y en el
  cambio propio (`false`).
- Bloqueo, sin estado mutable fuera de estos métodos:
  - `IsLockedOut(nowUtc)`.
  - `RegisterFailedLogin(nowUtc)`: devuelve `true` si con este fallo queda bloqueado.
  - `RegisterSuccessfulLogin(nowUtc)`.
- `UserNameRules.Normalize(string)` y `UserNameRules.IsValid(string)` son estáticos para
  compartirlos con los validadores.

**Constantes**: `FullNameMaxLength = 100`, `UserNameMinLength = 3`, `UserNameMaxLength = 40`,
`PasswordMinLength = 8`, `MaxFailedAttempts = 5` y `LockoutDuration = 5 min`.

**Transiciones de estado**:

```text
          Create / CreateFirstAdmin
                   │
                   ▼
   ┌──────── Activo ◄──────── Activate ────────┐
   │   (MustChangePassword según origen)       │
   │           │                               │
   │  5 fallos │ RegisterFailedLogin           │
   │           ▼                               │
   │   Bloqueado (LockoutEndsAt > now) ──── vence (5 min)
   │                                           │
   └── Deactivate ─► Inactivo ─────────────────┘
        (protección: nunca el último administrador activo ni uno mismo;
         descarta su venta conservada)
```

---

## UserRole y Permission (Domain: `Pos.Domain/Users/`)

- `UserRole { Admin, Cashier }` con códigos de texto `ADMIN` y `CASHIER`.
- `Permission` (enum): `Sell`, `ViewOwnSales`, `ViewAllSales`, `ViewProducts`, `ManageProducts`,
  `ViewInventory`, `RegisterMovements`, `CancelSales`, `OpenDrawerWithoutSale`, `ManageUsers`,
  `ViewAuditLog`, `ManageSettings` y `ExportDiagnostics`.
- `RolePermissions`: la tabla de asignación es el **único** punto de definición (FR-012). Se
  detalla en [research §5](research.md#5-modelo-de-permisos-fr-010-a-fr-012).
  - `bool Has(UserRole, Permission)`.
  - `IReadOnlySet<Permission> For(UserRole)`.
  - `bool IsAuthorizable(Permission)`: verdadero solo para `CancelSales` y
    `OpenDrawerWithoutSale`.

No se persisten: son código.

---

## AuditEntry (existente, se extiende)

| Campo | Cambio |
|---|---|
| `AuthorizedBy` | **nuevo**, `Guid?`: administrador que autorizó la operación (FR-014) |
| Índices | + `IX_AuditEntries_CreatedAt` y `IX_AuditEntries_CreatedBy` para la consulta (FR-027) |

`AuditEntry.Create(action, entityType, entityId, details, authorizedBy = null)`. Sigue siendo
inmutable: `PosDbContext` rechaza cualquier modificación o borrado. El catálogo de acciones está
en [research §14](research.md#14-bitácora-de-auditoría-eventos-nuevos-y-consulta-fr-024-y-fr-027).

---

## SaleDraft (existente, cambia la llave)

Venta en curso durable, ahora **una por usuario**. Es la "venta conservada" de la Historia 8.

| Campo | Antes | Después |
|---|---|---|
| `Slot` (PK, `CHECK = 1`) | llave primaria | **se elimina**, junto con `CK_SaleDrafts_Slot` |
| `UserId` | — | **nueva llave primaria**, `Guid` |
| `DraftId`, `LinesJson`, `UpdatedAt` | sin cambio | sin cambio |

- `SaleDraft.Create(userId, draftId, linesJson, utcNow)`; `Replace` no cambia.
- `ISaleDraftStore` opera siempre sobre el `ICurrentUser.UserId`. Se agrega
  `RemoveFor(Guid userId)` para el descarte al desactivar un usuario, y `ReassignAsync(from, to)`
  para el primer administrador.

**Revisión del SQL (reconstrucción)**: el `INSERT INTO "ef_temp_SaleDrafts" … SELECT` debe copiar
`DraftId`, `LinesJson` y `UpdatedAt`, y asignar
`UserId = '00000000-0000-7000-8000-000000000001'`. Como el `CHECK` garantizaba una sola fila,
nunca hay más de una fila que copiar.

Para que la fila existente reciba ese valor, `UserId` se configura con
`HasDefaultValue(SystemUser.Id)`. Es el mismo patrón que `Products.UnitCode` en
[docs/migraciones.md](../../docs/migraciones.md): la columna nueva entra con valor por defecto
antes de copiarse. Las filas nuevas siempre traen `UserId` explícito, así que el valor por
defecto no tiene efecto en la operación normal.

Si el SQL generado no copia la fila, se ajusta la configuración y se regenera la migración antes
de integrarla. Las migraciones no se editan a mano. La prueba de la base de ejemplo `v0.5.0.db`
con borrador lo verifica.

---

## Sale (existente, sin columnas nuevas)

- `CreatedBy` = cajero que realizó la venta (Historia 6, FR-020). `CancelledBy` = solicitante de la
  cancelación. El autorizador queda en la bitácora.
- Índice nuevo `IX_Sales_CreatedBy_CreatedAt`.
- DTOs:
  - `SaleSearch` + `CashierId Guid?`.
  - `SaleListItemDto` + `CashierName`.
  - `SaleDetailDto.CreatedByName` ahora sale de `Users.FullName`.

---

## Objetos de Application (no persistidos)

| Tipo | Contenido | Vida |
|---|---|---|
| `SessionUser` | `Id`, `FullName`, `UserName`, `Role`, `Initials` | Instantánea al iniciar sesión; `UserSession` la conserva hasta cerrar sesión |
| `AuthorizationGrant` | `Id`, `Permission`, `RequestedBy`, `AuthorizedBy`, `ExpiresAt` | En memoria (`AuthorizationGrants`), 2 min, un solo uso |
| `AccessDecision` | `Allowed`, `AuthorizedBy Guid?`, `Error Forbidden?` | Resultado de `IAccessControl.CheckAsync` |
| `SecuritySettings` | `IdleLockMinutes` (0 = desactivado, por defecto 15, máximo 240) | `preferences/security.json` mediante `IPreferencesStore` |
| Contador de nombres inexistentes | nombre normalizado → (fallos, fin de bloqueo) | En memoria dentro de `SignInHandler`/`LoginThrottle` (singleton) |

---

## Relaciones

```text
Users 1 ──── 0..1 SaleDrafts            (PK = UserId)
Users 1 ──── * Sales.CreatedBy           (lógica, sin FK; research §3)
Users 1 ──── * AuditEntries.CreatedBy    (lógica)
Users 1 ──── * AuditEntries.AuthorizedBy (lógica)
Users 1 ──── * Products/InventoryMovements .CreatedBy/.UpdatedBy (lógica)
```
