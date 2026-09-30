# Contrato: interfaz (Pos.Desktop)

Los textos van en `Resources/Strings.resx`, en español. Los ViewModels solo coordinan: toda regla
(contraseña, bloqueo, permisos) viene de los casos de uso ([application-ports.md](application-ports.md)).

## Flujo de la ventana principal

`MainWindow.DataContext` pasa a ser `RootViewModel` (Shell). Su propiedad `Content` alterna entre
estas vistas (research §12):

```text
Pantalla de carga ─► GetSetupState
                       ├─ NeedsFirstAdmin ─► FirstAdminView ─► LoginView
                       └─ si no ──────────────────────────────► LoginView
LoginView ─► SignIn ─┬─ MustChangePassword ─► ChangePasswordView (obligatorio, sin "Cancelar")
                     └─ si no ─────────────────────────────────┐
                                                               ▼
                 StartSession + nuevo ámbito de sesión ─► SessionShell (MainViewModel: menú + pantalla)
SessionShell ─► Cerrar sesión / Cambiar de usuario ─► (confirmación si hay venta en curso)
               ─► EndSession + desechar el ámbito ─► LoginView
SessionShell ─► inactividad ─► LockOverlay (sobre el shell; el ámbito sigue vivo)
```

- Mientras `Content` no sea `SessionShell`, no hay menú ni atajo F9. No hay forma de llegar al
  resto de la aplicación (SC-001).
- Al cerrar la ventana, el comportamiento actual no cambia: primero se confirman los cambios sin
  guardar y después se hace el respaldo. Si hay sesión, se audita `LOGOUT`. La venta en curso ya
  está en el borrador y se ofrecerá en la siguiente sesión de ese usuario.

## Asistente de primer administrador (`FirstAdminView`)

- Título "Crear administrador". Texto: "Es la primera vez que se usa el sistema. Crea la cuenta
  del administrador."
- Campos: Nombre completo, Usuario, Contraseña y Confirmar contraseña. Los errores se muestran por
  campo, igual que en el formulario de productos.
- Botón "Crear y continuar". No hay botón para omitir. Al terminar, pasa a `LoginView` con el
  usuario ya escrito.

## Inicio de sesión (`LoginView`)

- Campos Usuario y Contraseña; Enter en Contraseña ejecuta "Entrar". Al abrir, el foco va a
  Usuario, o a Contraseña si Usuario viene lleno.
- Mensajes:
  - Genérico: "Usuario o contraseña incorrectos".
  - Bloqueo: "Usuario bloqueado temporalmente por intentos fallidos. Intenta de nuevo en N
    minutos."
- Tras un error, se limpia la contraseña y el foco vuelve a ella.

## Cambio de contraseña (`ChangePasswordView`, compartida)

- **Modo obligatorio**, tras `SignIn`:
  - Campos Nueva y Confirmar.
  - Botón "Guardar y continuar".
  - "Salir" regresa a `LoginView` sin abrir la sesión.
- **Modo voluntario**, desde el menú de usuario:
  - Es un diálogo con Contraseña actual, Nueva y Confirmar.
  - "Guardar" y "Cancelar".

## Sección de usuario en el menú (FR-009)

Está en el pie del menú lateral (`MenuView`), sobre el botón de contraer:

- **Menú expandido**: se ven el nombre completo y el rol ("Administrador" o "Cajero").
- **Menú contraído**: se ve un círculo con las iniciales. El tooltip muestra "Nombre completo ·
  Rol".
- Al hacer clic se abre un menú flotante con:
  - Cambiar contraseña.
  - Cambiar de usuario.
  - Cerrar sesión.
- "Cambiar de usuario" y "Cerrar sesión" hacen lo mismo: vuelven a `LoginView`. La diferencia es
  que "Cambiar de usuario" deja el nombre de usuario vacío y enfocado.
- Si el Punto de venta tiene líneas, se pide confirmación antes de salir: "Hay una venta en curso.
  Se guardará y se te ofrecerá al volver a entrar. ¿Continuar?" con "Sí" y "No". Con "No" nada
  cambia.

## Menú según rol (FR-008)

`NavigationEntry`, `NavigationGroup` (implícito por sus hijos) y `DashboardCard` declaran un
`Permission?` opcional. `AddPage(..., permission: …)` lo recibe. `NavigationRegistry` (scoped)
filtra con el rol de la sesión.

| Opción | Permiso | Cajero |
|---|---|---|
| Inicio | — | ✔ |
| Ventas › Punto de venta (F9) | `Sell` | ✔ |
| Ventas › Ventas realizadas | `ViewOwnSales` | ✔ (solo las propias, sin filtro por cajero) |
| Catálogos › Productos | `ViewProducts` | ✔ en solo lectura: sin Nuevo, Editar ni Eliminar |
| Inventario › Existencias | `ViewInventory` | ✔ |
| Inventario › Movimientos | `ViewInventory` | ✔ en solo lectura: sin "Registrar movimiento" |
| Administración › Usuarios | `ManageUsers` | ✘ |
| Administración › Bitácora | `ViewAuditLog` | ✘ |
| Configuración › Datos del negocio, Impresora, Seguridad | `ManageSettings` | ✘ (el grupo se oculta) |
| Ayuda › Acerca de | — | ✔ sin "Exportar diagnóstico" (`ExportDiagnostics`) |

- Los botones de acciones restringidas dentro de pantallas permitidas se ocultan con una propiedad
  `Can…` del ViewModel. Ejemplos: Nuevo producto, Registrar movimiento, Probar impresora y
  Exportar diagnóstico. Esa propiedad se calcula con `RolePermissions.Has(session.Role, …)`. La
  protección real sigue estando en el caso de uso.
- Excepciones: "Cancelar venta" (detalle de venta) y "Abrir cajón" (Punto de venta) **sí** se
  muestran al cajero, porque permiten solicitar autorización.
- Tarjetas de Inicio:
  - Ventas de hoy, Últimos 7 días y Más vendidos requieren `ViewAllSales`.
  - Sin existencia y Existencia baja requieren `ViewInventory`.
  - Productos activos requiere `ViewProducts`.

## Autorización de administrador (`AdminAuthorizationView`, diálogo)

- Se abre cuando un caso de uso responde `Forbidden { CanBeAuthorized: true }`: al cancelar una
  venta o al abrir el cajón sin venta.
- Texto: "Esta operación requiere autorización de un administrador: {Cancelar venta | Abrir cajón
  sin venta}".
- Campos Usuario y Contraseña del administrador. Botones "Autorizar" y "Cancelar".
- Con éxito, el diálogo se cierra y el ViewModel repite el comando original con el `grantId`. La
  sesión del cajero no cambia.
- Con error, se muestra "Usuario o contraseña incorrectos, o el usuario no es administrador", o el
  mensaje de bloqueo.

## Ventas realizadas y detalle (FR-020 y FR-026)

- Columna nueva "Cajero" en el listado.
- Filtro "Cajero" (lista de `ListCashiers` más "Todos"). Solo es visible con `ViewAllSales`.
- El detalle muestra "Cajero: {nombre}". Si la venta está cancelada, también "Cancelada por
  {nombre}".
- El ticket agrega la línea "Cajero: {nombre}" bajo la fecha, recortada al ancho
  ([../../006-ticket-printing/contracts/ticket-format.md](../../006-ticket-printing/contracts/ticket-format.md)
  se actualiza).

## Administración › Usuarios (`UsersView` y `UserEditorView`)

- El listado sigue el patrón de Productos:
  - Columnas Nombre completo, Usuario, Rol y Estado.
  - Búsqueda, casilla "Mostrar inactivos" y paginación de 100. Los inactivos se atenúan con
    `InactiveOpacityConverter`.
- Botones "Nuevo usuario", "Editar" (o doble clic) y "Restablecer contraseña" (deshabilitado sobre
  uno mismo).
- El formulario (`FormViewModel`, patrón de productos) tiene:
  - Nombre completo, Usuario, Rol (Administrador o Cajero) y Activo.
  - En alta, además: Contraseña inicial y Confirmar.
- En edición de uno mismo, Rol y Activo están deshabilitados y un texto explica por qué. El caso
  de uso lo impide de todos modos.
- Al desactivar a un usuario que tiene una venta conservada, se pide confirmación: "Se descartará
  la venta en curso que {usuario} dejó guardada".
- "Restablecer contraseña" abre un diálogo con Contraseña temporal y Confirmar. Al terminar, se
  informa: "{usuario} deberá cambiarla al iniciar sesión".

## Administración › Bitácora (`AuditLogView`, solo lectura)

- Filtros:
  - Desde y Hasta (fecha local, convertida a UTC con el mismo helper de Ventas realizadas).
  - Usuario (todos los usuarios, incluido "Sistema").
  - Tipo de evento (catálogo `AuditActions` con texto en español).
- Columnas: Fecha y hora local, Evento, Usuario, Autorizó y Detalle. Del más reciente al más
  antiguo, 100 por página.
- No hay acciones de edición ni de borrado.

## Configuración › Seguridad (`SecuritySettingsView`)

- "Bloquear la sesión tras [ 15 ] minutos sin actividad", con un casillero "Activado". Desactivado
  guarda 0.
- Rango de 1 a 240 minutos.

## Bloqueo por inactividad (`LockOverlay`)

- Cubre todo el contenido del shell, incluido el menú, con un fondo opaco.
- Muestra "Sesión bloqueada", el nombre del usuario y un campo Contraseña. Tiene los botones
  "Desbloquear" y "Cambiar de usuario".
- Si hay un diálogo abierto (cobro, formulario, autorización), `DialogService` lo oculta sin
  cerrarlo y lo vuelve a mostrar al desbloquear. Ningún dato capturado se pierde.
- Al desbloquear, vuelve exactamente a la pantalla y al estado anteriores.
