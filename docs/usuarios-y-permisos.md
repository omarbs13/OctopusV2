# Usuarios, inicio de sesión y roles

Guía para soporte técnico. La especificación completa está en
[specs/007-users-roles-auth/spec.md](../specs/007-users-roles-auth/spec.md).

## Primer arranque

En una instalación nueva (o una base migrada sin usuarios reales) la aplicación muestra, tras la
pantalla de carga, el asistente **Crear administrador**: nombre completo, usuario y contraseña (mínimo
8 caracteres, con confirmación). No se puede omitir ni entrar a la aplicación sin completarlo. No
existe ningún usuario ni contraseña por defecto.

## Roles y permisos

Los permisos se asignan a los roles en un solo lugar: `src/Pos.Domain/Users/RolePermissions.cs`.

| Permiso | Administrador | Cajero |
|---|:-:|:-:|
| Punto de venta y cobro (`Sell`) | ✔ | ✔ |
| Consultar y reimprimir sus propias ventas (`ViewOwnSales`) | ✔ | ✔ |
| Ver todas las ventas, filtro por cajero y totales de Inicio (`ViewAllSales`) | ✔ | |
| Consultar productos y existencias (`ViewProducts`, `ViewInventory`) | ✔ | ✔ |
| Crear, editar y borrar productos (`ManageProducts`) | ✔ | |
| Registrar movimientos de inventario (`RegisterMovements`) | ✔ | |
| Cancelar ventas (`CancelSales`) | ✔ | con autorización |
| Abrir el cajón sin venta (`OpenDrawerWithoutSale`) | ✔ | con autorización |
| Administrar usuarios (`ManageUsers`) | ✔ | |
| Consultar y exportar la bitácora (`ViewAuditLog`) | ✔ | |
| Configuración: negocio, impresora, seguridad (`ManageSettings`) | ✔ | |
| Exportar el diagnóstico (`ExportDiagnostics`) | ✔ | |
| Reportes de ventas y arqueo, alertas de Inicio (`ViewReports`) | ✔ | |
| Mi turno: ver y exportar el resumen del propio turno (`OperateShift`) | ✔ | ✔ |
| Clientes: alta, edición de datos y consulta (`ManageCustomers`) | ✔ | ✔ |
| Vender a crédito (`SellOnCredit`) | ✔ | ✔ |
| Registrar y reimprimir abonos (`RegisterCustomerPayments`) | ✔ | ✔ |
| Límite y modalidad de crédito, desactivar clientes, plazo de pago (`ManageCustomerCredit`) | ✔ | |
| Vender a crédito sobre el límite (`ApproveCreditOverLimit`) | ✔ | con autorización |
| Anular abonos (`VoidCustomerPayments`) | con su contraseña | con autorización |
| Reportes > Créditos (`ViewReceivables`) | ✔ | |
| Aplicar descuentos y cupones en la venta (`ApplyDiscounts`) | ✔ | ✔ |
| Descuentos sobre el límite (`ApproveDiscounts`) | ✔ (sin contraseña) | con autorización |
| Cupones y límite de descuento (`ManageDiscounts`) | ✔ | |
| Descuentos > Reporte (`ViewDiscountReport`) | ✔ | |
| Generar un Corte X del turno abierto (`GenerateShiftReadout`) | ✔ | con autorización |
| Proveedores: alta, edición, desactivación y consulta (`ManageSuppliers`) | ✔ | |
| Registrar compras: Inventario > Entrada de mercancía (`RegisterPurchases`) | ✔ | |
| Anular compras (`VoidPurchases`) | ✔ | |
| Reportes > Compras y detalle de una compra (`ViewPurchaseReport`) | ✔ | |

Los 7 permisos de clientes y crédito (0.9.0) pertenecen al módulo **Crédito y clientes** de la licencia:
sin él no aparecen en el menú y los casos de uso devuelven "módulo no activo". La anulación de abonos
exige la autorización siempre, también al Administrador. Ver [clientes-y-credito.md](clientes-y-credito.md).

Los 4 permisos de descuentos (0.10.0) pertenecen al módulo **Descuentos y promociones**. Un descuento que
supera el límite queda con aprobación guardada y autorizador registrado, también cuando lo aplica el
Administrador. Ver [descuentos.md](descuentos.md).

El permiso del Corte X (0.12.0) pertenece al módulo **Turnos y arqueo**, igual que `OperateShift`,
`WithdrawCash` y `ManageShifts`. Es autorizable para que el Cajero pueda hacer un Corte X con un
Administrador presente sin abrirle otras operaciones de "Turnos"; el corte y la bitácora guardan quién
autorizó. El histórico de cortes usa `ManageShifts` (solo Administrador). Ver
[turnos-de-caja.md](turnos-de-caja.md).

Los 4 permisos de proveedores y compras (0.14.0) pertenecen al módulo **Inventario**, igual que
`ViewInventory` y `RegisterMovements`. Son solo del Administrador y **ninguno es autorizable**: un Cajero
no puede registrar ni anular una compra aunque un Administrador capture su contraseña. `RegisterPurchases`
es independiente de `RegisterMovements`. Ver [compras.md](compras.md).

Los permisos se verifican en los casos de uso, releyendo al usuario de la base en cada operación
restringida: un usuario desactivado o con otro rol pierde el acceso aunque su sesión siga abierta. El
menú solo oculta lo que se rechazaría.

### Autorización de administrador

Ante una operación restringida autorizable (cancelar una venta, abrir el cajón sin venta, generar un
Corte X), el cajero
puede pedir la autorización de un administrador: este captura su usuario y contraseña en un diálogo,
sin cerrar la sesión del cajero. La concesión es de un solo uso, vence a los 2 minutos y solo vale para
esa operación y ese solicitante. La bitácora registra al solicitante (`CreatedBy`) y al autorizador
(`AuthorizedBy`).

## Administración de usuarios

**Administración → Usuarios** (solo Administrador): listado con búsqueda, filtro de inactivos y
paginación de 100; alta y edición (nombre completo, usuario, rol y estado); restablecer la contraseña de
otro usuario. Los usuarios no se borran: se desactivan y conservan su historial.

- El nombre de usuario es único sin distinguir mayúsculas ni acentos ("Maria" = "MARIA"); de 3 a 40
  caracteres (letras, dígitos, `.`, `_` y `-`).
- La contraseña inicial es temporal: el usuario debe cambiarla en su primer inicio de sesión.
- Nunca puede quedar el sistema sin un administrador activo, y nadie puede desactivarse ni quitarse el
  rol a sí mismo.
- Al desactivar a un usuario se descarta la venta que dejó guardada y queda en la bitácora
  (`HELD_SALE_DISCARDED`).
- El usuario "Sistema" (dueño de los registros anteriores) no inicia sesión ni aparece en las pantallas.

## Bloqueo por intentos fallidos y cómo desbloquear

Tras **5 intentos fallidos consecutivos** el usuario queda bloqueado **5 minutos**, también entre
reinicios de la aplicación (`Users.FailedLoginCount` y `Users.LockoutEndsAt`). Cuentan los intentos del
inicio de sesión, de la autorización de administrador y del desbloqueo por inactividad. Un acceso correcto
reinicia el contador. Para desbloquear a un usuario:

1. Esperar 5 minutos; o
2. Que un administrador **restablezca su contraseña** (limpia también el bloqueo).

Si el bloqueado es el único administrador, espera los 5 minutos: el bloqueo vence solo.

## Contraseñas

Se guardan solo como hash PBKDF2-SHA256 (600 000 iteraciones, sal aleatoria de 16 bytes) con el formato
`pbkdf2-sha256$<iteraciones>$<sal>$<hash>`. Nunca aparecen en claro en la base, en la bitácora ni en los
logs. Si el costo vigente aumenta, el hash se regenera tras un inicio de sesión correcto. No hay
recuperación por correo: un administrador restablece la contraseña.

## Bloqueo por inactividad

Tras 15 minutos sin actividad (configurable) la sesión se bloquea con una capa opaca que pide la
contraseña del mismo usuario; se conservan la pantalla y la venta en curso. Se configura en
**Configuración → Seguridad** (de 1 a 240 minutos; desactivado guarda 0) y se guarda en
`preferences/security.json` de cada computadora. Los diálogos abiertos se ocultan al bloquear y se
restauran al desbloquear. "Cambiar de usuario" en la capa de bloqueo sigue el mismo flujo que en el menú.

## Venta en curso y cambio de usuario

El borrador de la venta es **uno por usuario** y está en la base de datos: sobrevive al cierre o
reinicio de la aplicación. Al cerrar sesión o cambiar de usuario con una venta en curso se pide
confirmación; al volver a entrar, ese mismo usuario puede recuperarla (otro usuario no la ve).

## Bitácora de auditoría

**Administración → Bitácora** (solo Administrador, solo lectura): filtros por rango de fechas, usuario
involucrado (autor, autorizador o afectado), tipo de evento y entidad; más reciente primero, 100 por página.
Desde 0.13.0 muestra el antes y después de cada cambio, el historial de un registro y exporta a PDF o
Excel. Consultar y exportar son exclusivos del Administrador (`ViewAuditLog`); un Cajero no ve la pantalla
y los casos de uso `SearchAuditLog`, `ExportAuditLog` y `ConfirmAuditExport` lo rechazan. Ver
[auditoria.md](auditoria.md).

| Evento (`Action`) | Cuándo |
|---|---|
| `LOGIN_SUCCEEDED`, `LOGIN_FAILED`, `USER_LOCKED_OUT`, `LOGOUT` | Acceso, fallos (incluidos nombres inexistentes y desbloqueos), bloqueos y cierres de sesión |
| `USER_CREATED`, `USER_UPDATED`, `USER_DEACTIVATED`, `USER_ACTIVATED` | Alta y cambios de usuarios (también el primer administrador) |
| `PASSWORD_RESET`, `PASSWORD_CHANGED` | Restablecimiento por un administrador y cambio propio |
| `ADMIN_AUTHORIZATION_GRANTED`, `ADMIN_AUTHORIZATION_DENIED` | Autorizaciones de administrador |
| `HELD_SALE_DISCARDED` | Descarte de la venta conservada al desactivar un usuario |
| `SALE_CANCELLED`, `DRAWER_OPENED` | Operaciones sensibles, con `AuthorizedBy` si hubo autorización |

Los eventos de acceso previos a la sesión se registran a nombre de "Sistema". Ninguna entrada contiene
contraseñas, ni siquiera las erróneas.

## Diagnóstico rápido

- Log `Operación rechazada por permisos. UserId=… Permiso=…`: un usuario intentó algo que su rol no permite.
- Log `Contraseña incorrecta. UserId=… Bloqueado=…` y `Acceso rechazado para un usuario inexistente.`.
- Para revisar un respaldo: `SELECT UserName, Role, IsActive, FailedLoginCount, LockoutEndsAt FROM Users;`
  (nunca sobre la base abierta).
