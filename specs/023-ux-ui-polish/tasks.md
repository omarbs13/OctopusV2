---

description: "Lista de tareas para implementar 023-ux-ui-polish"
---

# Tasks: Mejoras de UX/UI y comportamiento de la aplicación

**Input**: documentos de diseño de `specs/023-ux-ui-polish/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/ui.md](contracts/ui.md), [quickstart.md](quickstart.md)

**Tests**: se incluyen solo las pruebas que fija el plan (research §11, constitución v1.2.0,
Principio VI). No se prueban vistas, estilos, colocación de ventana ni tarjetas: se validan con
[quickstart.md](quickstart.md).

**Organization**: tareas agrupadas por historia de usuario. Todas son P1; se siguen en el orden de
la spec (US1 → US10) y cada una puede entregarse por separado.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede ejecutarse en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia a la que pertenece (US1…US10)
- Rutas relativas a la raíz del repositorio

## Path Conventions

- Código: `src/Pos.Application/`, `src/Pos.Infrastructure/`, `src/Pos.Desktop/`
- Pruebas: `tests/Pos.Application.Tests/`, `tests/Pos.Infrastructure.Tests/`, `tests/Pos.Desktop.Tests/`
- Compilar: `dotnet build -v q` (0 advertencias). Pruebas: `dotnet test --verbosity quiet`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: versión de la entrega (research §12). Sin dependencias nuevas ni cambios de esquema.

- [X] T001 Subir `<Version>` de `0.16.0` a `0.17.0` en `Directory.Build.props`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: geometrías de iconos que usan US6 (asignación del menú) y US10 (`Icon.BarcodeScan`,
`Icon.Cog`, `Icon.Store`, `Icon.Printer`).

**⚠️ CRITICAL**: US6 y US10 dependen de esta fase. Las demás historias pueden empezar sin ella.

- [X] T002 Agregar a `src/Pos.Desktop/Resources/Icons.axaml` las ~30 `StreamGeometry` nuevas de Material Design Icons (cuadrícula 24×24, misma licencia Apache 2.0 ya documentada en el archivo), con estas claves y rutas MDI: `Icon.CashRegister` (cash-register), `Icon.ReceiptHistory` (receipt-text-clock), `Icon.AccountClock` (account-clock), `Icon.CalendarClock` (calendar-clock), `Icon.CashRefund` (cash-refund), `Icon.CashMultiple` (cash-multiple), `Icon.ReceiptText` (receipt-text), `Icon.CashLock` (cash-lock), `Icon.Archive` (archive), `Icon.CardAccount` (card-account-details), `Icon.TagMultiple` (tag-multiple), `Icon.TicketPercent` (ticket-percent), `Icon.Percent` (percent), `Icon.ChartPie` (chart-pie), `Icon.ChartLine` (chart-line), `Icon.CashCheck` (cash-check), `Icon.ClipboardList` (clipboard-list), `Icon.TruckCheck` (truck-check), `Icon.AccountCash` (account-cash), `Icon.Shape` (shape), `Icon.TruckDelivery` (truck-delivery), `Icon.Factory` (factory), `Icon.AccountKey` (account-key), `Icon.TextSearch` (text-box-search), `Icon.Cog` (cog), `Icon.Store` (store), `Icon.Printer` (printer), `Icon.BarcodeScan` (barcode-scan). Si alguna clave ya existe con la misma geometría, reutilizarla; no renombrar ni cambiar la geometría de claves existentes (las usan las tarjetas de Inicio)

**Checkpoint**: `dotnet build -v q` sin advertencias; los recursos nuevos resuelven.

---

## Phase 3: User Story 1 - Ventana maximizada y estado recordado (Priority: P1) 🎯 MVP

**Goal**: la ventana abre maximizada la primera vez y después en su último estado no minimizado,
con tamaño y posición en estado normal (FR-001 a FR-004, FR-032).

**Independent Test**: borrar `<datos>/preferences/window.json`, arrancar (maximizada), restaurar,
cerrar y volver a abrir (normal, mismo tamaño y posición). Quickstart §1 pasos 1–3, 5 y 6.

### Implementation for User Story 1

- [X] T003 [US1] Crear `src/Pos.Desktop/Shell/WindowPlacement.cs` con: (a) `record WindowPlacement(string State, int X, int Y, double Width, double Height)` donde `State` es `"Maximized"` | `"Normal"` ("Último estado no minimizado. Un valor desconocido se trata como sin preferencia"), `X`/`Y` en píxeles de pantalla del estado normal, `Width`/`Height` = tamaño del área cliente en DIP del estado normal, "≥ mínimo efectivo"; (b) clase estática `WindowPlacementRules` con la función pura `Resolve(WindowPlacement? saved, IReadOnlyList<PixelRect> workingAreas (primera = principal), double scaling, Size minimumSize)` que devuelve estado, posición y tamaño según data-model.md: sin archivo/dañado/`State` desconocido → Maximizada con tamaño normal por defecto 1200×800 centrado en la principal; `Maximized` → Maximizada conservando tamaño/posición guardados para la restauración; `Normal` cuyo rectángulo se cruza con alguna área de trabajo → Normal en `X`,`Y` con `Width`×`Height`; `Normal` fuera de todas las pantallas → Normal centrada en la principal (FR-004); `Width`/`Height` menores que el mínimo efectivo → se elevan al mínimo efectivo. Incluir también la constante de clave de preferencia `"window"`
- [X] T004 [US1] En `src/Pos.Desktop/Shell/MainWindow.axaml.cs`: registrar mientras la ventana está abierta el último estado no minimizado (`Maximized`/`Normal`) y, en `Normal`, `Position` y `Width`/`Height` (suscripción a cambios de `WindowState`, `PositionChanged` y tamaño); exponer un método `CapturePlacement()` que devuelva el `WindowPlacement` actual (nunca `Minimized`, FR-003) y un método `ApplyPlacement(...)` que, antes de `Show()`, aplique el resultado de `WindowPlacementRules.Resolve` con `Screens.All` (`WorkingArea`, `Scaling`) y `Screens.Primary`; aunque abra maximizada, asignar el último tamaño normal para la restauración. Quitar de `src/Pos.Desktop/Shell/MainWindow.axaml` el tamaño fijo 1200×720 y `WindowStartupLocation` centrado si choca con la posición restaurada
- [X] T005 [US1] En `src/Pos.Desktop/Composition/App.axaml.cs`: antes de mostrar `MainWindow`, leer `IPreferencesStore` con la clave `window` (archivo `<datos>/preferences/window.json`; si es ilegible, `JsonFilePreferencesStore` ya registra en el log y devuelve nulo → comportamiento por defecto sin mensaje, FR-032) y llamar `ApplyPlacement`; en `OnMainWindowClosing`, **antes** del respaldo de cierre, guardar `CapturePlacement()` con la misma clave, envolviendo la escritura para que un fallo solo se registre en el log y no impida el cierre (Principio I)

**Checkpoint**: quickstart §1 pasos 1, 2, 3, 5 y 6 cumplen.

---

## Phase 4: User Story 2 - Tamaño mínimo de ventana (Priority: P1)

**Goal**: la ventana no baja de 1024×768 (área cliente) y en pantallas menores se limita al área
de trabajo con desplazamiento global (FR-005, FR-006).

**Independent Test**: en estado normal, reducir la ventana: se detiene en 1024×768. Quickstart §1
pasos 4 y 7.

### Implementation for User Story 2

- [X] T006 [US2] En `src/Pos.Desktop/Shell/MainWindow.axaml` cambiar `MinWidth="640" MinHeight="480"` por `MinWidth="1024" MinHeight="768"` y envolver el contenido de la ventana en un `ScrollViewer` con `HorizontalScrollBarVisibility="Auto"` y `VerticalScrollBarVisibility="Auto"` (con nombre, p. ej. `x:Name="RootScroll"`, y su hijo con nombre, p. ej. `x:Name="RootContent"`)
- [X] T007 [US2] En `src/Pos.Desktop/Shell/MainWindow.axaml.cs`: (a) en `OnOpened`, si el área de trabajo de la pantalla actual convertida a DIP (`WorkingArea / Scaling`) es menor que 1024×768, reducir `MinWidth`/`MinHeight` a esa área (mínimo efectivo = `min(1024×768, área de trabajo en DIP)`); (b) al cambiar `ClientSize`, asignar al hijo del `ScrollViewer` `Width = max(ClientSize.Width, MinWidth efectivo)` y `Height = max(ClientSize.Height, MinHeight efectivo)` para que los `ScrollViewer` internos de cada pantalla sigan funcionando y la barra global solo aparezca por debajo del mínimo; (c) pasar ese mínimo efectivo como `minimumSize` a `WindowPlacementRules.Resolve` (T004). No modificar `MenuViewModel.AutoCollapseWidth` (research §2)

**Checkpoint**: quickstart §1 pasos 4 y 7 cumplen; con la ventana ≥ mínimo no hay barra global.

---

## Phase 5: User Story 3 - Pantalla de carga con duración mínima (Priority: P1)

**Goal**: la pantalla de carga dura al menos 2 s desde que aparece, en paralelo con todo el
arranque; un error se muestra de inmediato (FR-007 a FR-010, SC-004).

**Independent Test**: arranque rápido ≈ 2,0–2,3 s; arranque lento = su duración + ≤ 0,3 s.
Quickstart §2.

### Tests for User Story 3

- [X] T008 [P] [US3] Actualizar `tests/Pos.Desktop.Tests/Splash/SplashViewModelTests.cs` (casos `EsperaMinima_*`): en arranque rápido la espera dura 2 s (`MinimumVisible == TimeSpan.FromSeconds(2)`) y, si ya transcurrieron ≥ 2 s desde la creación del `SplashViewModel`, `WaitMinimumAsync()` termina sin espera adicional

### Implementation for User Story 3

- [X] T009 [US3] En `src/Pos.Desktop/Splash/SplashViewModel.cs` cambiar `MinimumVisible` de 800 ms a 2 s; conservar que el cronómetro inicia al crear el view model (justo antes de `splash.Show()`)
- [X] T010 [US3] En `src/Pos.Desktop/Composition/App.axaml.cs` (`StartAsync`): pedir la espera como tarea inmediatamente después de `splash.Show()` (`var minimum = splashViewModel.WaitMinimumAsync();`), ejecutar después todo el arranque (migraciones, licencia, `root.StartAsync()`) y hacer `await minimum` solo justo antes de mostrar la ventana principal; eliminar el `await` actual entre la licencia y `root.StartAsync()`. En la ruta de error (`StartupPresenter.RunAsync`) no esperar la tarea: se descarta (FR-010)

**Checkpoint**: `dotnet test tests/Pos.Desktop.Tests --verbosity quiet` pasa; quickstart §2 cumple.

---

## Phase 6: User Story 4 - Menú con grupos colapsados y estado recordado (Priority: P1)

**Goal**: sin estado guardado, todos los grupos colapsados; el estado se guarda por usuario
(FR-011 a FR-013).

**Independent Test**: borrar `navigation*.json`, iniciar sesión (todo colapsado), expandir
"Ventas", cerrar y volver a entrar (solo "Ventas" expandido). Quickstart §3 pasos 1–4.

### Tests for User Story 4

- [X] T011 [P] [US4] Actualizar `tests/Pos.Desktop.Tests/Navigation/MenuViewModelTests.cs`: sustituir `SinPreferencia_ExpandidoConSoloElGrupoActualAbierto` por una prueba que verifique que sin preferencia el menú queda expandido (`Collapsed = false`) y **todos** los grupos colapsados, incluido el de la opción actual (que conserva su marca `currentGroup`); agregar una prueba de que, con un `IUserSession` cuyo usuario es `userId`, la preferencia se lee y escribe con la clave `navigation.{userId:N}` y que sin sesión se usa `navigation`; mantener la prueba de que un grupo ausente de `ExpandedGroups` (grupo nuevo) aparece colapsado y que contraer/expandir el menú no altera los grupos

### Implementation for User Story 4

- [X] T012 [US4] En `src/Pos.Desktop/Navigation/MenuViewModel.cs`: recibir `IUserSession` (opcional/nulo en pruebas) y formar la clave de `NavigationPreferences` como `navigation.{userId:N}` (sin sesión: `navigation`); sin preferencia guardada → `Collapsed = false` y todos los grupos colapsados, eliminando la apertura automática del grupo de la opción actual (el grupo conserva `currentGroup`); los grupos que no están en `ExpandedGroups` quedan colapsados y los ids inexistentes se ignoran; seguir guardando en cada expandir/colapsar grupo y al alternar el menú. No migrar ni borrar el `navigation.json` global
- [X] T013 [US4] Verificar/ajustar el registro de `MenuViewModel` en el ámbito de sesión (`src/Pos.Desktop/Shell/SessionScope.cs` o el módulo de navegación que lo registre) para que se resuelva con el `IUserSession` de la sesión activa

**Checkpoint**: pruebas de `MenuViewModelTests` pasan; quickstart §3 pasos 1–4 cumplen.

---

## Phase 7: User Story 5 - Botón hamburguesa alineado a la izquierda (Priority: P1)

**Goal**: el botón hamburguesa y los iconos de primer nivel quedan en x = 16 px con el menú
expandido (240 px) o contraído (56 px) (FR-014).

**Independent Test**: alternar el menú con Ctrl+B; el botón no se mueve horizontalmente.
Quickstart §3 paso 5.

### Implementation for User Story 5

- [X] T014 [US5] En `src/Pos.Desktop/Navigation/MenuView.axaml` agregar `HorizontalAlignment="Left"` al estilo `PathIcon.menuIcon`, de modo que con el margen de 4 y el relleno de 12 de `Button.menuItem` el botón hamburguesa y todos los iconos de primer nivel queden en x = 16; conservar el tooltip "Mostrar u ocultar menú (Ctrl+B)" y comprobar que el icono cabe en el ancho contraído de 56

**Checkpoint**: quickstart §3 paso 5 cumple.

---

## Phase 8: User Story 6 - Iconos distintos por opción del menú (Priority: P1)

**Goal**: los 41 elementos del menú tienen iconos distintos del mismo estilo (FR-015 a FR-017,
SC-005).

**Independent Test**: como Administrador con el menú contraído, ningún icono se repite y cada uno
muestra su tooltip. Quickstart §3 paso 6.

**Depends on**: Phase 2 (T002).

### Tests for User Story 6

- [X] T015 [US6] En `tests/Pos.Desktop.Tests/Navigation/ModuleRegistrationTests.cs` agregar una prueba que construya el menú completo de un Administrador (todos los permisos) con todos los módulos registrados y verifique que los `Icon` de todos los grupos, opciones sueltas y opciones de grupo son distintos (0 repetidos, SC-005)

### Implementation for User Story 6

- [X] T016 [P] [US6] Asignar iconos de ventas según contracts/ui.md: en `src/Pos.Desktop/Sales/SalesModule.cs` (grupo Ventas `Icon.Sales`, Punto de venta `Icon.CashRegister`, Historial de ventas `Icon.ReceiptHistory`), en `src/Pos.Desktop/CashShifts/CashShiftsModule.cs` (Mi turno `Icon.AccountClock` / Turnos `Icon.CalendarClock`, según la página que registre) y en `src/Pos.Desktop/Returns/ReturnsModule.cs` (Devoluciones `Icon.CashRefund`)
- [X] T017 [P] [US6] En `src/Pos.Desktop/CashShifts/CashModule.cs` asignar: grupo Caja `Icon.CashMultiple`, Corte X `Icon.ReceiptText`, Cierre de turno `Icon.CashLock`, Cortes `Icon.Archive` (y Mi turno/Turnos con `Icon.AccountClock`/`Icon.CalendarClock` si se registran aquí)
- [X] T018 [P] [US6] En `src/Pos.Desktop/Customers/CustomersModule.cs` asignar: grupo Clientes `Icon.Users`, Clientes `Icon.CardAccount`
- [X] T019 [P] [US6] En `src/Pos.Desktop/Discounts/DiscountsModule.cs` asignar: grupo Descuentos `Icon.TagMultiple`, Cupones `Icon.TicketPercent`, Configuración de descuentos `Icon.Percent`, Reporte de descuentos `Icon.ChartPie`
- [X] T020 [P] [US6] En `src/Pos.Desktop/Reports/ReportsModule.cs` asignar: grupo Reportes `Icon.Chart`, Ventas `Icon.ChartLine`, Corte de caja `Icon.CashCheck`, Inventario `Icon.ClipboardList`, Compras `Icon.TruckCheck`, Cuentas por cobrar `Icon.AccountCash`
- [X] T021 [P] [US6] Asignar iconos de catálogos e inventario: `src/Pos.Desktop/Products/ProductsModule.cs` (grupo Catálogos `Icon.Catalog`, Productos `Icon.Product`), `src/Pos.Desktop/Categories/CategoriesModule.cs` (Categorías `Icon.Shape`), `src/Pos.Desktop/Inventory/InventoryModule.cs` (grupo Inventario `Icon.Inventory`, Existencias `Icon.Stock`, Movimientos `Icon.Movements`), `src/Pos.Desktop/Purchases/PurchasesModule.cs` (Entrada de compra `Icon.TruckDelivery`, Proveedores `Icon.Factory`)
- [X] T022 [P] [US6] Asignar iconos de administración, configuración, ayuda e inicio: `src/Pos.Desktop/Administration/AdministrationModule.cs` (grupo `Icon.Shield`, Usuarios `Icon.AccountKey`, Bitácora `Icon.TextSearch`), `src/Pos.Desktop/Settings/SettingsModule.cs` (grupo Configuración `Icon.Cog`, Datos del negocio `Icon.Store`, Impresora `Icon.Printer`, Seguridad `Icon.Lock`), `src/Pos.Desktop/About/AboutModule.cs` (grupo Ayuda `Icon.Help`, Acerca de `Icon.Info`), `src/Pos.Desktop/Home/HomeModule.cs` (Inicio `Icon.Home`)

**Checkpoint**: `dotnet test tests/Pos.Desktop.Tests --verbosity quiet` pasa (incluida T015);
quickstart §3 paso 6 cumple.

---

## Phase 9: User Story 7 - Texto centrado en botones grandes (Priority: P1)

**Goal**: contenido de todos los botones centrado en ambos ejes; en `action`/`touch` el texto se
ajusta en varias líneas centradas sin salir del botón (FR-018, FR-019, SC-008).

**Independent Test**: quickstart §4 con la ventana maximizada y en 1024×768.

### Implementation for User Story 7

- [X] T023 [US7] Crear `src/Pos.Desktop/Resources/Styles.axaml` (`<Styles>`) con: `Style Selector="Button"` → `HorizontalContentAlignment="Center"`, `VerticalContentAlignment="Center"`; `Style Selector="Button.action /template/ ContentPresenter, Button.touch /template/ ContentPresenter"` → `TextWrapping="Wrap"`, `TextAlignment="Center"` (verificar el nombre de parte del template de Fluent `PART_ContentPresenter`)
- [X] T024 [US7] Incluir `Resources/Styles.axaml` en `src/Pos.Desktop/Composition/App.axaml` con `<StyleInclude Source="avares://Pos.Desktop/Resources/Styles.axaml"/>` **después** de `FluentTheme`; comprobar que los botones con alineación local (menú, tarjetas de Inicio, segmentos, notificaciones) la conservan
- [X] T025 [P] [US7] En `src/Pos.Desktop/Sales/PointOfSaleView.axaml` agregar la clase `action` a Ingreso, Retiro y Cerrar turno de la barra de turno (Abrir turno, Cerrar turno de otro, Cobrar y laterales ya llevan `touch`); los botones con icono y texto usan un `StackPanel` con `HorizontalAlignment="Center"`
- [X] T026 [P] [US7] Agregar la clase `action` a los botones de los diálogos de caja: `src/Pos.Desktop/CashShifts/OpenShiftView.axaml`, `src/Pos.Desktop/CashShifts/CashMovementView.axaml` (Guardar, Cancelar, Imprimir, Listo) y `src/Pos.Desktop/CashShifts/CloseShiftView.axaml` (los 6 botones)
- [X] T027 [P] [US7] Agregar la clase `action` a "Generar corte X" en `src/Pos.Desktop/CashShifts/ShiftReadoutView.axaml` y a "Cerrar turno" en `src/Pos.Desktop/CashShifts/ShiftClosingView.axaml`

**Checkpoint**: quickstart §4 cumple.

---

## Phase 10: User Story 8 - Encabezado con datos del negocio en reportes y tickets (Priority: P1)

**Goal**: un solo modelo `BusinessHeader` define el orden canónico del encabezado en PDF (solo
página 1, con logo y RFC), XLSX (hoja "Resumen") y los cuatro constructores de ticket (FR-020 a
FR-025, SC-007).

**Independent Test**: quickstart §5 (con logo y RFC, sin ellos y sin datos del negocio).

### Tests for User Story 8

- [X] T028 [P] [US8] Crear `tests/Pos.Application.Tests/Business/BusinessHeaderTests.cs`: `From(null)` devuelve nulo; con perfil completo `Lines` = [Nombre (`IsTitle = true`), Dirección, `Tel. {Phone}`, `RFC: {TaxId}`] en ese orden; textos vacíos o de solo espacios en `Address`/`Phone`/`TaxId` se convierten en nulos y se omiten sin dejar línea vacía (FR-023, FR-024); los textos se recortan; `MissingText == "Datos del negocio no capturados"`
- [X] T029 [P] [US8] Agregar a `tests/Pos.Application.Tests/Printing/TicketBuilderTests.cs` un caso que, con el mismo perfil de negocio, verifique que los tickets de corte/turno (`ShiftTicketBuilder`), nota de crédito (`CreditNoteTicketBuilder`) y abono (`CustomerPaymentReceiptBuilder`) inician con las mismas líneas de encabezado que el ticket de venta (`TicketBuilder`) (FR-022)
- [X] T030 [P] [US8] Agregar a `tests/Pos.Infrastructure.Tests/Reports/PdfReportWriterTests.cs` un caso con un documento de varias páginas: el nombre del negocio aparece solo en la página 1 y el pie aparece en todas (FR-020)
- [X] T031 [P] [US8] Agregar a `tests/Pos.Infrastructure.Tests/Reports/XlsxReportWriterTests.cs` un caso sin datos del negocio (`Business = null`): la fila 1 de la hoja "Resumen" contiene "Datos del negocio no capturados" y el archivo se genera (FR-025)

### Implementation for User Story 8

- [X] T032 [US8] Crear `src/Pos.Application/Business/BusinessHeader.cs`: `sealed record BusinessHeader(string Name, string? Address, string? Phone, string? TaxId, byte[]? Logo)` con `Name` "Nombre comercial (obligatorio en BusinessProfile)", `Address`/`Phone`/`TaxId` "Nulo si está vacío", `Logo` "el logo optimizado de BusinessProfile; nulo si no existe"; `static BusinessHeader? From(BusinessProfileDto? profile)` (nulo si no hay perfil; recorta espacios y convierte vacíos en nulos); `IReadOnlyList<BusinessHeaderLine> Lines` en orden canónico 1. `Name` (`IsTitle = true`), 2. `Address`, 3. `Tel. {Phone}`, 4. `RFC: {TaxId}`, omitiendo los nulos; `const string MissingText = "Datos del negocio no capturados"`; y `sealed record BusinessHeaderLine(string Text, bool IsTitle)`. Documentación XML en español como el resto de Application
- [X] T033 [US8] En `src/Pos.Application/Reports/Export/ReportDocument.cs` sustituir `ReportBusiness` por `BusinessHeader` en `ReportDocument.Business` y eliminar el tipo `ReportBusiness` (depende de T032)
- [X] T034 [P] [US8] En `src/Pos.Application/Reports/Export/ReportDocumentBuilder.cs` (`CompleteAsync`) construir el encabezado con `BusinessHeader.From(profile)` en lugar de armar `ReportBusiness` (depende de T033)
- [X] T035 [P] [US8] En `src/Pos.Application/Audit/ExportAuditLog/AuditLogDocumentBuilder.cs` construir el encabezado con `BusinessHeader.From(profile)` en lugar de armar `ReportBusiness` (depende de T033)
- [X] T036 [US8] En `src/Pos.Infrastructure/Reports/PdfReportWriter.cs`: dibujar el encabezado del negocio **solo en la página 1**; logo (si existe) a la izquierda en una caja de 120×48 pt escalado `Uniform` sin deformar, decodificado **una vez por documento** con `SKBitmap.Decode` y omitido si no decodifica; texto de `BusinessHeader.Lines` a la derecha del logo (o desde el margen sin logo), nombre en negrita y dirección ajustada en varias líneas con el ajuste de texto existente de las celdas; línea separadora debajo; si `Business` es nulo, "Datos del negocio no capturados" en gris; calcular el alto del encabezado y usar en `Layout` `Top` = margen + alto en la página 1 y `Top` = margen en las siguientes; el pie ("Generado el… / Página n de N") sigue en todas (depende de T033)
- [X] T037 [US8] En `src/Pos.Infrastructure/Reports/XlsxReportWriter.cs`: la hoja "Resumen" empieza en la fila 1 con `BusinessHeader.Lines` (una línea por fila, nombre en negrita, sin logo), luego una fila vacía y el título del reporte; sin datos del negocio, `MissingText` en la fila 1; la hoja "Detalle" conserva los encabezados de columna en la fila 1 (depende de T033)
- [X] T038 [US8] Actualizar `tests/Pos.Infrastructure.Tests/Reports/ReportWritersTestData.cs` y `tests/Pos.Infrastructure.Tests/Reports/SalesReportPerformanceTests.cs` para construir `BusinessHeader` en lugar de `ReportBusiness` (depende de T033)
- [X] T039 [US8] Crear `src/Pos.Application/Printing/Ticket/TicketHeader.cs` con `static void Add(<lista de líneas del ticket>, BusinessProfileDto? profile, int columns)` que, a partir de `BusinessHeader.From(profile)?.Lines`, agrega las líneas centradas y ajustadas con `TextWrap` al ancho de 32 o 48 columnas, con el nombre en negrita; sin datos del negocio no agrega nada (el ticket empieza en el título, como hoy). El logo no cambia: sigue en `TicketDocument.Logo` (depende de T032)
- [X] T040 [US8] Reemplazar el encabezado propio por `TicketHeader.Add(...)` en `src/Pos.Application/Printing/Ticket/TicketBuilder.cs`, `src/Pos.Application/Printing/Ticket/CreditNoteTicketBuilder.cs`, `src/Pos.Application/Printing/Ticket/ShiftTicketBuilder.cs` (turno, corte X, corte Z y movimiento de caja) y `src/Pos.Application/Printing/Ticket/CustomerPaymentReceiptBuilder.cs`; ajustar las aserciones existentes de `tests/Pos.Application.Tests/Printing/TicketBuilderTests.cs` que dependan del encabezado anterior (depende de T039)

**Checkpoint**: `dotnet test tests/Pos.Application.Tests --verbosity quiet` y
`dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet` pasan; quickstart §5 cumple.

---

## Phase 11: User Story 9 - Tarjeta "Período de evaluación" con proporciones correctas (Priority: P1)

**Goal**: todas las tarjetas de indicadores miden 240×150; los valores de texto van a 22 pt en una
línea con recorte y tooltip completo (FR-026 a FR-028, SC-009).

**Independent Test**: quickstart §6 con 30, 1 y 0 días, maximizada y en 1024×768.

### Implementation for User Story 9

- [X] T041 [P] [US9] En `src/Pos.Desktop/Home/DashboardCard.cs` agregar la propiedad `bool IsTextValue` (por defecto `false`) para distinguir valores de texto de numéricos
- [X] T042 [P] [US9] En `src/Pos.Desktop/Licensing/LicenseCard.cs` marcar `IsTextValue = true`
- [X] T043 [US9] En `src/Pos.Desktop/Home/HomeView.axaml`, para todas las tarjetas de indicadores: `Width="240" Height="150"` fijo en lugar de `MinHeight="130"`; nuevo estilo `TextBlock.cardValue.text` de 22 pt en negrita con `MaxLines="1"` y `TextTrimming="CharacterEllipsis"`, aplicado con `Classes.text="{Binding IsTextValue}"` (los numéricos conservan 34 pt); `cardMessage` con `MaxLines="2"` y `TextTrimming="CharacterEllipsis"`; `ToolTip.Tip` de la tarjeta con título, valor y mensaje completos (depende de T041)

**Checkpoint**: quickstart §6 cumple.

---

## Phase 12: User Story 10 - "Probar escáner" en Configuración (Priority: P1)

**Goal**: "Probar escáner" se registra en Configuración sin permiso; "Acerca de" muestra solo
versión, ID de máquina, diagnóstico y licencia (FR-029 a FR-031).

**Independent Test**: como Administrador y como Cajero, revisar los grupos Configuración y Ayuda
y la pantalla "Acerca de". Quickstart §7.

**Depends on**: Phase 2 (T002, `Icon.BarcodeScan`). Si se implementa después de US6, T022 ya dejó
los iconos de `SettingsModule` y `AboutModule`.

### Tests for User Story 10

- [X] T044 [US10] Actualizar `tests/Pos.Desktop.Tests/Navigation/ModuleRegistrationTests.cs` (después de T015, mismo archivo): "Probar escáner" está registrada con id `settings.scanner-test` en el grupo `settings`, orden 30, sin permiso, y no aparece en el grupo `help`; un usuario sin `ManageSettings` (Cajero) ve el grupo Configuración solo con "Probar escáner"
- [X] T045 [P] [US10] Actualizar `tests/Pos.Desktop.Tests/About/AboutViewModelTests.cs`: retirar lo relativo a `OpenScannerTest`, carpeta de datos, "Copiar ruta" y sistema operativo; agregar que el ID de máquina se expone desde `LicenseStatusDto.MachineId`

### Implementation for User Story 10

- [X] T046 [US10] Mover con `git mv` `src/Pos.Desktop/About/ScannerTestView.axaml`, `ScannerTestView.axaml.cs` y `ScannerTestViewModel.cs` a `src/Pos.Desktop/Settings/`, cambiando el espacio de nombres a `Pos.Desktop.Settings` y el `x:Class` del axaml; mover también la prueba si existe en `tests/Pos.Desktop.Tests/About/` a `tests/Pos.Desktop.Tests/Settings/`
- [X] T047 [US10] En `src/Pos.Desktop/Settings/SettingsModule.cs` registrar la página `settings.scanner-test` ("Probar escáner") en el grupo Configuración, orden 30 (después de Seguridad), icono `Icon.BarcodeScan`, **sin permiso**, con la vista y el view model movidos (depende de T046)
- [X] T048 [US10] En `src/Pos.Desktop/About/AboutModule.cs` dejar registrado solo "Acerca de" (eliminar `help.scanner-test` y el registro de la vista/view model del escáner) (depende de T046)
- [X] T049 [P] [US10] Agregar `string MachineId` a `LicenseStatusDto` (archivo en `src/Pos.Application/Licensing/GetLicenseStatus/`) y llenarlo en `src/Pos.Application/Licensing/GetLicenseStatus/GetLicenseStatusHandler.cs` con `IMachineIdProvider.GetMachineId()`; actualizar las construcciones de `LicenseStatusDto` en pruebas y dobles que fallen al compilar
- [X] T050 [US10] En `src/Pos.Desktop/About/AboutViewModel.cs` eliminar el comando `OpenScannerTest` y las propiedades de carpeta de datos, "Copiar ruta" y sistema operativo; exponer `MachineId` desde el estado de licencia para todos los usuarios (depende de T049)
- [X] T051 [US10] En `src/Pos.Desktop/About/AboutView.axaml` dejar, de arriba abajo: versión (texto seleccionable), ID de máquina (texto seleccionable, todos los roles), exportar diagnóstico con "Incluir base de datos" (solo `ExportDiagnostics`) y administración de licencia (solo `ManageLicense`); quitar carpeta de datos, "Copiar ruta", sistema operativo y botón "Probar escáner" (depende de T050)
- [X] T052 [P] [US10] Agregar la cadena `About_MachineId` ("ID de máquina") en `src/Pos.Desktop/Resources/Strings.resx` y regenerar/actualizar `src/Pos.Desktop/Resources/Strings.Designer.cs`; retirar las cadenas del escáner/carpeta que queden sin uso en "Acerca de"

**Checkpoint**: `dotnet test tests/Pos.Desktop.Tests --verbosity quiet` pasa; quickstart §7 cumple
(el ZIP de diagnóstico sigue incluyendo carpeta de datos y sistema operativo).

---

## Phase 13: Polish & Cross-Cutting Concerns

**Purpose**: base de ejemplo, documentación de soporte (Principio VIII) y validación final.

- [X] T053 Generar la base de ejemplo `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.17.0.db` con `POS_GENERATE_SAMPLE_DB=1 dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.SampleDatabases.SampleDatabaseGenerator"` (depende de T001; esquema idéntico a 0.16.0) y agregar en `docs/migraciones.md` la sección "0.17.0" indicando que no hay cambios de esquema
- [X] T054 [P] Actualizar `docs/escaner.md`: "Probar escáner" está en Configuración (visible para todos los roles), ya no en Ayuda / "Acerca de"
- [X] T055 [P] Actualizar `docs/carpeta-de-datos.md`: preferencias `preferences/window.json` (por equipo) y `preferences/navigation.{userId}.json` (por usuario; el `navigation.json` global se ignora); "Acerca de" ya no muestra la ruta de datos (está en el diagnóstico)
- [X] T056 [P] Actualizar `docs/reportes.md` y `docs/impresion.md`: encabezado canónico del negocio (logo, nombre, dirección, `Tel.`, `RFC:`), solo en la página 1 del PDF, en "Resumen" del XLSX, mismo encabezado en todos los tickets y aviso "Datos del negocio no capturados"
- [X] T057 [P] Actualizar `docs/manual-usuario/manual.html`: ventana maximizada y recordada, menú colapsado por usuario, iconos nuevos, "Probar escáner" en Configuración y "Acerca de" con el ID de máquina
- [X] T058 Ejecutar `dotnet build -v q` (0 advertencias) y `dotnet test --verbosity quiet` (todas las pruebas pasan, incluidas las de arquitectura)
- [ ] T059 Ejecutar los escenarios manuales de `specs/023-ux-ui-polish/quickstart.md` §1–§7 y anotar cualquier desviación

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias.
- **Foundational (Phase 2)**: solo bloquea US6 y US10 (iconos). Las demás historias pueden empezar
  tras el Setup.
- **User Stories (Phase 3–12)**: independientes entre sí, salvo lo indicado abajo.
- **Polish (Phase 13)**: después de las historias que se entreguen; T053 solo depende de T001.

### User Story Dependencies

- **US1** (ventana): ninguna.
- **US2** (mínimo): comparte `MainWindow.axaml(.cs)` con US1; hacerla después de US1 para pasar el
  mínimo efectivo a `WindowPlacementRules.Resolve` (T007c). Sin US1, T007c se omite.
- **US3** (splash): comparte `App.axaml.cs` con US1 (T005) → no en paralelo con T005.
- **US4** (menú colapsado): ninguna.
- **US5** (hamburguesa): ninguna.
- **US6** (iconos): Phase 2. Comparte `SettingsModule.cs`/`AboutModule.cs` con US10 y
  `ModuleRegistrationTests.cs` con US10 → secuencial con US10.
- **US7** (botones): ninguna (solo ella toca `App.axaml`; US1/US3 tocan `App.axaml.cs`).
- **US8** (encabezado): ninguna; interna: T032 → T033 → (T034, T035, T036, T037, T038) y T032 → T039 → T040.
- **US9** (tarjetas): ninguna.
- **US10** (escáner): Phase 2; secuencial con US6 en los archivos compartidos.

### Within Each User Story

- Las pruebas listadas se escriben primero y deben fallar antes de la implementación.
- Modelos (`BusinessHeader`, `WindowPlacement`) antes que sus consumidores.
- Cada historia termina con su checkpoint del quickstart.

### Parallel Opportunities

- US4, US5, US7, US8 y US9 tocan archivos disjuntos: pueden desarrollarse en paralelo.
- En US6, T016–T022 editan módulos distintos ([P]).
- En US8, T028–T031 (pruebas) en paralelo; luego T034/T035 en paralelo.
- En US7, T025–T027 en paralelo tras T023–T024.
- En Polish, T054–T057 en paralelo.

---

## Parallel Example: User Story 8

```bash
# Pruebas de US8 juntas:
Task: "Crear BusinessHeaderTests en tests/Pos.Application.Tests/Business/BusinessHeaderTests.cs"
Task: "Caso de encabezado común en tests/Pos.Application.Tests/Printing/TicketBuilderTests.cs"
Task: "Caso solo página 1 en tests/Pos.Infrastructure.Tests/Reports/PdfReportWriterTests.cs"
Task: "Caso sin negocio en tests/Pos.Infrastructure.Tests/Reports/XlsxReportWriterTests.cs"

# Tras T033, los dos constructores de documento juntos:
Task: "BusinessHeader.From en src/Pos.Application/Reports/Export/ReportDocumentBuilder.cs"
Task: "BusinessHeader.From en src/Pos.Application/Audit/ExportAuditLog/AuditLogDocumentBuilder.cs"
```

## Parallel Example: User Story 6

```bash
Task: "Iconos de ventas en SalesModule.cs, CashShiftsModule.cs, ReturnsModule.cs"
Task: "Iconos de caja en src/Pos.Desktop/CashShifts/CashModule.cs"
Task: "Iconos de descuentos en src/Pos.Desktop/Discounts/DiscountsModule.cs"
Task: "Iconos de reportes en src/Pos.Desktop/Reports/ReportsModule.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup (T001).
2. Phase 3: US1 (T003–T005).
3. **STOP and VALIDATE**: quickstart §1 pasos 1–3, 5 y 6.

### Incremental Delivery

1. Setup + Foundational (T001–T002).
2. US1 → US2 → US3 (arranque y ventana, `MainWindow` y `App.axaml.cs` en secuencia).
3. US4 → US5 → US6 (menú).
4. US7, US8, US9 (independientes; US8 es la de mayor alcance).
5. US10 (después de US6 por archivos compartidos).
6. Polish (T053–T059).

### Parallel Team Strategy

- Desarrollador A: US1 → US2 → US3 (Shell, Splash, App).
- Desarrollador B: US8 (Application + Infrastructure).
- Desarrollador C: US4 → US5 → US6 → US10 (Navegación y módulos).
- Desarrollador D: US7 → US9 (estilos y tarjetas).

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes.
- Sin dependencias externas nuevas ni cambios de esquema.
- Compilar con 0 advertencias después de cada historia.
- Commit por tarea o por grupo lógico; detenerse en cada checkpoint para validar la historia.
