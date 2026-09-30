# Research: Estructura de navegación y formularios

Decisiones técnicas de la Fase 0 para [plan.md](plan.md). Parten de lo que ya existe en la
fundación (`specs/001-pos-foundation`): `OperationRunner`, `UseCases`, `IDialogService`,
`PageViewModel`, `StartupPresenter` y el editor de Productos.

## 1. Pantalla de carga

- **Decision**: una ventana propia `SplashWindow` (sin bordes, centrada, sin barra de tareas) que
  `App` muestra en cuanto Avalonia inicia, antes de construir la ventana principal. El arranque
  de la base informa su avance con `IProgress<StartupStep>`: `RunAsync` recibe un parámetro
  opcional `progress`, y `StartupStep` (Application) vale `CheckingDatabase`,
  `BackingUp`, `Migrating`, `Restoring` o `Finishing`. `SplashViewModel` traduce cada paso a un
  texto de `Strings.resx`. La ventana permanece al menos 800 ms (sin retrasar un arranque que ya
  tarda más). Los diálogos de error y de restauración de la fundación se muestran con la pantalla
  de carga como ventana dueña. Si el arranque falla, la pantalla de carga se cierra y la aplicación
  termina.
- **Rationale**: FR-001 a FR-003. `IProgress<T>` es del BCL, no agrega dependencias y se prueba con
  una lista en Application. La instancia única se verifica antes de iniciar Avalonia (fundación),
  así que la pantalla de carga no la anuncia.
- **Alternatives considered**: mostrar la carga dentro de la ventana principal (la ventana
  aparecería con menú antes de saber si la base se puede abrir); una ventana nativa de arranque
  por plataforma (duplicaría código por sistema operativo).

## 2. Logotipo reemplazable

- **Decision**: el logotipo predeterminado es un dibujo vectorial (`DrawingImage`) en los
  recursos de la aplicación. Si existe `logo.png` en la carpeta de datos, se carga como `Bitmap`;
  si no existe o no se puede decodificar, se usa el predeterminado y se registra una advertencia.
  El puerto `IBrandingAssets` (Desktop) expone el logotipo; la ruta sale de `IAppPaths.LogoFile`.
- **Rationale**: FR-004. Un logotipo vectorial se ve bien en cualquier escala y no requiere
  herramientas para generar un PNG.
- **Alternatives considered**: un PNG embebido (requiere un archivo binario generado aparte);
  buscar el logotipo junto al ejecutable (en Windows, `Program Files` no tiene permisos de
  escritura para el cliente).

## 3. Íconos

- **Decision**: íconos vectoriales como `StreamGeometry` en un diccionario de recursos
  (`Resources/Icons.axaml`), con las rutas de Material Design Icons (licencia Apache 2.0; la
  atribución queda en el archivo). Se referencian por clave (`Icon.Home`, `Icon.Catalog`...).
- **Rationale**: Principio VII. Se necesitan unos 10 íconos; un paquete de íconos agregaría una
  dependencia completa por eso. Los vectores se escalan sin pérdida en pantallas táctiles.
- **Alternatives considered**: Material.Icons.Avalonia o Projektanker.Icons.Avalonia (dependencia
  extra que habría que justificar); una fuente de íconos (depende de que la fuente cargue en
  Linux).

## 4. Registro de módulos: menú, pantallas y vistas

- **Decision**: registro por DI en cada módulo, sin tocar vistas ni `App.axaml`:
  - `services.AddNavigationGroup(id, titulo, icono, orden)`.
  - `services.AddPage<TViewModel, TView>(id, titulo, icono, orden, grupo?)`: registra el
    ViewModel como singleton (para conservar su estado en la sesión, FR-020b), una
    `NavigationEntry` y una `ViewRegistration` que asocia el tipo de ViewModel con una fábrica
    de su vista.
  - Cada módulo expone su propio `Add<Modulo>Module()` (por ejemplo `AddProductsModule()`), y
    `HostBuilder` solo los invoca.

  `RegisteredViewLocator : IDataTemplate` resuelve las vistas a partir de las
  `ViewRegistration` y se instala una sola vez en `App.axaml`. `MenuViewModel` construye el
  árbol a partir de las `NavigationGroup` y `NavigationEntry` registradas, ordenadas por
  `Order`.
- **Rationale**: FR-011, FR-020 y SC-007. Una prueba registra un grupo, una pantalla y una
  tarjeta de prueba y verifica que aparecen, sin modificar vistas.
- **Alternatives considered**: `DataTemplate` por pantalla en `App.axaml` (cada módulo
  modificaría un archivo compartido); localizador por reflexión de nombres (frágil y sin
  verificación en compilación).

## 5. Navegación y protección de salida

- **Decision**: `Navigator` (Desktop) es el único que cambia la pantalla actual. Antes de navegar,
  pregunta a la pantalla actual si puede salir (`ILeaveGuard.CanLeaveAsync()`). Las pantallas con
  formulario delegan en su formulario activo (ver §7). Navegar a la opción ya abierta no hace
  nada. Tras navegar, invoca `OnActivatedAsync()` de la pantalla, que refresca sus datos sin
  perder búsqueda, filtros ni selección (FR-020b). El cierre de la ventana principal usa la misma
  protección antes del respaldo al cerrar de la fundación.
- **Rationale**: FR-028 a FR-030 y FR-020b, con un único punto de control que se puede probar sin
  UI.
- **Alternatives considered**: que cada vista intercepte clics del menú (duplicado en cada
  módulo).

## 6. Menú colapsable

- **Decision**: `MenuViewModel` con `IsUserCollapsed` (elección del operador), `IsAutoCollapsed`
  (ventana angosta) e `IsCollapsed = IsUserCollapsed || IsAutoCollapsed`. Los grupos tienen
  `IsExpanded`. La vista usa un `SplitView` en modo `CompactInline` (ancho expandido de 240 y
  contraído de 56). Contraído, cada grupo es un botón con `MenuFlyout` que lista sus opciones, y
  todos los ítems tienen `ToolTip.Tip`. Hay estilos para la opción actual y para el grupo que
  contiene la opción actual. Los ítems tienen `MinHeight` y `MinWidth` de 44. El atajo es Ctrl+B
  (`KeyBinding` en la ventana principal). La ventana principal informa su ancho, y si es menor
  que 1000 activa `IsAutoCollapsed`.
- **Rationale**: FR-012 a FR-019. `SplitView` ya resuelve la animación y el modo compacto.
- **Alternatives considered**: `TreeView` (no ofrece el modo contraído con menú flotante);
  controles propios desde cero (más código sin beneficio).

## 7. Patrón de formularios

- **Decision**:
  - `FormViewModel` (Desktop, abstracto) con `Title`, `SaveCommand` (`AsyncRelayCommand` sin
    concurrencia, FR-024), `CancelCommand`, los eventos `Saved` y `Closed`, `FocusField` y
    `IsDirty`. Cada formulario implementa:
    - `CaptureState()`: devuelve un registro con los valores normalizados como se guardarían
      (nombre recortado, SKU en mayúsculas, precio en centavos si se puede interpretar). Se toma
      al abrir o recargar, y `IsDirty = !Equals(original, actual)`.
    - `SaveCoreAsync()`: devuelve `bool` (guardado o no).
  - `ConfirmLeaveAsync()`: si no hay cambios devuelve verdadero. Si los hay, pregunta con
    `IDialogService.AskUnsavedChangesAsync()` → `Save`, `Discard` o `KeepEditing`. Con `Save`
    ejecuta el guardado y devuelve su resultado; con `Discard`, verdadero; con `KeepEditing`,
    falso.
  - Presentación: `FormPresentation` (`SidePanel` o `FullScreen`) la decide la pantalla que abre
    el formulario, no el formulario. `FormHost` (en `PageViewModel`) tiene `ActiveForm` y
    `ActivePresentation`, y la vista `FormHostView` muestra el panel lateral o reemplaza el
    contenido de la pantalla con una barra "← Regresar al listado" (el menú sigue visible porque
    está fuera del área de contenido).
  - Campos obligatorios: control `FieldLabel` con la propiedad `IsRequired`, que muestra un
    asterisco con color de acento y el texto accesible "(obligatorio)".
  - Validación solo al guardar (clarificación 1): los errores se limpian al intentar guardar de
    nuevo.
  - `ProductEditorViewModel` pasa a heredar de `FormViewModel` sin cambiar su comportamiento.
- **Rationale**: FR-021 a FR-030. Separar la presentación del formulario permite pasar de corto a
  grande sin reescribirlo (FR-023). Comparar el estado normalizado cumple "si se revirtieron los
  cambios, no se pregunta" (FR-030).
- **Alternatives considered**: marcar "sucio" con cualquier cambio de propiedad (preguntaría
  aunque el valor vuelva al original); que cada formulario decida su presentación (acoplaría el
  formulario a su contenedor).
- **Formulario grande de prueba**: `tests/Pos.Desktop.Tests/Forms/SampleLargeFormViewModel` (12
  campos y dos secciones) se abre con `FullScreen` en una pantalla de prueba. Así se verifica el
  patrón sin agregar a la aplicación un formulario que nadie usa (Principio VII).

## 8. Diálogo de cambios sin guardar

- **Decision**: `IDialogService` agrega `AskUnsavedChangesAsync()` con tres botones: Guardar
  (acento), Descartar y Seguir editando (predeterminado para Enter y Esc, la opción segura).
  `DialogWindow` se generaliza para aceptar una lista de botones.
- **Rationale**: FR-028. La opción segura por defecto evita perder datos con un Enter accidental.

## 9. Pantalla de inicio y tarjetas

- **Decision**:
  - `DashboardCard` (Desktop, abstracta): `Title`, `Icon`, `Order`, `Kind` (`Metric` o
    `Chart`), `State` (`Loading`, `Ready`, `Empty` o `Error`), `Value`, `EmptyMessage`,
    `NavigateTo` (id de pantalla, opcional) y `LoadAsync()`.
  - Los módulos las registran con `services.AddDashboardCard<T>()`.
  - `HomeViewModel` carga todas en paralelo al activarse (FR-010). Cada carga está aislada: un
    error deja esa tarjeta en `Error`, se registra con `OperationRunner.RunQuietlyAsync` (registra
    sin mostrar diálogo) y no afecta a las demás.
  - Tarjetas de esta fase:
    - `ActiveProductsCard`: usa el nuevo caso de uso `CountActiveProducts` y lleva a `products`.
    - `LowStockCard` y `OutOfStockCard`: `Empty`, con "Disponible con el módulo de Inventario".
    - `SalesTodayChart`, `SalesLast7DaysChart` y `TopProductsChart`: `Empty`, con "Disponible con
      el módulo de Ventas".
  - La vista coloca las métricas en un `WrapPanel` y las gráficas en una cuadrícula adaptable.
    Una tarjeta con destino se activa con clic, toque o Enter.
- **Rationale**: FR-005 a FR-011 y SC-006.
- **Gráficas**: no se agrega ninguna librería de gráficas en esta fase, porque todas las gráficas
  muestran estado vacío (Principio VII). El contrato `DashboardCard` con `Kind = Chart` deja el
  lugar; la librería se elegirá y justificará con el módulo de Ventas.

## 10. Conteo de productos activos

- **Decision**: el caso de uso `CountActiveProductsHandler` (Application) llama a
  `IProductRepository.CountActiveAsync()`, que cuenta `IsActive && DeletedAt == null` en SQLite.
- **Rationale**: Principio III (el ViewModel no calcula) y SC-006 (dato real).

## 11. Preferencias del menú

- **Decision**: puerto `IPreferencesStore` (Application) con `T? Load<T>(string key)` y
  `void Save<T>(string key, T value)`. `JsonFilePreferencesStore` (Infrastructure) guarda un JSON
  por clave en `<datos>/preferences/<clave>.json`: escribe un `.tmp` y lo renombra. Un archivo
  inexistente o dañado devuelve `null` y registra una advertencia. La clave del menú es
  `navigation`, con `{ "collapsed": bool, "expandedGroups": [ids] }`. Se guarda cuando el operador
  cambia la preferencia; la contracción automática nunca se guarda.
- **Rationale**: FR-017 y los casos límite de preferencia dañada o de grupo inexistente. Queda
  fuera de la base del negocio y es por máquina y usuario.
- **Alternatives considered**: guardarlas en la base SQLite (mezcla preferencias de UI con datos de
  negocio y se incluiría en respaldos y migraciones).

## 12. Pantallas "disponible más adelante"

- **Decision**: `ComingSoonViewModel` con título e ícono, registrada como pantalla para
  `inventory.stock` (Existencias) e `inventory.movements` (Movimientos) mediante
  `AddComingSoonPage(...)`. Cuando exista el módulo, se reemplaza su registro.
- **Rationale**: FR-020a.

## 13. Cierre de la aplicación con cambios

- **Decision**: al primer evento `Closing` de la ventana principal se cancela el cierre y se
  consulta `Navigator.CanLeaveCurrentAsync()`. Si devuelve falso (Seguir editando, o Guardar con
  errores), el cierre queda cancelado **sin** respaldo. Si devuelve verdadero, se ejecuta el
  respaldo al cerrar de la fundación y se cierra.
- **Rationale**: FR-028 y el caso límite de la especificación.

## Paquetes externos

No se agrega ningún paquete (Principio VII). Todo se construye con Avalonia 12, CommunityToolkit.Mvvm
y el BCL, que ya están en la solución.
