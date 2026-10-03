# Research: Mejoras de UX/UI y comportamiento de la aplicación

**Feature**: 023-ux-ui-polish | **Fecha**: 2026-10-02

No quedaron puntos "NEEDS CLARIFICATION" en el contexto técnico. Cada sección documenta una
decisión tomada a partir del código actual.

## §1 Estado de la ventana principal (H1, FR-001 a FR-004)

**Estado actual**: [MainWindow.axaml](../../src/Pos.Desktop/Shell/MainWindow.axaml) abre en
1200×720, centrada y sin recordar nada.

**Decisión**:

- Nuevo registro `WindowPlacement` guardado con el `IPreferencesStore` existente, clave `window`
  (archivo `<datos>/preferences/window.json`). Las preferencias son por instalación, así que el
  estado queda por equipo, como pide la spec.
- `MainWindow` registra mientras está abierta:
  - el último estado no minimizado (`Maximized` o `Normal`);
  - en estado `Normal`, `Position` (píxeles) y `Width`/`Height` (DIP).
- Se guarda al cerrar, en `App.OnMainWindowClosing`, antes del respaldo de cierre. Así se guarda
  también si el cierre termina con el respaldo.
- Se restaura antes de `Show()` con la función pura `WindowPlacementRules.Resolve(saved,
  workingAreas, minimumSize)`:
  - sin preferencia → `Maximized`;
  - `Normal` cuyo rectángulo no se cruza con el área de trabajo de ninguna pantalla conectada →
    `Normal` centrada en la pantalla principal (FR-004);
  - tamaño guardado menor que el mínimo → se eleva al mínimo.
- Aunque abra maximizada, la ventana también recibe el último tamaño normal. Así, al restaurarla,
  vuelve a un tamaño útil.

**Alternativas descartadas**:

- Guardar por usuario: el estado depende del monitor, no del operador (Assumptions).
- Guardar en cada cambio de tamaño: escribe en disco muchas veces y no aporta nada, porque solo
  importa el estado final.

## §2 Tamaño mínimo y desplazamiento (H2, FR-005, FR-006)

**Estado actual**: `MinWidth="640" MinHeight="480"`; el contenido no tiene desplazamiento global.

**Decisión**:

- `MinWidth="1024" MinHeight="768"`. En Avalonia se aplican al área cliente.
- En `OnOpened`, si el área de trabajo de la pantalla (convertida a DIP con `Scaling`) es menor,
  el mínimo se reduce a esa área (FR-006). Así la ventana nunca queda más grande que la pantalla.
- El contenido de la ventana se envuelve en un `ScrollViewer` (horizontal y vertical en `Auto`).
  Su hijo recibe desde el código `Width = max(ClientSize.Width, 1024)` y
  `Height = max(ClientSize.Height, 768)`.
  - Con un tamaño finito, los `ScrollViewer` internos de cada pantalla (por ejemplo, Inicio y
    listas) siguen funcionando.
  - Con la ventana al tamaño mínimo o mayor, la barra global nunca aparece.

**Alternativas descartadas**:

- Un `ScrollViewer` con solo `MinWidth`/`MinHeight` en el hijo: mide con altura infinita y rompe
  el desplazamiento interno de las pantallas.
- Desplazamiento pantalla por pantalla: son más de 60 vistas que habría que tocar y es fácil
  olvidar alguna.

**Nota**: `MenuViewModel.AutoCollapseWidth = 1000` queda sin efecto con el mínimo nuevo, salvo en
pantallas pequeñas. Se conserva sin cambios.

## §3 Pantalla de carga de 2 segundos (H3, FR-007 a FR-010)

**Estado actual**: `SplashViewModel.MinimumVisible = 800 ms`. `App.StartAsync` espera el mínimo
*después* de la licencia y *antes* de `root.StartAsync()`, así que esa última parte del arranque
no corre en paralelo con la espera.

**Decisión**:

- `MinimumVisible = 2 s`. El cronómetro ya inicia al crear el `SplashViewModel`, justo antes de
  `splash.Show()`, así que los 2 s se cuentan desde que aparece la pantalla de carga.
- En `App.StartAsync` la espera se pide como tarea inmediatamente después de `splash.Show()`:
  `var minimum = splashViewModel.WaitMinimumAsync();`. Después corre todo el arranque
  (migraciones, licencia, `root.StartAsync()`) y al final se hace `await minimum` antes de mostrar
  la principal.
  - Con un arranque de más de 2 s, la tarea ya terminó y no hay espera (FR-008, SC-004).
- Errores (FR-010): `StartupPresenter.RunAsync` ya muestra el diálogo de error sin pasar por la
  espera. La tarea pendiente se descarta: `Task.Delay` no tiene efectos.

**Alternativas descartadas**: un temporizador en la vista del splash, porque separa la regla de su
prueba existente (`SplashViewModelTests`).

## §4 Menú: grupos colapsados y estado por usuario (H4, FR-011 a FR-013)

**Estado actual**: [MenuViewModel](../../src/Pos.Desktop/Navigation/MenuViewModel.cs) ya guarda
`NavigationPreferences(Collapsed, ExpandedGroups)` con la clave global `navigation`. Además, sin
preferencia guardada, abre el grupo de la opción actual.

**Decisión**:

- Clave por usuario: `navigation.{userId:N}`. El `MenuViewModel` del ámbito de sesión recibe el
  `IUserSession` y lo usa para formar la clave. Sin sesión (pruebas), se usa `navigation`.
- Sin preferencia guardada, todos los grupos quedan colapsados. Se elimina la apertura automática
  del grupo de la opción actual; el grupo conserva su marca `currentGroup`.
- Los grupos que no están en `ExpandedGroups`, incluidos los grupos nuevos de versiones futuras,
  quedan colapsados. El código actual ya funciona así.
- El modo contraído (solo iconos) sigue en el mismo registro, ahora por usuario.
- No se migra el archivo global `navigation.json`: cada usuario empieza con todo colapsado una
  vez, que es el comportamiento que pide la spec para la primera sesión. El archivo antiguo se
  ignora y no se borra.

**Alternativas descartadas**: guardar en la base (tabla de preferencias por usuario). Requeriría
una migración, y la spec indica configuración local y ningún cambio de base.

## §5 Botón hamburguesa a la izquierda (H5, FR-014)

**Estado actual**: `Button.menuItem` usa `HorizontalContentAlignment="Stretch"`. Un `PathIcon` de
20 px solo en un espacio estirado queda centrado. Los iconos de las filas expandidas están en una
columna `Auto` a la izquierda. Por eso el botón y los iconos en modo contraído cambian de
posición horizontal.

**Decisión**: el estilo `PathIcon.menuIcon` agrega `HorizontalAlignment="Left"`. Con el margen
de 4 y el relleno de 12, todos los iconos de primer nivel y el botón quedan en x = 16, con el menú
expandido o contraído. El ancho contraído (56) deja el icono dentro.

## §6 Iconos únicos (H6, FR-015 a FR-017)

**Estado actual**:

- 41 elementos de menú (10 grupos, 1 opción suelta y 30 opciones de grupo).
- Muchos repiten icono: `Icon.Movements` se usa 9 veces, `Icon.Catalog` 5 y `Icon.Chart` 5.

**Decisión**:

- Se agregan a `Resources/Icons.axaml` las geometrías que faltan, tomadas de Material Design Icons
  (la misma colección, licencia Apache 2.0 y cuadrícula de 24×24 ya documentadas). La tabla de
  asignación está en [contracts/ui.md](contracts/ui.md#iconos-del-menú).
- Todos se dibujan con el mismo estilo `menuIcon` (20×20). FR-016 se cumple con una sola
  colección y un solo tamaño.
- Las claves existentes no se renombran porque las tarjetas de Inicio las usan.
- FR-017 ya se cumple: cada botón del menú tiene `ToolTip.Tip="{Binding Title}"`.
- Para que el defecto no regrese, `ModuleRegistrationTests` comprueba que el menú completo de un
  Administrador no repite iconos (SC-005).

**Alternativas descartadas**: otra librería de iconos (Fluent Icons, Lucide). Agrega una
dependencia y mezcla estilos (Principio VII, Assumptions).

## §7 Texto centrado en botones grandes (H7, FR-018, FR-019)

**Estado actual**:

- Los botones grandes tienen `MinWidth`/`MinHeight` sin una alineación de contenido explícita.
  Con el tema Fluent, el contenido de un botón más ancho que su texto no siempre queda centrado.
- Solo `Button.touch` del Punto de venta y del cobro declara el centrado.

**Decisión**:

- Nuevo diccionario de estilos `Resources/Styles.axaml`, incluido en `App.axaml` después de
  `FluentTheme`:
  - `Button`: `HorizontalContentAlignment="Center"` y `VerticalContentAlignment="Center"`. Los
    botones que necesitan otra alineación ya la declaran localmente (menú, tarjetas de Inicio,
    segmentos y notificaciones), y el estilo local tiene prioridad.
  - `Button.action`: además, `/template/ ContentPresenter` con `TextWrapping="Wrap"` y
    `TextAlignment="Center"`. Así un texto de dos líneas queda centrado y no se sale del botón
    (FR-019).
- La clase `action` se aplica a los botones grandes de la lista en
  [contracts/ui.md](contracts/ui.md#botones-de-acción-principal).
- Los botones con icono y texto usan un `StackPanel` centrado.

**Alternativas descartadas**: corregir botón por botón sin estilo global. Los botones nuevos
volverían a nacer descentrados.

## §8 Encabezado del negocio en reportes y tickets (H8, FR-020 a FR-025)

**Estado actual**:

- `ReportBusiness(Name, Address, Phone)` se arma en dos sitios: `ReportDocumentBuilder` y
  `AuditLogDocumentBuilder`.
- `PdfReportWriter.DrawHeader` dibuja el encabezado en **todas** las páginas, en una sola línea
  sin ajuste de texto y sin RFC ni logo.
- El XLSX no muestra el aviso cuando faltan los datos del negocio.
- Tickets:
  - `TicketBuilder` (venta) muestra nombre, dirección, teléfono y RFC.
  - `ShiftTicketBuilder`, `CreditNoteTicketBuilder` y `CustomerPaymentReceiptBuilder` muestran
    solo el nombre.
  - El logo ya viaja en `TicketDocument.Logo` en todos los tipos. `EscPosEncoder` lo omite si no
    lo puede rasterizar y `FileTicketPrinter` imprime `[LOGOTIPO]`.

**Decisión**:

- **Modelo único** `BusinessHeader` en `Pos.Application/Business`. Reemplaza a `ReportBusiness`.
  - `From(BusinessProfileDto?)` limpia los espacios y convierte los textos vacíos en nulos.
  - `Lines` devuelve el orden canónico: nombre (título), dirección, `Tel. {teléfono}` y
    `RFC: {rfc}`, y omite los que no existan (FR-023, FR-024).
  - `MissingText = "Datos del negocio no capturados"` (FR-025).
  - PDF, XLSX y tickets consumen el mismo `Lines`, así que el orden no puede divergir.
- **PDF**:
  - El encabezado se dibuja solo en la página 1 (clarificación, FR-020).
  - El logo, si existe, va a la izquierda en una caja de 120×48 pt. Se escala con `Uniform` sin
    deformarse y se decodifica con `SKBitmap.Decode`; si no decodifica, se omite.
  - El texto va a la derecha del logo. La dirección se ajusta en varias líneas con el ajuste de
    texto existente de las celdas.
  - El alto del encabezado se calcula, y `Layout` usa `Top` = margen + alto del encabezado en la
    página 1 y `Top` = margen en las siguientes.
  - El pie ("Generado el… / Página n de N") sigue en todas las páginas, porque no es encabezado de
    negocio.
- **XLSX**: la hoja "Resumen", que es la primera, empieza con `Lines` (el nombre en negrita), sin
  logo. Sin datos del negocio, muestra el aviso. La hoja "Detalle" conserva los encabezados de
  columna en la fila 1 para que el filtro y la ordenación sigan funcionando.
- **Tickets**: `TicketHeader.Add(lines, profile, columns)` en `Printing/Ticket` usa
  `BusinessHeader.Lines` centrado con `TextWrap`. Lo usan los cuatro constructores (venta, nota de
  crédito, abono, turno/corte X/corte Z/movimiento de caja) en lugar de sus encabezados propios.
  El logo no cambia: ya lo imprime el codificador cuando la impresora puede.

**Alternativas descartadas**:

- Repetir un encabezado reducido en las páginas siguientes: la clarificación lo descarta.
- Poner el encabezado del negocio también en la hoja "Detalle": rompe las tablas que el usuario
  filtra y ordena.

## §9 Tarjeta "Período de evaluación" (H9, FR-026 a FR-028)

**Estado actual**:

- Las tarjetas de indicadores usan `Width="240" MinHeight="130"`, así que la altura crece con el
  mensaje. La tarjeta de licencia lleva notas largas ("Incluye todos los módulos…" y avisos).
- `cardValue` usa 34 pt en negrita, y "30 días restantes" no cabe en 208 px.

**Decisión** (en [HomeView.axaml](../../src/Pos.Desktop/Home/HomeView.axaml), para todas las
tarjetas de indicadores, así toda la fila tiene la misma altura):

- `Height="150"` fijo en lugar de `MinHeight` (FR-026).
- Nuevo estilo `TextBlock.cardValue.text` de 22 pt en negrita con `TextTrimming` y `MaxLines="1"`,
  para valores de texto. Los numéricos conservan 34 pt. `DashboardCard` expone `IsTextValue`, y
  `LicenseCard` lo marca (FR-027).
- `cardMessage` con `MaxLines="2"` y `TextTrimming="CharacterEllipsis"`.
- `ToolTip.Tip` de la tarjeta con valor y mensaje completos (FR-028).

**Alternativas descartadas**: un `Viewbox` alrededor del valor. El tamaño del texto variaría según
los días y no sería proporcional al título.

## §10 "Probar escáner" en Configuración (H10, FR-029 a FR-031)

**Estado actual**:

- La página `help.scanner-test` está en el grupo Ayuda (orden 10).
- "Acerca de" tiene el botón "Probar escáner", la carpeta de datos con "Copiar ruta" y el sistema
  operativo.
- El ID de máquina no se muestra en ninguna pantalla.

**Decisión**:

- Se mueven `ScannerTestView`/`ScannerTestViewModel` de `About/` a `Settings/`, porque el código
  se organiza por funcionalidad. Se registran en `SettingsModule` con el id
  `settings.scanner-test`, orden 30 (después de Seguridad), icono `Icon.BarcodeScan` y **sin
  permiso**.
  - `NavigationRegistry` ya muestra el grupo a cualquier usuario con al menos una opción visible
    y filtra cada opción por su permiso. Un Cajero ve Configuración solo con "Probar escáner"
    (FR-031, clarificación).
- `AboutModule` registra solo "Acerca de". Se eliminan el botón y el comando `OpenScannerTest` de
  `AboutViewModel`.
- "Acerca de" muestra únicamente (FR-030):
  - versión;
  - **ID de máquina**: nuevo campo `MachineId` en `LicenseStatusDto`, que
    `GetLicenseStatusHandler` obtiene de `IMachineIdProvider`. Se muestra en texto seleccionable
    a todos los usuarios, porque soporte lo pide por teléfono;
  - exportar diagnóstico (Administrador);
  - administración de licencia (Administrador).
- Se retiran de la pantalla la carpeta de datos, "Copiar ruta" y el sistema operativo. Siguen
  dentro del ZIP de diagnóstico, que es lo que usa soporte (Principio VIII). `GetAppInfo` no
  cambia.

## §11 Pruebas (constitución v1.2.0, Principio VI)

Esta funcionalidad no tiene reglas de dinero ni de inventario. Las pruebas se limitan a los
defectos corregidos con lógica fuera de las vistas y a las que ya existen y cambian.

| Prueba | Tipo | Qué cubre |
|---|---|---|
| `SplashViewModelTests.EsperaMinima_*` | actualizar | 2 s en arranque rápido; sin espera si ya pasaron (US3). |
| `MenuViewModelTests` | actualizar | Sin preferencia → todo colapsado (sustituye a `SinPreferencia_ExpandidoConSoloElGrupoActualAbierto`); clave por usuario (US4). |
| `ModuleRegistrationTests` | actualizar + 1 nueva | Escáner en `settings`, no en `help`; menú de Administrador sin iconos repetidos (US6, US10). |
| `BusinessHeaderTests` (Application) | nueva | Orden canónico y omisión de RFC/teléfono vacíos (FR-023, FR-024). |
| `TicketBuilderTests` | 1 caso nuevo | Corte/nota/abono inician con las mismas líneas que la venta (FR-022). |
| `PdfReportWriterTests` | 1 caso nuevo | Documento de varias páginas: el nombre del negocio aparece solo en la página 1 (FR-020). |
| `XlsxReportWriterTests` | 1 caso nuevo | Sin negocio → aviso en la primera fila de "Resumen" (FR-025). |

No se prueban vistas, estilos, la colocación de la ventana ni la tarjeta. Se validan con
[quickstart.md](quickstart.md).

## §12 Versión

No hay cambios de esquema.

- `Version` sube de 0.16.0 a 0.17.0 (cambio visible de comportamiento).
- Se conserva la base de ejemplo `v0.17.0.db`, como pide el Principio IV para cada versión
  publicada. Se genera con el procedimiento habitual y su esquema es idéntico al de 0.16.0.
