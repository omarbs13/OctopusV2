# Research: Usuarios, inicio de sesión y roles

**Funcionalidad**: `007-users-roles-auth` | **Fecha**: 2026-09-30 | **Plan**: [plan.md](plan.md)

El contexto técnico no tenía incógnitas abiertas (mismo stack de 001 a 006). Esta investigación
resuelve las decisiones de diseño que la especificación deja abiertas y las integra con el código
existente.

---

## §1. Almacenamiento de contraseñas

**Decisión**: PBKDF2-HMAC-SHA256 con `Rfc2898DeriveBytes.Pbkdf2` del runtime:

- Sal aleatoria de 16 bytes y hash de 32 bytes.
- 600 000 iteraciones.
- Se guarda como texto con versión: `pbkdf2-sha256$600000$<sal base64>$<hash base64>`.
- La verificación usa `CryptographicOperations.FixedTimeEquals`.
- Si el número de iteraciones guardado es menor que el vigente, el hash se regenera tras un inicio
  de sesión correcto.

El puerto `IPasswordHasher` vive en Application y su implementación, en `Infrastructure/Security`.

**Motivo**:

- El Principio IX nombra PBKDF2 o Argon2. PBKDF2 viene en .NET, así que no se agrega ninguna
  dependencia (Principio VII).
- 600 000 iteraciones es la recomendación vigente de OWASP para SHA-256. Cuesta unos 0.3 a 0.6 s
  en un equipo de caja modesto, compatible con SC-007 (15 s hasta el punto de venta).
- El prefijo con algoritmo e iteraciones permite endurecer el costo después sin migrar datos.
- El puerto permite usar un hasher rápido en las pruebas de Application.

**Alternativas consideradas**:

- Argon2id (`Konscious.Security.Cryptography`): es más resistente a GPU, pero agrega una
  dependencia y no aporta una mejora práctica para una base local.
- BCrypt.Net: también es una dependencia nueva y trunca las contraseñas a 72 bytes.
- `PasswordHasher<T>` de ASP.NET Core Identity: arrastra `Microsoft.Extensions.Identity.Core`
  para una sola función.

---

## §2. Usuario "Sistema" y registros previos

**Decisión**:

- La migración crea la tabla `Users` y siembra con `HasData` al usuario "Sistema" con el id fijo
  que ya existe: `SystemUser.Id = 00000000-0000-7000-8000-000000000001`.
- Sus propiedades: `IsSystem = true`, `IsActive = false`, sin hash de contraseña y
  `NormalizedUserName = "SISTEMA"`.
- Todos los registros previos ya tienen ese id en `CreatedBy`, `UpdatedBy` y `CancelledBy`, así que
  **no hay que actualizar datos**: con sembrar la fila, los registros quedan asociados (FR-021,
  SC-004).
- "Sistema" nunca aparece en la administración de usuarios, en el filtro de cajero ni en el
  inicio de sesión (FR-006).

**Motivo**: el Principio IV permite `HasData` para datos fijos, iguales en toda instalación, y
"Sistema" lo es. No es un dato propio de la instalación, que sí iría al asistente de primer
arranque. Reutilizar el id existente evita un `UPDATE` masivo sobre ventas y movimientos.

**Alternativas consideradas**:

- Crear "Sistema" en el arranque, fuera de la migración: dejaría una ventana en la que los
  registros apuntan a un usuario inexistente.
- Reescribir `CreatedBy` hacia un id nuevo: implica un `UPDATE` sobre tablas grandes y el riesgo
  de romper la integridad.

---

## §3. Llaves foráneas de los campos de auditoría

**Decisión**:

- `CreatedBy`, `UpdatedBy`, `CancelledBy` y `AuditEntries.CreatedBy` siguen siendo referencias
  lógicas, **sin** llave foránea hacia `Users`.
- Los nombres se resuelven con un `LEFT JOIN` a `Users` en las consultas de lectura. Si no se
  encuentra el usuario, se muestran los primeros 8 caracteres del id, como hace hoy
  `SystemUser.NameOf`.

**Motivo**: en SQLite, agregar una llave foránea obliga a reconstruir la tabla. Hacerlo en
`Products`, `InventoryMovements`, `Sales`, `SaleLines` y `AuditEntries` es el tipo de migración
riesgosa que el Principio IV pide evitar. Los usuarios nunca se borran físicamente (FR-017), así
que la referencia nunca queda huérfana.

**Alternativas consideradas**: llaves foráneas completas (se rechazan por las cinco
reconstrucciones de tabla).

---

## §4. Sesión y punto único del usuario conectado

**Decisión**:

- `Pos.Application/Users/Session/UserSession` es un singleton en memoria que implementa el
  `ICurrentUser` existente (Principio IV y FR-019). Conserva:
  - `UserId`.
  - Una instantánea (`SessionUser`) con nombre completo, usuario y rol.
- Sin sesión, `UserId` devuelve `SystemUser.Id`. Así el arranque, las migraciones y los intentos
  de acceso previos al inicio de sesión quedan a nombre de "Sistema".
- `SystemCurrentUser` se elimina y el registro de DI cambia a `UserSession`.
- `AuditingInterceptor` y los casos de uso **no cambian** (Historia 6, escenario 5).

**Motivo**: la aplicación es de escritorio y tiene un solo operador a la vez por proceso. Un
singleton es el modelo correcto. Vivir en Application evita que Desktop o Infrastructure dupliquen
la regla de "Sistema cuando no hay sesión".

**Alternativas consideradas**:

- `Thread.CurrentPrincipal` o `ClaimsPrincipal`: agregan indirección sin beneficio fuera de
  ASP.NET.
- Guardar la sesión en Desktop: obligaría a Infrastructure a depender de un puerto implementado
  en la capa de UI.

---

## §5. Modelo de permisos (FR-010 a FR-012)

**Decisión**:

- `Pos.Domain/Users/Permission.cs` es un `enum` de capacidades puntuales.
- `Pos.Domain/Users/RolePermissions.cs` es la **única** tabla rol → permisos. Administrador
  tiene todos los permisos; Cajero, este conjunto exacto:

  | Permiso | Cajero |
  |---|---|
  | `Sell` (punto de venta y cobro) | ✔ |
  | `ViewOwnSales` (consultar y reimprimir las propias) | ✔ |
  | `ViewProducts`, `ViewInventory` (solo lectura) | ✔ |
  | `ViewAllSales` (todas las ventas, filtro por cajero, tarjetas de ventas del Inicio) | ✘ |
  | `ManageProducts` | ✘ |
  | `RegisterMovements` | ✘ |
  | `CancelSales` (se puede autorizar) | ✘ |
  | `OpenDrawerWithoutSale` (se puede autorizar) | ✘ |
  | `ManageUsers` | ✘ |
  | `ViewAuditLog` | ✘ |
  | `ManageSettings` (negocio, impresora, seguridad) | ✘ |
  | `ExportDiagnostics` | ✘ |

- Solo `CancelSales` y `OpenDrawerWithoutSale` son autorizables (FR-013).
- Inicio, cambiar la contraseña propia y "Acerca de" no requieren permiso.
- La exportación de diagnóstico incluye una copia completa de la base, con hashes y todas las
  ventas. Por eso queda solo para Administrador, en línea con la lista cerrada de la Historia 4.
- Las tarjetas de ventas del Inicio muestran totales de todo el negocio, así que requieren
  `ViewAllSales`. Las de inventario requieren `ViewInventory`.

**Motivo**: una tabla en el dominio se prueba sola y la usan tanto los casos de uso como el
menú. Agregar un rol o un permiso cambia un solo archivo (FR-012, Historia 4, escenario 3).

**Alternativas consideradas**:

- Permisos editables en la base: quedan fuera de alcance según los supuestos de la
  especificación.
- Atributos `[RequiresPermission]` con un decorador: sin MediatR (Principio VII) no hay una
  tubería donde aplicarlos, y una verificación explícita en el handler es más legible.

---

## §6. Verificación de permisos en los casos de uso (FR-011)

**Decisión**:

- `IAccessControl` (Application) expone
  `Task<AccessDecision> CheckAsync(Permission, Guid? grantId, CancellationToken)`.
- La implementación `AccessControl` **relee al usuario de la base** (una consulta por llave
  primaria) en cada verificación:
  - Si el usuario está inactivo o no existe, rechaza.
  - Si su rol tiene el permiso, permite.
  - Si trae un `grantId` válido para ese permiso, permite y devuelve el id del autorizador.
- Los handlers restringidos verifican el permiso **antes** de validar la entrada o de abrir la
  transacción, y responden con el error nuevo `Forbidden(Permission, bool CanBeAuthorized)` sin
  ningún efecto.
- Las consultas de ventas aplican además una regla de propiedad (§7).

**Motivo**: releer al usuario cubre el caso límite "un usuario desactivado con la sesión abierta
no puede iniciar operaciones restringidas", y también un cambio de rol durante la sesión. El costo
es de una lectura por operación restringida, insignificante en SQLite local.

**Alternativas consideradas**: confiar en el rol guardado en la sesión. Se rechaza porque no
detecta la desactivación.

---

## §7. Ventas propias del cajero (FR-026)

**Decisión**:

- **`SearchSales`**:
  - Sin `ViewAllSales`, el filtro `CashierId` se fuerza al usuario actual.
  - Si un cajero pide un `CashierId` distinto al suyo, recibe `Forbidden`.
  - Con `ViewAllSales`, el filtro es opcional.
- **`GetSale` y `PrintTicket` de una venta**: sin `ViewAllSales`, si `Sale.CreatedBy` no es el
  usuario actual, la respuesta es `Forbidden`. La impresión automática tras cobrar es siempre
  sobre la venta propia.
- Se agrega el índice `IX_Sales_CreatedBy_CreatedAt` para el filtro por cajero. Crear un índice no
  reconstruye la tabla.

**Motivo**: `Sale.CreatedBy` ya es el cajero, así que no hace falta ninguna columna nueva.

---

## §8. Autorización de administrador (Historia 7, FR-013 y FR-014)

**Decisión**: se usa una **concesión de un solo uso**, en tres pasos.

1. El cajero intenta, por ejemplo, cancelar. El handler responde
   `Forbidden(CancelSales, CanBeAuthorized: true)` y la interfaz ofrece "Solicitar autorización".
2. `AuthorizeAdminHandler(permission, userName, password)`:
   - Aplica las mismas reglas de acceso que el inicio de sesión: bloqueo, contador compartido
     (FR-005), usuario inactivo y "Sistema".
   - Verifica que el usuario sea Administrador.
   - Escribe `ADMIN_AUTHORIZATION_GRANTED` o `ADMIN_AUTHORIZATION_DENIED` en la bitácora.
   - Si concede, crea en `AuthorizationGrants` (singleton en memoria) una concesión con id,
     permiso, solicitante y administrador. Vence en 2 minutos.
3. La interfaz repite el comando con `AuthorizationGrantId`. `AccessControl` consume la
   concesión solo si coinciden el permiso y el solicitante y no ha vencido.
   - El handler registra la operación como siempre: `CreatedBy` o `CancelledBy` es el cajero.
   - Además, la entrada de la bitácora lleva `AuthorizedBy` con el id del administrador (FR-014,
     SC-006).

La sesión del cajero no cambia en ningún momento.

Si el usuario conectado ya tiene el permiso (un administrador), el paso 1 no rechaza y no se pide
nada (caso límite).

**Motivo**: la contraseña del administrador no viaja dentro de comandos de negocio, la
verificación de credenciales queda en un solo lugar y la concesión no se puede reutilizar para
otra operación.

**Alternativas consideradas**:

- Pasar las credenciales dentro de `CancelSaleCommand`: mezcla autenticación con negocio y
  obliga a duplicarla en cada comando autorizable.
- Cambiar temporalmente el `ICurrentUser`: rompería "sin cambiar la sesión del cajero" y dejaría
  al administrador como autor de la venta cancelada.

---

## §9. Bloqueo por intentos fallidos (FR-005)

**Decisión**:

- En `User` se agregan `FailedLoginCount` y `LockoutEndsAt` (UTC), persistidos en la base, así el
  bloqueo sobrevive a un reinicio.
- La regla vive en el dominio (`User.RegisterFailedLogin(now)` y `User.IsLockedOut(now)`):
  - Al quinto fallo consecutivo, `LockoutEndsAt = now + 5 min`.
  - Un acceso correcto reinicia el contador.
  - Al vencer el bloqueo, el siguiente intento empieza con el contador reiniciado.
- Estos dos campos se actualizan con `ExecuteUpdateAsync`, sin pasar por el interceptor. Así un
  intento fallido no incrementa `Version` ni cambia `UpdatedBy` y no provoca un conflicto de
  concurrencia en el formulario del administrador.
- Orden de evaluación del inicio de sesión:
  1. Buscar por `NormalizedUserName`.
  2. Si no existe o es "Sistema": verificar contra un hash ficticio (mismo tiempo), sumar al
     contador en memoria de ese nombre y devolver el mensaje genérico.
  3. Si está bloqueado: `LockedOut`, sin verificar la contraseña.
  4. Verificar la contraseña. Si falla: `RegisterFailedLogin`, auditar y devolver el mensaje
     genérico. Si con este fallo queda bloqueado, además `USER_LOCKED_OUT`.
  5. Si está inactivo: mensaje genérico.
  6. Éxito: reiniciar el contador, auditar `LOGIN_SUCCEEDED` e indicar si debe cambiar la
     contraseña.
- **Nombres inexistentes**: un contador en memoria por nombre normalizado aplica la misma regla
  de 5 intentos y 5 minutos. Así también responden "bloqueado temporalmente" y no revelan que el
  usuario no existe (caso límite). Ese contador se pierde al reiniciar, lo cual es aceptable
  porque no protege a ninguna cuenta real.

**Motivo**: la regla con fecha y contador es un cálculo de negocio que se prueba en el dominio.
`ExecuteUpdate` evita conflictos de versión espurios.

---

## §10. Nombre de usuario único sin distinguir mayúsculas (FR-003)

**Decisión**:

- `UserName` se guarda tal como se capturó, sin espacios al inicio ni al final.
- `NormalizedUserName` es `UserName.ToUpperInvariant()` y tiene un índice único.
- Reglas: de 3 a 40 caracteres, solo letras, dígitos, `.`, `_` y `-`, sin espacios.
- El índice único resuelve las altas simultáneas (caso límite): el perdedor recibe
  `Duplicate(UserName)`.
- El asistente de primer administrador comprueba "no hay usuarios además de Sistema" **dentro**
  de la transacción de escritura (`BEGIN IMMEDIATE`). Si el asistente se ejecuta dos veces, el
  segundo recibe `InvalidState` y pasa al inicio de sesión.

**Alternativas consideradas**: la intercalación `NOCASE` de SQLite, que solo compara ASCII:
"MARÍA" y "maría" no coincidirían.

---

## §11. Venta en curso conservada por usuario (Historia 8, FR-022)

**Decisión**:

- El borrador durable de 005 (`SaleDrafts`, hoy una sola fila con `Slot = 1`) pasa a ser **una
  fila por usuario**, con llave primaria `UserId`.
- El punto de venta ya guarda el borrador tras cada cambio. Ahora lo guarda bajo el usuario
  conectado, así que la "venta conservada" es simplemente el borrador del usuario que cerró
  sesión.
- Al volver a entrar, el punto de venta ofrece recuperarlo con el mecanismo actual de
  recuperación. Otro usuario no lo ve porque cada uno lee su propia fila.
- Queda como máximo una venta conservada por usuario gracias a la llave primaria.
- Sobrevive a un reinicio porque está en SQLite.
- Al recuperarla vuelve a ser la venta en curso, que es la misma fila.
- **Al desactivar un usuario**, `UpdateUserHandler` borra su fila de `SaleDrafts` y escribe
  `HELD_SALE_DISCARDED` en la misma transacción.
- **Migración**: la llave primaria cambia, así que EF Core reconstruye `SaleDrafts`. El borrador
  existente, si lo hay, se copia con `UserId = Sistema`. Al crear el primer administrador en el
  asistente, un borrador de "Sistema" se reasigna a ese administrador, así no se pierde la venta
  que estaba en curso al actualizar.

**Motivo**: reutiliza el mecanismo probado de 005 (guardado sin retraso, idempotencia por
`DraftId`, recuperación) en lugar de crear una segunda estructura de "ventas en espera".

**Alternativas consideradas**:

- Una tabla nueva `HeldSales` aparte del borrador: habría dos fuentes de verdad para la misma
  venta.
- Conservar la venta solo en memoria: la aclaración de la especificación exige que sobreviva al
  reinicio.

---

## §12. Sesión de interfaz y ámbito de dependencias en Desktop

**Decisión**:

- La ventana principal hospeda un `RootViewModel` que alterna entre:
  - Asistente de primer administrador.
  - Inicio de sesión.
  - Cambio de contraseña obligatorio.
  - Shell de la sesión (la actual `MainViewModel`).
  - Capa de bloqueo por inactividad, sobre el shell.
- Cada inicio de sesión crea un **ámbito de DI de sesión** (`SessionScope`). Todo lo que
  pertenece a una sesión se registra como *scoped* y se resuelve desde ese ámbito:
  - `MainViewModel`, `Navigator`, `NavigationRegistry` y `MenuViewModel`.
  - Las pantallas (`AddPage`) y las tarjetas de Inicio.
  - `SalesDashboardSource`.
- Al cerrar sesión o cambiar de usuario, el ámbito se desecha. La siguiente sesión empieza con
  pantallas y filtros limpios, y con el menú construido para su rol.
- `NavigationEntry`, `NavigationGroup` y las tarjetas declaran un `Permission?` opcional.
  `NavigationRegistry` filtra por el rol de la sesión y los grupos vacíos ya se omiten (FR-008).
  `Navigator` no puede navegar a una opción filtrada porque `FindEntry` devuelve nulo.
- Los servicios sin estado de sesión siguen siendo singletons: `UseCases`, `OperationRunner`,
  `DialogService`, `TicketPrintingService` y la cola de impresión.
- `UseCases` sigue creando un ámbito por operación desde el `IServiceScopeFactory` raíz.

**Motivo**: hoy las pantallas son singletons que conservan estado (carrito, filtros, historial).
Sin un ámbito por sesión, un cajero vería los filtros o el carrito del usuario anterior.
Desechar el ámbito da esa garantía por construcción, sin que cada pantalla tenga que "limpiarse".

**Alternativas consideradas**:

- Una interfaz `ISessionAware.Reset()` en cada pantalla: es fácil olvidarla en pantallas futuras.
- Cerrar y reabrir ventanas distintas para el inicio de sesión: complica el `ShutdownMode` y el
  respaldo al cerrar, que hoy dependen de una sola ventana principal.

---

## §13. Bloqueo por inactividad (Historia 9, FR-023)

**Decisión**:

- `IdleMonitor` (Desktop) escucha los eventos de teclado y puntero de la ventana principal con un
  manejador de túnel y un `DispatcherTimer`.
- Al cumplirse el tiempo, `RootViewModel` muestra una capa que cubre el contenido **sin
  desechar** el ámbito de sesión: la pantalla y la venta en curso quedan intactas.
- Para desbloquear se pide la contraseña del mismo usuario con `VerifySessionPassword`:
  - Los fallos cuentan para el bloqueo de FR-005.
  - La capa ofrece "Cambiar de usuario", que sigue el flujo de la Historia 8.
- La actividad dentro de los diálogos modales (el cobro, los formularios) también cuenta como
  actividad. `DialogService` registra cada diálogo en el monitor.
- Si el tiempo se cumple con un diálogo abierto, `DialogService` oculta el diálogo sin cerrarlo y
  lo vuelve a mostrar al desbloquear. Así nadie puede terminar un cobro a nombre del cajero y no
  se pierde nada de lo capturado.
- La configuración `SecuritySettings(IdleLockMinutes)` se guarda con el `IPreferencesStore`
  existente (`preferences/security.json`), igual que la impresión. El valor por defecto es 15 y
  0 significa desactivado.
- Se edita en la pantalla nueva **Configuración › Seguridad** (`ManageSettings`).

**Motivo**: es una preferencia de la máquina, como la impresora, así que no necesita migración.

**Alternativas consideradas**: guardarla en una tabla `Settings` en SQLite. Sería la primera
tabla de configuración general, y su valor solo aparecería si hubiera más ajustes de
instalación.

---

## §14. Bitácora de auditoría: eventos nuevos y consulta (FR-024 y FR-027)

**Decisión**:

- `AuditEntry` gana `AuthorizedBy Guid?`. En SQLite es un `ADD COLUMN` nulo, sin reconstruir la
  tabla.
- `IAuditLog.Add` recibe `authorizedBy` como parámetro opcional.
- Eventos nuevos, que siguen la convención actual de `Action` y `EntityType`:

  | Action | EntityType / EntityId | CreatedBy |
  |---|---|---|
  | `LOGIN_SUCCEEDED` | `User` / usuario | Sistema (aún sin sesión) |
  | `LOGIN_FAILED` | `User` / usuario, o `Guid.Empty` si el nombre no existe (el nombre capturado va en `Details`) | Sistema |
  | `USER_LOCKED_OUT` | `User` / usuario | Sistema |
  | `LOGOUT` | `User` / usuario | usuario |
  | `USER_CREATED`, `USER_UPDATED`, `USER_DEACTIVATED`, `USER_ACTIVATED` | `User` / usuario afectado | administrador |
  | `PASSWORD_RESET` | `User` / usuario afectado | administrador |
  | `PASSWORD_CHANGED` | `User` / usuario | usuario |
  | `ADMIN_AUTHORIZATION_GRANTED` / `_DENIED` | `User` / administrador capturado (o `Guid.Empty`) | cajero; `AuthorizedBy` = administrador si se concede |
  | `HELD_SALE_DISCARDED` | `User` / usuario desactivado | administrador |
  | `SALE_CANCELLED`, `DRAWER_OPENED` (existentes) | sin cambio | usuario; + `AuthorizedBy` si hubo autorización |

- La contraseña nunca va en `Details`, ni siquiera la errónea.
- El catálogo de acciones se centraliza en `AuditActions` (Application) para el filtro por tipo
  de evento. Los handlers existentes pasan a usar esas constantes.
- **Consulta** (`SearchAuditLogHandler`, requiere `ViewAuditLog`):
  - Filtros: rango de fechas, tipo de evento y "usuario involucrado". Un usuario está involucrado
    si es `CreatedBy`, si es `AuthorizedBy` o si la entrada es de `EntityType = 'User'` con
    `EntityId` igual a él.
  - Orden por `CreatedAt DESC` y páginas de 100.
  - Índices nuevos: `IX_AuditEntries_CreatedAt` y `IX_AuditEntries_CreatedBy`.
- La inmutabilidad ya la garantiza `PosDbContext.RejectMovementChanges`. La pantalla es de solo
  lectura.

---

## §15. Cambio de contraseña obligatorio, restablecimiento y protecciones del administrador

**Decisión**:

- `MustChangePassword = true` al crear un usuario (supuesto de la especificación) y al
  restablecer su contraseña.
- Después de un inicio de sesión correcto con esa marca, `RootViewModel` muestra el cambio
  obligatorio. No se crea el ámbito de sesión hasta completarlo, así que interrumpirlo no da
  acceso.
- `ChangeOwnPassword` exige la contraseña actual, excepto en el cambio obligatorio, donde se
  acaba de verificar. La nueva debe tener al menos 8 caracteres y ser distinta de la actual.
- `ResetUserPassword` solo aplica a **otro** usuario. Para sí mismo se usa el cambio de
  contraseña propia.
- Las protecciones de FR-018 viven en `UpdateUserHandler`, dentro de la transacción
  (`BEGIN IMMEDIATE`):
  - Un administrador no puede cambiarse su propio rol ni desactivarse.
  - No se puede quitar el rol ni desactivar al último administrador activo. La cuenta
    `COUNT(Role=Admin AND IsActive AND NOT IsSystem)` se hace dentro de la transacción, así dos
    administradores que se desactivan mutuamente al mismo tiempo no dejan el sistema sin
    administrador.

---

## §16. Pruebas (política mínima del Principio VI)

| Capa | Qué se prueba | Por qué entra en la política |
|---|---|---|
| Domain | `User`: normalización y reglas del nombre de usuario; regla de bloqueo (5 fallos → 5 min, reinicio tras éxito y tras vencer) | validación de integridad y regla con cálculo de tiempo |
| Domain | `RolePermissions`: el Cajero tiene exactamente el conjunto de la Historia 4 | protege la integridad del control de acceso (SC-002) |
| Application | Cada handler restringido invocado con un Cajero devuelve `Forbidden` sin efectos, en una prueba parametrizada (SC-002) | validación que protege los datos |
| Application | `SignIn`: mensaje genérico (inexistente, erróneo, inactivo, Sistema), bloqueo y reinicio del contador | validación de integridad |
| Application | `UpdateUser`: último administrador y autoprotección; desactivar descarta la venta conservada | validación de integridad |
| Application | `AuthorizeAdmin`: concesión de un solo uso, ligada al permiso y al solicitante; los fallos cuentan para el bloqueo | validación de integridad |
| Application | `SearchSales`, `GetSale` y `PrintTicket`: el cajero solo accede a lo propio | validación de integridad |
| Infrastructure (SQLite real) | Migración de todas las bases de ejemplo más una nueva `v0.5.0.db` con borrador: Sistema existe, registros intactos, borrador bajo Sistema, `foreign_key_check` limpio | obligatoria |
| Infrastructure | Índice único de usuario sin distinguir mayúsculas con dos altas simultáneas; asistente ejecutado dos veces; bloqueo persistente entre contextos | integridad y concurrencia |
| Infrastructure | `Pbkdf2PasswordHasher`: verifica, rechaza y no contiene la contraseña en claro (SC-008) | validación de integridad |
| Arquitectura | Sin cambios; las reglas existentes cubren las carpetas nuevas | obligatoria |

Sin pruebas de ViewModels, vistas, `IdleMonitor` ni del filtrado del menú. Estas se verifican a
mano con [quickstart.md](quickstart.md).
