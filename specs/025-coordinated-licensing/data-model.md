# Data Model: Licenciamiento coordinado con OctopusAdmin

**Feature**: 025-coordinated-licensing | **Fecha**: 2026-10-03

## Glosario de versiones

Hay tres numeraciones independientes. En estos documentos siempre se nombran así:

| Término | Qué es | Versiones |
|---------|--------|-----------|
| **Licencia formato N** | Archivo `.lic` que emite OctopusAdmin (`format` del sobre). | 2 retirado; 3 vigente. |
| **Archivo de prueba vN** | Registro local cifrado de la prueba, `license.lic` en la carpeta de datos (no es una licencia del proveedor, aunque comparte la extensión). | v1 (011) ya no se lee; v2 (012) se lee y se migra; v3 se escribe. |
| **Solicitud vN** | Archivo `.octoreq` (`requestFormat`). | v2 vigente. |

## Domain (`Pos.Domain/Licensing`)

### `LicensedModule` (enum) — ampliado

`Pos`, `Inventory`, `AdvancedReports`, `CreditAndCustomers`, `CashShifts`, `Returns`,
`Suppliers`, `Discounts`, `Categories`. El valor numérico no se persiste en ningún lado; el GUID
del catálogo es la identidad.

### `ModuleCatalog` (constantes) — ampliado

| Módulo | GUID | Key | Orden | Base |
|--------|------|-----|-------|------|
| Pos | `4c2517f5-096a-460e-8ee7-b09e46572952` | `pos` | 1 | sí |
| Inventory | `7a99f06e-6c58-43fb-bc34-20bec30f8060` | `inventory` | 2 | |
| AdvancedReports | `d6c0dca2-2400-481b-94fc-2d90688eedfc` | `advanced_reports` | 3 | |
| CreditAndCustomers | `be39a38f-0371-4b15-9bb6-fcb9c1bb1b7a` | `credit_customers` | 4 | |
| CashShifts | `b2145dea-fd36-48bc-85ba-773875b4a298` | `cash_shifts` | 5 | |
| Returns | `2b1ab797-3339-43ef-a135-022f998177ca` | `returns` | 6 | |
| Suppliers | `1e6111ca-514b-497e-b553-154960c946f3` | `suppliers` | 7 | |
| Discounts | `01a0f957-082e-72cb-9c5a-9cc8fbee6772` | `discounts` | 8 | |
| Categories | `7b4ae9e2-6e3f-4006-b259-b7d4dcd06933` | `categories` | 9 | |

API: `All` (en orden), `Base` (= `Pos`), `IdOf`, `KeyOf`, `OrderOf`, `TryGetModule(Guid)`.
Fuente de verdad del contrato: `contracts/module-catalog.json` (prueba de consistencia, research §5).

### `ModuleGrant` (record) — nuevo

Entrada de módulo de una licencia ya verificada.

| Campo | Tipo | Regla |
|-------|------|-------|
| `Module` | `LicensedModule` | Solo módulos conocidos (los GUID desconocidos se descartan al leer). |
| `ActivatesOn` | `DateOnly` | Fecha local. |
| `ExpiresOn` | `DateOnly?` | `null` = indefinido. Inclusivo. |

`IsActiveOn(DateOnly d) => ActivatesOn <= d && (ExpiresOn is null || d <= ExpiresOn)`.

### `SignedLicense` (record) — nuevo

Contenido verificado de una licencia v3 (no incluye la firma; esa vive en el texto guardado).

| Campo | Tipo |
|-------|------|
| `LicenseId` | `Guid` |
| `IssuedAtUtc` | `DateTime` (UTC) |
| `MachineId` | `string` |
| `CustomerName` | `string` |
| `Grants` | `IReadOnlyList<ModuleGrant>` |

Regla de antigüedad: `IsAcceptableReplacementFor(SignedLicense? current)` → `current is null`
o `LicenseId == current.LicenseId` o `IssuedAtUtc > current.IssuedAtUtc`.

### `TrialRecord` (record) — reemplaza a `LicenseRecord`

| Campo | Tipo | Regla |
|-------|------|-------|
| `MachineId` | `string` | |
| `FirstRunUtc` | `DateTime` | Nunca aumenta; si la copia protegida está alterada = `DateTime.MinValue` (prueba vencida). |
| `LastSeenUtc` | `DateTime` | Nunca retrocede. |
| `TrialDays` | `int` | 30. |
| `LicenseImportedUtc` | `DateTime?` | Se fija al aceptar la primera licencia y nunca vuelve a nulo (en la reconciliación se toma de la copia que lo tenga). Con valor, la prueba ya no aplica (FR-026a). |

Se elimina `Modules` (la licencia firmada es la única fuente de módulos).

### `LicenseStatus` (record, calculado, no se guarda) — rediseñado

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `Overall` | `LicenseOverall` | `Trial`, `Licensed`, `Blocked`. |
| `BlockReason` | `LicenseBlockReason?` | `TrialExpired`, `LicenseInvalid`, `BaseNotLicensed`, `BasePending`, `BaseExpired`; solo con `Blocked`. |
| `TrialDaysRemaining` | `int` | 0 fuera de la prueba. |
| `TrialWarning` | `LicenseWarning` | `None`, `Near` (5 días), `Urgent` (1 día). |
| `CustomerName` | `string?` | De la licencia vigente. |
| `Modules` | `IReadOnlyList<ModuleStatus>` | Los 9, en orden del catálogo. |
| `ExpiringSoon` | `IReadOnlyList<ModuleStatus>` | Activos con `ExpiresOn` en `[hoy, hoy + 7]`. |
| `ClockBehind` | `bool` | Reloj atrasado > 1 día respecto de la última fecha vista. |
| `StoredLicenseRejected` | `bool` | Había licencia guardada pero no pasó la reverificación. |

`IsBlocked => Overall == Blocked`; `IsModuleActive(m) => Modules[m].State == Active`.
Con `Blocked`, los módulos conservan su estado individual (se muestra en la pantalla) pero el
sistema rechaza todo lo no exento (research §10).

### `ModuleStatus` (record)

| Campo | Tipo |
|-------|------|
| `Module` | `LicensedModule` |
| `State` | `ModuleState`: `Active`, `Pending`, `Expired`, `NotLicensed` |
| `ActivatesOn` | `DateOnly?` |
| `ExpiresOn` | `DateOnly?` |

Una entrada con `ExpiresOn < ActivatesOn` es **inválida**: nunca está activa y cuenta siempre
como `Expired`, nunca como `Pending`, aunque su `ActivatesOn` sea futura (caso límite de la spec).

Con varias entradas para el mismo módulo: `Active` si alguna está activa (fechas de esa entrada);
si no, `Pending` si alguna entrada válida es futura (la más próxima); si no, `Expired` (la de
vencimiento más reciente, incluidas las inválidas).

### `LicenseEvaluator` (estático) — rediseñado

`Evaluate(TrialRecord trial, SignedLicense? license, bool storedLicenseRejected, DateOnly today, DateOnly lastSeen)`:

1. Si `today >= lastSeen`: estado de módulos en `today`.
2. Si `today < lastSeen`: activo solo si está activo en `today` **y** en `lastSeen`;
   `ClockBehind = lastSeen.DayNumber - today.DayNumber > 1`.
3. Con `license` → `Licensed` si el módulo base está activo; si no, `Blocked` con
   `BaseNotLicensed` (sin entrada), `BasePending` (solo futura) o `BaseExpired`.
4. Sin `license` y con `storedLicenseRejected` **o** `trial.LicenseImportedUtc` →
   `Blocked/LicenseInvalid` (la prueba no se reanuda tras haber licenciado, aunque le queden días;
   `storedLicenseRejected` cubre los casos en que `LicenseImportedUtc` se perdió, como un sello
   ilegible por cambio de hardware).
5. Sin `license`, sin licencia guardada rechazada y sin `LicenseImportedUtc` → días restantes = `TrialDays − (max(today, lastSeen) − fecha(FirstRunUtc))`;
   > 0 → `Trial` con los 9 activos y avisos exactos de 5 y 1 día; ≤ 0 → `Blocked/TrialExpired`.

### `ModuleAccess` — ampliado

`RequiredModules(Permission) : IReadOnlyList<LicensedModule>` (vacío = siempre disponible):

| Permisos | Módulos |
|----------|---------|
| `ViewInventory`, `RegisterMovements` | Inventory |
| `ManageSuppliers`, `ViewPurchaseReport` | Suppliers |
| `RegisterPurchases`, `VoidPurchases` | Suppliers + Inventory |
| `ViewReports` | AdvancedReports |
| `OperateShift`, `WithdrawCash`, `ManageShifts`, `GenerateShiftReadout` | CashShifts |
| `ProcessReturns`, `ApproveReturns`, `ManageCreditNotes` | Returns |
| `ManageCustomers`, `SellOnCredit`, `RegisterCustomerPayments`, `ManageCustomerCredit`, `ApproveCreditOverLimit`, `VoidCustomerPayments`, `ViewReceivables` | CreditAndCustomers |
| `ApplyDiscounts`, `ApproveDiscounts`, `ManageDiscounts`, `ViewDiscountReport` | Discounts |
| `ManageCategories` | Categories |

### `LicenseLock` (estático) — nuevo

`ExemptPermissions`: `ManageLicense`, `ExportBackup`, `Sell`, `ApplyDiscounts`,
`ApproveDiscounts`, `SellOnCredit`, `ApproveCreditOverLimit`, `OperateShift`, `ManageShifts`.

### `Permission` — nuevos valores

`ManageCategories` (mismos roles que `ManageProducts`), `ExportBackup` (solo Administrador).
Se agregan al final del enum. `Permission` no se persiste en la base (los roles se resuelven en
código con `RolePermissions`), así que no hay migración de datos.

## Application (`Pos.Application/Licensing`)

| Tipo | Cambio |
|------|--------|
| `ILicenseVerifier` | `Verify(string content, string machineId) : LicenseVerification` sobre el **texto** (permite reverificar lo guardado). `Valid(SignedLicense)` / `Rejected(LicenseImportRejection)`. |
| `LicenseImportRejection` | `Unreadable`, `UnsupportedFormat` (nuevo), `BadSignature`, `OtherMachine`, `NotNewer` (nuevo). |
| `IInstalledLicenseStore` (nuevo) | `Task<string?> ReadAsync()`, `Task ReplaceAsync(string content, DateTime importedAtUtc)`. |
| `ILicenseStore` | Pasa a guardar `TrialRecord` (sin módulos); `LegacyV1` se elimina. |
| `IModuleCatalogInfo` (nuevo) | `int CatalogVersion`, `string NameOf(LicensedModule)`, `string DescriptionOf(LicensedModule)`. |
| `ILicenseState` | `Current : LicenseStatus`, `Set(TrialRecord, SignedLicense?, bool storedRejected)`, `Refresh()` compara huella completa. `Record` → `Trial`, `License`. |
| `LicenseBootstrapper` | Además lee y reverifica `IInstalledLicenseStore`; registra en bitácora `LicenseStoredRejected`. Si la licencia guardada es válida y `LicenseImportedUtc` es nulo, lo fija (recupera un corte entre guardar la licencia y el sello). |
| `ImportLicenseHandler` | Lee el archivo, verifica (con `NotNewer` frente a `ILicenseState.License`), reemplaza en `IInstalledLicenseStore`, fija `TrialRecord.LicenseImportedUtc` si estaba nulo (archivo y sello), actualiza estado, audita. Ya no toca módulos del `TrialRecord`. |
| `ExportLicenseRequestHandler` | `requestFormat` 2 con `businessName` y `catalogVersion`. `AccessControl.CheckSessionAsync` (ya no `ManageLicense`). |
| `GetLicenseStatusHandler` | DTO con todo `LicenseStatus` + nombres del catálogo + ID de máquina + contacto. |
| `ExportBackupHandler` (nuevo, `Application/Backup/ExportBackup`) | `ExportBackupCommand(DestinationFilePath)`; permiso `ExportBackup`. |
| `AccessControl` | Regla de bloqueo con `LicenseLock.ExemptPermissions` → `SystemNotActivated`; regla de módulos con `RequiredModules`. Nuevo `CheckToFinishAsync(Permission)`: sesión y rol, sin regla de bloqueo ni de módulo; solo para terminar la venta del borrador, su ticket original y el turno abierto (blocked-mode §1). Nuevo `CheckSessionAsync()`: solo sesión, sin regla de permiso, bloqueo ni módulo (generar solicitud). |
| `PrintTicketHandler` | Impresión original de la venta propia recién cobrada con `CheckToFinishAsync`; en bloqueo rechaza reimpresiones e impresiones de turnos anteriores. |
| `GetCurrentShiftHandler`, `GetShiftDetailHandler`, `CountShiftCashHandler`, `CloseShiftHandler` | Para el turno abierto, `CheckToFinishAsync`, con o sin bloqueo (se consulta y se cierra aunque Turnos y arqueo no esté activo). |
| `SaveSaleDraftHandler`, `ConfirmSaleHandler` (partes) | Compara el comando con el borrador guardado: partes ya capturadas → `CheckToFinishAsync`; agregadas o cambiadas → `CheckAsync`. Con o sin bloqueo. |
| `OpenCashDrawerHandler`, consultas de turnos y cortes anteriores | En bloqueo, rechazo `SystemNotActivated`. |
| `SaveSaleDraftHandler`, `ConfirmSaleHandler` | En bloqueo solo el `DraftId` del borrador guardado con líneas. |
| `OpenShiftHandler`, `RegisterCashMovementHandler` | En bloqueo, rechazo `SystemNotActivated`. |
| Casos de uso de Categorías | `ManageProducts` → `ManageCategories`. `ListCategoryOptions` vacío si Categorías inactivo. |
| `UpdateProductHandler` | Con Categorías inactivo conserva `CategoryId` actual. |

Errores nuevos (`Pos.Application.Abstractions`): `SystemNotActivated(LicenseBlockReason)`.

## Infrastructure

### Tabla `InstalledLicenses` (nueva, migración EF Core)

| Columna | Tipo | Regla |
|---------|------|-------|
| `Id` | GUID (PK) | `Guid.CreateVersion7()`; una sola fila. |
| `Content` | TEXT, no nulo | Texto exacto del `.lic` importado (≤ 64 KiB). |
| `ImportedAtUtc` | TEXT (UTC) | Momento de la importación. |

Dato técnico de la instalación, como `LicenseSeals`: sin auditoría, borrado lógico ni versión.

### Otros cambios

| Tipo | Cambio |
|------|--------|
| `EcdsaLicenseVerifier` | Formato 3 según `contracts/license-format.md`; nueva clave pública de producción. |
| `LicenseCanonical` | Se elimina. |
| `LicenseFileStore` | Escribe el archivo de prueba v3 sin módulos (con `LicenseImportedUtc`). Lee el archivo de prueba v2 descartando sus módulos y devuelve `HadLegacyModules` si tenía alguno; en ese caso el bootstrapper fija `LicenseImportedUtc` y marca la licencia guardada como rechazada (FR-021). Deja de leer el v1. `AppSecret` → `KeyContext`. |
| `LicenseSealStore` | `AppSecret` → `KeyContext`; distingue "fila ausente" de "fila alterada" (`LicenseSealReadResult`); `SealContent` agrega `LicenseImportedUtc` (JSON cifrado en `Payload`, sin migración; ausente = nulo). |
| `InstalledLicenseStore` (nuevo) | Implementa `IInstalledLicenseStore` sobre `InstalledLicenses`. |
| `EmbeddedModuleCatalogInfo` (nuevo) | Lee `Pos.ModuleCatalog.json` embebido. |
| `Pos.Infrastructure.csproj` | `<EmbeddedResource Include="..\..\contracts\module-catalog.json" LogicalName="Pos.ModuleCatalog.json" />`. |

## Transiciones de estado

```text
                 importar licencia válida con POS activo
   [Prueba] ──────────────────────────────────────────────► [Licenciado]
      │                                                        │   ▲
      │ día 31 sin licencia                     POS vence /    │   │ importar licencia
      ▼                                         licencia sin   ▼   │ más reciente con POS
  [Bloqueado: TrialExpired]                     POS        [Bloqueado: BaseExpired /
      │                                                      BaseNotLicensed / BasePending]
      └──── importar licencia válida con POS activo ────────► [Licenciado]

  [Licenciado] ──(licencia guardada falta o no supera la reverificación)──► [Bloqueado: LicenseInvalid]
  [Bloqueado: LicenseInvalid] ──(importar licencia válida con POS activo)──► [Licenciado]
  (Tras la primera importación no se vuelve a [Prueba].)

  Por módulo:  NotLicensed ──(licencia lo incluye)──► Pending ──(llega ActivatesOn)──► Active
               Active ──(pasa ExpiresOn)──► Expired ──(licencia lo renueva)──► Active
```
