# Quickstart: validar Usuarios, inicio de sesión y roles

Guía de validación de extremo a extremo. Las reglas y firmas están en
[contracts/application-ports.md](contracts/application-ports.md), el comportamiento de pantallas
en [contracts/ui.md](contracts/ui.md) y el esquema en [data-model.md](data-model.md).

## Requisitos

- SDK de .NET 10 (`global.json`) y `dotnet tool restore` (para `dotnet ef`).
- Para la migración, una copia de una base de una versión previa. La más cómoda es
  `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.5.0.db`.
- La carpeta de datos está descrita en [docs/carpeta-de-datos.md](../../docs/carpeta-de-datos.md).
  Para empezar desde cero, basta con borrar o mover `pos.db` de esa carpeta.

## 1. Compilar y probar

```bash
dotnet build -v q                     # 0 errores, 0 advertencias
dotnet test --verbosity quiet         # suite completa (en CI; localmente, el proyecto modificado)
```

Pruebas que deben existir y pasar (research §16):

- `Pos.Domain.Tests/Users/`: `UserNameRulesTests`, `UserLockoutTests` y `RolePermissionsTests`.
- `Pos.Application.Tests/Users/`: `SignInHandlerTests`, `UpdateUserHandlerTests` (último
  administrador y venta conservada) y `AuthorizeAdminHandlerTests`.
- `Pos.Application.Tests/Security/RestrictedOperationsTests`: un cajero es rechazado en cada
  operación restringida (SC-002).
- `Pos.Application.Tests/Sales/SalesOwnershipTests`.
- `Pos.Infrastructure.Tests/Users/`: `UserPersistenceTests` (índice único sin distinguir
  mayúsculas, alta simultánea, asistente doble y bloqueo persistente) y
  `Pbkdf2PasswordHasherTests`.
- `Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests`: incluye `v0.5.0.db`.

## 2. Revisar la migración

```bash
dotnet ef migrations script BusinessProfile UsersAndRoles --project src/Pos.Infrastructure -o migracion.sql
```

Verificar en el SQL:

1. `CREATE TABLE "Users"` con `IX_Users_NormalizedUserName` único y el `INSERT` de "Sistema" con
   id `00000000-0000-7000-8000-000000000001`, `IsSystem = 1`, `IsActive = 0` y
   `PasswordHash NULL`.
2. `ALTER TABLE "AuditEntries" ADD "AuthorizedBy"`, sin reconstruir la tabla, más sus dos índices.
3. Reconstrucción de `SaleDrafts`: el `INSERT INTO "ef_temp_SaleDrafts" … SELECT` copia
   `DraftId`, `LinesJson` y `UpdatedAt`, y `UserId` queda con el id de "Sistema". Ya no aparece
   `CK_SaleDrafts_Slot`.
4. `CREATE INDEX "IX_Sales_CreatedBy_CreatedAt"`.
5. Ninguna otra tabla se reconstruye.

## 3. Primer arranque (Historia 1, SC-001)

1. Arrancar sin base: `dotnet run --project src/Pos.Desktop`.
2. Tras la pantalla de carga aparece **solo** "Crear administrador". No hay menú y F9 no hace
   nada.
3. Con contraseña `1234567`, o con una confirmación distinta, se muestra un error por campo y no
   se crea nada.
4. Capturar "Ana Admin" / `ana` / `Secreta123`. Pasa al inicio de sesión con `ana` escrito.
5. Cerrar y volver a abrir: ya no aparece el asistente.

## 4. Inicio de sesión y bloqueo (Historia 2)

1. Entrar como `ANA` con `Secreta123`. Se abre Inicio (el usuario no distingue mayúsculas).
2. Cerrar sesión. Probar `ana` con una contraseña errónea y luego `noexiste`: en ambos casos se
   muestra "Usuario o contraseña incorrectos".
3. Fallar 5 veces con `ana`. Al sexto intento, **aun con la contraseña correcta**, aparece el
   mensaje de bloqueo.
4. Cerrar la aplicación, volver a abrirla y entrar con `ana`: sigue bloqueada. Pasados 5 minutos,
   entra.
5. Fallar 6 veces con `noexiste`: también responde "bloqueado temporalmente".

## 5. Usuarios y roles (Historias 3, 4 y 5)

1. Como `ana`: en Administración › Usuarios, crear "Carlos Caja" / `carlos` / Cajero /
   `Temporal01`.
2. Intentar crear `CARLOS`: se rechaza por duplicado.
3. Cerrar sesión y entrar como `carlos`: pide cambiar la contraseña. Pulsar "Salir" y confirmar
   que no se entró. Volver a entrar y cambiarla a `Carlos2026`.
4. Menú de `carlos`, comparado con [contracts/ui.md §Menú según rol](contracts/ui.md#menú-según-rol-fr-008):
   - Sin Administración ni Configuración.
   - Productos sin Nuevo, Editar ni Eliminar.
   - Movimientos sin "Registrar".
   - Acerca de sin "Exportar diagnóstico".
   - Inicio sin tarjetas de ventas.
5. Contraer el menú: se ven las iniciales "CC" y, con el cursor encima, "Carlos Caja · Cajero".
6. Como `ana`, editarse a sí misma: Rol y Activo están deshabilitados. Crear un segundo
   administrador `beto`, desactivar a `ana` desde `beto` y, desde `beto`, intentar desactivarse
   él mismo: se rechaza.
7. Restablecer la contraseña de `carlos` desde `beto`. `carlos` debe cambiarla en su siguiente
   inicio de sesión.

## 6. Auditoría y ventas por cajero (Historia 6, FR-026)

1. Vender como `carlos` (folio A) y como `beto` (folio B).
2. En Ventas realizadas, como `carlos`: solo aparece A, no hay filtro "Cajero" y el detalle y el
   ticket (impresora virtual) muestran "Cajero: Carlos Caja".
3. Como `beto`: aparecen A y B. Con el filtro Cajero = Carlos Caja, solo A.
4. En Existencias y Movimientos, el movimiento de la venta A muestra "Carlos Caja". Los
   movimientos previos a la migración muestran "Sistema".

## 7. Autorización de administrador (Historia 7)

1. Como `carlos`, abrir el detalle de A y pulsar "Cancelar venta" con un motivo. Se ofrece
   "Solicitar autorización".
2. Capturar credenciales de `carlos`: se rechaza porque no es administrador.
3. Capturar `beto` con su contraseña: la venta queda cancelada y la sesión sigue siendo la de
   `carlos`.
4. Como `beto`, en Bitácora: aparecen `ADMIN_AUTHORIZATION_DENIED`,
   `ADMIN_AUTHORIZATION_GRANTED` y `SALE_CANCELLED` con Usuario = Carlos Caja y Autorizó = Beto.
5. "Abrir cajón" en el Punto de venta como `carlos`: hace el mismo recorrido y registra
   `DRAWER_OPENED` con ambos usuarios.
6. Capturar 5 contraseñas erróneas de `beto` en autorizaciones: `beto` queda bloqueado también
   para iniciar sesión.

## 8. Venta conservada (Historia 8)

1. Como `carlos`, agregar 2 productos sin cobrar. "Cambiar de usuario" pide confirmación: con
   "No" nada cambia; con "Sí" vuelve al inicio de sesión.
2. Entrar como `beto`: su Punto de venta está vacío.
3. Cerrar la aplicación, abrirla y entrar como `carlos`: se ofrece recuperar la venta con sus 2
   productos.
4. Cambiar a `beto`, desactivar a `carlos` y confirmar el aviso de descarte. En la Bitácora
   aparece `HELD_SALE_DISCARDED`.

## 9. Bloqueo por inactividad (Historia 9)

1. En Configuración › Seguridad, poner 1 minuto.
2. En el Punto de venta, con una línea, esperar 1 minuto. El contenido queda cubierto.
3. Una contraseña errónea no desbloquea. La correcta regresa al Punto de venta con la línea
   intacta.
4. Desactivar el bloqueo y esperar: no se bloquea.

## 10. Migración de una base existente (SC-004)

1. Copiar `v0.5.0.db` como `pos.db` en la carpeta de datos y arrancar.
2. Se crea el respaldo previo, se migra y aparece el asistente de primer administrador, porque la
   base previa no tiene usuarios.
3. Tras crear el administrador:
   - Las ventas y movimientos previos muestran "Sistema".
   - Los totales no cambian.
   - La venta en curso que tuviera la base se ofrece al administrador en el Punto de venta.
   - "Sistema" no aparece en Usuarios y no puede iniciar sesión.
