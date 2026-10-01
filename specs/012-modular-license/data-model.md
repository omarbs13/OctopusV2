# Modelo de datos: Licencia modular por módulos

## Dominio (`Pos.Domain/Licensing`)

### `LicensedModule` (enum)

`Inventory`, `AdvancedReports`, `CreditAndCustomers`, `CashShifts`, `Returns`.

### `ModuleCatalog` (estático, interno)

| Campo | Regla |
|---|---|
| `Guid` por módulo | Fijo en código, único, nunca visible en la interfaz ni configurable (FR-007) |
| `TryGetModule(Guid)` | Devuelve el módulo o nada; un GUID desconocido se ignora (FR-008) |
| `IdOf(LicensedModule)` | Para escribir el archivo y para el emisor |
| `All` | Los 5 módulos (se usan durante la evaluación y en la migración 011) |

### `ModuleAccess` (estático)

`Required(Permission) → LicensedModule?`. Ver tabla en [research.md §2](research.md). `null` = función no licenciable (siempre disponible, FR-021).

### `LicenseRecord` (contenido validado del archivo)

| Campo | Tipo | Notas |
|---|---|---|
| `Version` | int | 2 |
| `MachineId` | string | Debe coincidir con la máquina actual |
| `FirstRunUtc` | DateTime (UTC) | Inicio de la evaluación; inmutable salvo recuperación desde la copia protegida |
| `LastSeenUtc` | DateTime (UTC) | Nunca retrocede (FR-019) |
| `TrialDays` | int | 30 (FR-004) |
| `Modules` | conjunto de `LicensedModule` | Solo módulos conocidos y comprados; en el archivo se guardan como GUID |

### `LicenseStatus` (calculado, no se guarda)

| Campo | Notas |
|---|---|
| `Phase` | `Trial` (todos activos) o `Modular` (solo los comprados) |
| `DaysRemaining` | Días de evaluación; `0` en `Modular` |
| `Warning` | `None`, `Near` (exactamente 5) o `Urgent` (exactamente 1) |

`IsModuleActive(m) = Phase == Trial || Modules.Contains(m)`.

**Transiciones**: `Trial → Modular` el día siguiente al día 30 (por fecha local, con la fecha efectiva = máximo entre reloj y `LastSeenUtc`). `Modular` es definitivo; importar licencia solo agrega módulos. No hay transición de vuelta a `Trial`.

### Reglas de validación

- Importar: el archivo debe verificar la firma, ser formato 2 y de esta máquina; si no, se rechaza y la licencia no cambia (FR-010).
- Importar es idempotente y aditivo: unión de conjuntos, sin tocar `FirstRunUtc` (FR-011).
- `LastSeenUtc` solo avanza (`max(ahora, LastSeenUtc)`).

## Persistencia

### Archivo `license.lic` v2 (cifrado AES-256-GCM)

Contenedor igual al de 011 (ver [contracts/license-contracts.md](contracts/license-contracts.md)). Contenido JSON:

```text
{ machineId, firstRunUtc, lastSeenUtc, trialDays, modules: [guid, …] }
```

### Tabla `LicenseSeals` (nueva, una migración EF Core)

| Columna | Tipo | Restricciones |
|---|---|---|
| `Id` | GUID v7 | PK, generado en la aplicación |
| `Payload` | BLOB | NOT NULL; AES-256-GCM (nonce + etiqueta + cifrado) de `{ firstRunUtc, lastSeenUtc }` |

- Máximo una fila (la aplicación la lee y actualiza; no hay clave natural que imponer en SQLite sin sobreingeniería).
- **No** tiene `CreatedAt/By`, `UpdatedAt/By`, `DeletedAt` ni `Version` (Principio IV): no es una entidad de negocio, es un sello técnico de la instalación; el borrado lógico no tiene sentido y una auditoría de quién lo toca la hace la bitácora de licencia. Se justifica en el plan.
- Migración puramente aditiva (crear tabla): sin reconstrucción de tablas existentes; se revisa el SQL generado antes de integrar.
- Sin datos sembrados: la fila se crea en el primer arranque posterior a la actualización.

## Estado en memoria (`ILicenseState`)

| Miembro | Comportamiento |
|---|---|
| `Current` | `LicenseStatus` recalculado con el reloj en cada lectura, sin acceso a disco |
| `IsModuleActive(m)` | Consulta rápida para `AccessControl`, menú y casos de uso |
| `EnabledModules` | Para mostrar los nombres de los módulos activos en Acerca de |
| `Set(record)` | Reemplaza el registro y dispara `Changed` |
| `Changed` | Evento que reconstruye el menú sin reiniciar |

Antes de cargar la licencia (solo en pruebas) no se restringe nada, como en 011.

## Relaciones

- `ModuleAccess` relaciona `Permission` (Pos.Domain.Users) con `LicensedModule`.
- `Sale.CashShiftId` ya es nulo: las ventas hechas con Turnos bloqueado quedan con `CashShiftId = null`; no hay cambio de esquema en `Sales`.
- Sin llaves foráneas desde o hacia `LicenseSeals`.
