# Implementation Plan: Mejoras de UX/UI y comportamiento de la aplicación

**Branch**: `023-ux-ui-polish` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/023-ux-ui-polish/spec.md`

## Summary

Corregir diez defectos visuales y de comportamiento sin agregar capacidades de negocio ni tocar
la base de datos.

1. **Ventana** (research §1, §2):
   - Preferencia `window.json` por equipo, con apertura maximizada por defecto y restauración del
     último estado no minimizado, del tamaño y de la posición, corrigiendo la posición si queda
     fuera de pantalla.
   - Mínimo de 1024×768, limitado al área de trabajo en pantallas pequeñas.
   - Desplazamiento global solo cuando el área cliente es menor que el mínimo.
2. **Pantalla de carga** (§3): mínimo de 2 s, contado desde que aparece y en paralelo con todo el
   arranque; los errores se muestran sin esperar.
3. **Menú** (§4, §5, §6):
   - Estado de los grupos por usuario (`navigation.{userId}`), con todos colapsados por defecto.
   - Iconos alineados a la izquierda, en la misma columna que el botón hamburguesa.
   - 41 iconos únicos de Material Design Icons.
4. **Botones** (§7): estilo global de centrado, más la clase `action`/`touch` con texto ajustado
   en varias líneas.
5. **Encabezado del negocio** (§8):
   - Modelo único `BusinessHeader` en Application (orden canónico, omisión de vacíos y aviso sin
     datos).
   - PDF con logo y RFC solo en la página 1; XLSX en las primeras filas de "Resumen"; los cuatro
     constructores de ticket con el mismo encabezado.
6. **Tarjetas de Inicio** (§9): altura fija de 150, valor de texto a 22 pt con recorte y tooltip.
7. **Configuración / Acerca de** (§10):
   - "Probar escáner" pasa a `settings.scanner-test`, visible para todos los roles.
   - "Acerca de" queda con versión, ID de máquina, diagnóstico y licencia.

No se agrega ninguna dependencia externa.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12.1.3 + Fluent, CommunityToolkit.Mvvm, Hosting,
Serilog, SkiaSharp para PDF, ClosedXML para XLSX). **No se agrega ninguna.** Los iconos nuevos son
rutas de Material Design Icons, la colección y la licencia (Apache 2.0) que ya están documentadas
en `Resources/Icons.axaml`.

**Storage**:

- Sin cambios de esquema ni migraciones.
- Preferencias JSON locales con `IPreferencesStore`:
  - `preferences/window.json` (nueva, por equipo);
  - `preferences/navigation.{userId}.json` (cambia la clave; antes era `navigation.json`,
    global).
- `Version` 0.16.0 → 0.17.0 y base de ejemplo `v0.17.0.db`, con el esquema idéntico (research §12).

**Testing**: xUnit v3, con la política mínima de la constitución v1.2.0 (research §11):

- **Desktop**:
  - actualizar `SplashViewModelTests` (2 s) y `MenuViewModelTests` (todo colapsado y clave por
    usuario);
  - en `ModuleRegistrationTests`, actualizar la ubicación del escáner y agregar la prueba de
    iconos únicos.
- **Application**: `BusinessHeaderTests` (nueva) y un caso nuevo en `TicketBuilderTests` (los
  encabezados son iguales en todos los tickets).
- **Infrastructure**: un caso nuevo en `PdfReportWriterTests` (encabezado solo en la página 1) y
  uno en `XlsxReportWriterTests` (aviso sin negocio). Se actualiza `ReportWritersTestData` al
  nuevo `BusinessHeader`.
- Sin pruebas de vistas, estilos, colocación de ventana ni tarjetas: se validan con
  [quickstart.md](quickstart.md).

**Target Platform**: Windows 10+ y Linux (X11/Wayland con XWayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Pantalla de carga: entre 2,0 y 2,3 s en un arranque rápido, y un máximo de 0,3 s extra en un
  arranque lento (SC-004).
- Encabezado del PDF: decodificar un logo de 1 MB o menos una vez por documento, no por página.

**Constraints**:

- Sin red.
- 0 advertencias.
- Una preferencia ilegible nunca interrumpe al operador (FR-032); `JsonFilePreferencesStore` ya
  registra en el log y devuelve nulo.
- El arranque no se retrasa por la espera mínima (FR-009).

**Scale/Scope**:

- 41 elementos de menú y unas 30 geometrías nuevas.
- Unas 15 vistas con botones de acción.
- 6 reportes exportables (Ventas, Corte de caja, Inventario, Mi turno, Descuentos y Bitácora).
- 4 constructores de ticket (6 tipos de comprobante).
- 1 DTO cambia: `LicenseStatusDto` + `MachineId`.
- Casos de uso: ninguno nuevo. Cambian `GetLicenseStatus`, `ReportDocumentBuilder.CompleteAsync`
  y `AuditLogDocumentBuilder`.

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó después del diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Sin dependencia de red. Una preferencia dañada usa los valores por defecto sin mensaje (FR-032). El guardado de la ventana ocurre en el cierre, antes del respaldo, y un fallo de escritura solo se registra. Un logo que no decodifica se omite y el reporte se genera. La espera del splash nunca bloquea el arranque. |
| II. Capas | `BusinessHeader` vive en Application y lo consumen los escritores de Infrastructure y los constructores de ticket de Application. Preferencias, `WindowPlacement`, estilos e iconos viven en Desktop. `MachineId` se obtiene por el puerto existente `IMachineIdProvider`. "Probar escáner" se mueve de carpeta (`About/` → `Settings/`) dentro de Desktop. Las pruebas de arquitectura existentes cubren todo. |
| III. Lógica en el núcleo | El orden y la omisión de los datos del encabezado se deciden en `BusinessHeader` (Application), no en las vistas ni en los escritores. La resolución de la colocación de la ventana es una función pura de la UI, sin reglas de negocio. |
| IV. Integridad de datos | Sin cambios de esquema ni de datos. Las preferencias son archivos locales y no son datos de negocio. Se conserva la base de ejemplo de la versión nueva. |
| V. Multiplataforma | Solo APIs de Avalonia (`Screens`, `WindowState`, `Position`) y rutas de `IAppPaths`. La colocación se valida contra `Screens.All`, que funciona en Windows y Linux; en Wayland puro la posición puede ser ignorada por el compositor y la regla sigue siendo correcta. |
| VI. Calidad verificable | Hay pruebas solo para los defectos con lógica fuera de las vistas (espera, estado del menú, registro e iconos, encabezado canónico, PDF de una sola cabecera, aviso en XLSX). Se actualizan las pruebas existentes que cambian. Las vistas se validan con el quickstart. Persistencia sin cambios. |
| VII. Simplicidad | Sin dependencias nuevas. Se reutilizan `IPreferencesStore`, `NavigationPreferences`, `TextWrap`, el ajuste de texto del PDF y el filtrado por permisos del registro. Un solo modelo de encabezado sustituye a dos construcciones duplicadas y a cuatro encabezados de ticket. |
| VIII. Soporte | "Acerca de" muestra la versión y el ID de máquina y conserva la exportación de diagnóstico. La carpeta de datos y el sistema operativo siguen en el ZIP. Se actualizan `docs/escaner.md` (nueva ubicación), `docs/carpeta-de-datos.md` (preferencias `window.json` y `navigation.{userId}.json`; "Acerca de" ya no muestra la ruta), `docs/reportes.md` y `docs/impresion.md` (encabezado), y `docs/manual-usuario/manual.html` (menú, Acerca de, ventana). |
| IX. Seguridad local | "Probar escáner" no exige permiso (ya era así). Los demás elementos de Configuración siguen exigiendo `ManageSettings`. El ID de máquina no es secreto: ya viaja en la solicitud de licencia. No se auditan cambios de preferencias de UI. |

**Resultado**: sin desviaciones. Complexity Tracking queda vacío. Decisiones explícitas:

- **Estado del menú por usuario y no migrado** (research §4): el `navigation.json` global se
  ignora. Cada usuario empieza una vez con todo colapsado, que es lo que pide la spec para la
  primera sesión.
- **"Acerca de" retira la carpeta de datos y el sistema operativo** (research §10), porque FR-030
  dice "únicamente". Soporte los sigue obteniendo del diagnóstico.
- **Encabezado de XLSX solo en "Resumen"** (research §8): "Detalle" conserva los encabezados de
  columna en la fila 1 para filtrar y ordenar.
- **Altura fija para todas las tarjetas de indicadores**, no solo la de licencia (research §9),
  para que la fila quede alineada.

## Project Structure

### Documentation (this feature)

```text
specs/023-ux-ui-polish/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ui.md
├── checklists/
└── tasks.md             # Lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Pos.Application/
│   ├── Business/BusinessHeader.cs                       # nuevo: Lines, From, MissingText
│   ├── Reports/Export/ReportDocument.cs                 # Business: ReportBusiness → BusinessHeader
│   ├── Reports/Export/ReportDocumentBuilder.cs          # CompleteAsync usa BusinessHeader.From
│   ├── Audit/ExportAuditLog/AuditLogDocumentBuilder.cs  # ídem
│   ├── Printing/Ticket/TicketHeader.cs                  # nuevo: Add(lines, profile, columns)
│   ├── Printing/Ticket/TicketBuilder.cs  CreditNoteTicketBuilder.cs
│   │   ShiftTicketBuilder.cs  CustomerPaymentReceiptBuilder.cs   # usan TicketHeader
│   └── Licensing/GetLicenseStatus/GetLicenseStatusHandler.cs     # MachineId
├── Pos.Infrastructure/
│   └── Reports/PdfReportWriter.cs  XlsxReportWriter.cs  # encabezado en la 1.ª página / Resumen
└── Pos.Desktop/
    ├── Composition/App.axaml(.cs)                       # Styles.axaml; splash en paralelo; guardar ventana
    ├── Resources/Icons.axaml                            # ~30 geometrías nuevas
    ├── Resources/Styles.axaml                           # nuevo: Button, Button.action/.touch
    ├── Splash/SplashViewModel.cs                        # MinimumVisible = 2 s
    ├── Shell/MainWindow.axaml(.cs)                      # mínimo, ScrollViewer, colocación
    ├── Shell/WindowPlacement.cs                         # nuevo: registro + WindowPlacementRules
    ├── Navigation/MenuViewModel.cs  MenuView.axaml      # clave por usuario, colapsado, icono a la izquierda
    ├── */*Module.cs                                     # iconos según contracts/ui.md
    ├── About/AboutModule.cs  AboutView.axaml  AboutViewModel.cs  # sin escáner; ID de máquina
    ├── Settings/SettingsModule.cs                       # + settings.scanner-test
    ├── Settings/ScannerTestView.axaml(.cs)  ScannerTestViewModel.cs  # movidos desde About/
    ├── Home/HomeView.axaml  DashboardCard.cs            # altura fija, IsTextValue, tooltip
    ├── Licensing/LicenseCard.cs                         # IsTextValue = true
    ├── Sales/PointOfSaleView.axaml  CashShifts/*View.axaml       # clase action
    └── Resources/Strings.resx                           # About_MachineId

tests/
├── Pos.Application.Tests/Business/BusinessHeaderTests.cs         # nueva
├── Pos.Application.Tests/Printing/TicketBuilderTests.cs          # + encabezado común
├── Pos.Infrastructure.Tests/Reports/PdfReportWriterTests.cs      # + solo página 1
├── Pos.Infrastructure.Tests/Reports/XlsxReportWriterTests.cs     # + aviso sin negocio
├── Pos.Infrastructure.Tests/Reports/ReportWritersTestData.cs     # BusinessHeader
├── Pos.Infrastructure.Tests/SampleDatabases/v0.17.0.db           # base de ejemplo
├── Pos.Desktop.Tests/Splash/SplashViewModelTests.cs              # 2 s
├── Pos.Desktop.Tests/Navigation/MenuViewModelTests.cs            # colapsado y por usuario
├── Pos.Desktop.Tests/Navigation/ModuleRegistrationTests.cs       # escáner en settings, iconos únicos
└── Pos.Desktop.Tests/About/AboutViewModelTests.cs                # retirar lo del escáner, si aplica
```

**Structure Decision**: se mantiene la solución de cuatro capas (`Pos.Domain`,
`Pos.Application`, `Pos.Infrastructure` y `Pos.Desktop`) con sus proyectos de pruebas. Esta
funcionalidad no toca `Pos.Domain`. El código se organiza por funcionalidad: "Probar escáner" se
mueve a `Settings/` porque ahora pertenece a Configuración.

## Complexity Tracking

Sin desviaciones de la constitución; no aplica.
