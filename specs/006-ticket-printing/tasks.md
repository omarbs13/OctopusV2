---

description: "Lista de tareas: Impresión de ticket, cajón de dinero y datos del negocio"
---

# Tasks: Impresión de ticket, cajón de dinero y datos del negocio

**Input**: Documentos de diseño en `specs/006-ticket-printing/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/application-ports.md, contracts/ticket-format.md, quickstart.md

**Tests**: Solo los que exige la constitución v1.2.0 (Principio VI, pruebas mínimas) y el plan: armado del ticket
(ajuste de texto, alineación, leyendas, pagos y cambio), validación de datos del negocio, handlers (una falla de
impresión o de cajón no cambia la venta; la apertura sin venta audita también el intento fallido), persistencia con
SQLite real (`BusinessProfile` y bitácora del cajón), migración de bases de ejemplo y arquitectura. **No** se escriben
pruebas de ViewModels, vistas ni de los adaptadores de hardware (se validan a mano con la impresora virtual,
[quickstart.md](quickstart.md)). Al implementar, ejecutar solo el proyecto de pruebas modificado
(`dotnet test tests/<Proyecto> --verbosity quiet`); compilar con `dotnet build -v q` (0 errores, 0 advertencias).

**Organization**: Tareas agrupadas por historia de usuario. Código en inglés, textos de interfaz en español
(`src/Pos.Desktop/Resources/Strings.resx`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Puede ejecutarse en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: Historia a la que pertenece (US1..US5)

## Path Conventions

Solución por capas: `src/Pos.Domain`, `src/Pos.Application`, `src/Pos.Infrastructure`, `src/Pos.Desktop`;
pruebas en `tests/Pos.Domain.Tests`, `tests/Pos.Application.Tests`, `tests/Pos.Infrastructure.Tests`,
`tests/Pos.ArchitectureTests`. Antes de editar un archivo existente, leerlo y seguir su estilo (predicados estáticos,
`Result`, `FieldError`, patrones de `Sales`, `Inventory` y `Products`; el logotipo sigue el patrón de
`Products/PrepareProductImage` con `IImageProcessor`). Detalle de tipos y reglas: [data-model.md](data-model.md);
firmas de casos de uso y puertos: [contracts/application-ports.md](contracts/application-ports.md); formato del ticket:
[contracts/ticket-format.md](contracts/ticket-format.md). Si una firma del contrato difiere del código existente,
manda el código existente y se documenta la diferencia.

---

## Phase 1: Setup

**Purpose**: Verificar el punto de partida

- [X] T001 Ejecutar `dotnet build -v q` y `dotnet test --verbosity quiet` en la raíz del repositorio y confirmar 0 advertencias y pruebas en verde antes de cambiar nada; leer `src/Pos.Application/Abstractions/IPreferencesStore.cs`, `IAppPaths.cs`, `IAuditLog.cs`, `src/Pos.Application/Products/IImageProcessor.cs`, `src/Pos.Application/Sales/SaleDtos.cs` y `src/Pos.Desktop/Sales/SalesModule.cs` para seguir su estilo

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Puertos compartidos, opciones de impresión y auditoría sin transacción ajena, necesarios para varias historias

**⚠️ CRITICAL**: Ninguna historia puede empezar hasta terminar esta fase

- [X] T002 [P] Crear `PrintingSettings` (record) en `src/Pos.Application/Printing/PrintingSettings.cs` con `PrinterName` (texto o nulo, predeterminado nulo), `UseVirtualPrinter` (bool, false), `PaperWidth` (enum `PaperWidth { Mm58, Mm80 }`, predeterminado `Mm80`), `AutoPrint` (bool, false) y `AutoOpenDrawer` (bool, true); agregar propiedad `Columns` (32 para `Mm58`, 48 para `Mm80`) y `LogoMaxDots` (384 / 576); no es error guardar `AutoPrint` sin impresora
- [X] T003 [P] Crear `IPrintingSettingsStore` (`Load()`, `Save(PrintingSettings)`) en `src/Pos.Application/Printing/IPrintingSettingsStore.cs`
- [X] T004 [P] Crear los puertos `IPrinterCatalog` (`Task<IReadOnlyList<string>> ListAsync(CancellationToken)`), `ITicketPrinter` (`PrintAsync(TicketDocument, PrintingSettings, CancellationToken)` → `PrintOutcome`) e `ICashDrawer` (`OpenAsync(PrintingSettings, CancellationToken)` → `DrawerOutcome`) en `src/Pos.Application/Printing/IPrinterCatalog.cs`, `ITicketPrinter.cs` e `ICashDrawer.cs`; definir `PrintOutcome` y `DrawerOutcome` como records con éxito, destino (impresora o ruta) y código de falla `NotConfigured`/`Unavailable`/`IoError` (enum `DeviceFailure`), sin excepciones hacia la interfaz, en `src/Pos.Application/Printing/DeviceOutcomes.cs`
- [X] T005 [P] Crear los modelos del ticket en `src/Pos.Application/Printing/Ticket/TicketDocument.cs`: `TicketLine` (texto, alineación `Left|Center|Right`, negrita), `TicketDocument` (`Lines`, `Logo` opcional como bytes PNG, `Columns` 32 o 48) y `TicketOptions` (`IsReprint`)
- [X] T006 Agregar `Task SaveAsync(CancellationToken ct)` a `IAuditLog` en `src/Pos.Application/Abstractions/IAuditLog.cs` (persiste lo agregado cuando no hay otra escritura; documentar en el comentario XML) e implementarlo en `src/Pos.Infrastructure/Audit/AuditLog.cs` con `_context.SaveChangesAsync(ct)`; los usos actuales de `Add` no cambian; corregir cualquier doble de `IAuditLog` en `tests/` para que compile
- [X] T007 [P] Crear `PreferencesPrintingSettingsStore` (clave `printing` de `IPreferencesStore`, valores predeterminados de T002 cuando no hay archivo o está dañado) en `src/Pos.Infrastructure/Printing/PreferencesPrintingSettingsStore.cs`
- [X] T008 Registrar `IPrintingSettingsStore` en `src/Pos.Infrastructure/DependencyInjection.cs` (singleton)

**Checkpoint**: La solución compila con 0 advertencias; los puertos existen y nadie los usa todavía

---

## Phase 3: User Story 1 - Datos del negocio (Priority: P1) 🎯 MVP

**Goal**: Capturar y guardar los datos del negocio (nombre comercial, dirección, teléfono, RFC, logotipo, pie) que aparecen en el ticket.

**Independent Test**: Capturar los datos, cerrar y reabrir la aplicación y verificar que se conservan; dirección vacía indica el campo; un logotipo inválido se rechaza y conserva el anterior (quickstart escenario 1).

### Tests for User Story 1

- [X] T009 [P] [US1] Pruebas de `SaveBusinessProfileValidator` en `tests/Pos.Application.Tests/Business/SaveBusinessProfileValidatorTests.cs`: nombre, dirección y teléfono obligatorios (un caso válido y el vacío más importante), RFC de más de 13 caracteres rechazado, pie de más de 200 caracteres rechazado; y `SaveBusinessProfileHandler` con dobles: logotipo inválido o mayor a 1 MB devuelve `ValidationFailed` y conserva el logotipo anterior
- [X] T010 [P] [US1] Pruebas de persistencia con SQLite real en `tests/Pos.Infrastructure.Tests/Business/BusinessProfilePersistenceTests.cs`: guardar y releer un `BusinessProfile` con logotipo, y segunda escritura actualiza la misma fila; usar `TestSupport` existente

### Implementation for User Story 1

- [X] T011 [P] [US1] Crear la entidad `BusinessProfile` en `src/Pos.Domain/Business/BusinessProfile.cs` siguiendo el estilo de las entidades existentes (GUID v7, auditoría y `Version` estándar): `TradeName` texto máx. 80 obligatorio sin espacios sobrantes; `Address` texto máx. 200 obligatorio; `Phone` texto máx. 30 obligatorio; `TaxId` (RFC) texto máx. 13 opcional, sin espacios y en mayúsculas; `Logo` BLOB opcional (PNG normalizado, lado máximo 576 px); `FooterMessage` texto máx. 200 opcional, puede tener varias líneas; método de fábrica y `Update`
- [X] T012 [P] [US1] Crear `IBusinessProfileRepository` (`GetAsync` con seguimiento, `Add`, `SaveChangesAsync` → `SaveOutcome`) en `src/Pos.Application/Business/IBusinessProfileRepository.cs` y `BusinessProfileDto` (con logotipo) en `src/Pos.Application/Business/BusinessProfileDto.cs`
- [X] T013 [US1] Crear `BusinessProfileConfiguration` en `src/Pos.Infrastructure/Persistence/Configurations/BusinessProfileConfiguration.cs` (longitudes del data-model, `Version` como token de concurrencia, sin `HasData`), registrar `DbSet<BusinessProfile>` en `src/Pos.Infrastructure/Persistence/PosDbContext.cs` y crear `BusinessProfileRepository` en `src/Pos.Infrastructure/Business/BusinessProfileRepository.cs` (depende de T011, T012)
- [X] T014 [US1] Generar la migración `BusinessProfile` con `dotnet ef migrations add BusinessProfile` en `src/Pos.Infrastructure/Persistence/Migrations/`; revisar el SQL (`dotnet ef migrations script`) y confirmar que solo crea la tabla `BusinessProfile` sin reconstruir otras tablas; ejecutar `dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet` (migración de bases de ejemplo) (depende de T013)
- [X] T015 [P] [US1] Crear `GetBusinessProfile` (handler devuelve `BusinessProfileDto?`) en `src/Pos.Application/Business/GetBusinessProfile/GetBusinessProfileHandler.cs`
- [X] T016 [US1] Crear `SaveBusinessProfile` en `src/Pos.Application/Business/SaveBusinessProfile/`: `SaveBusinessProfileCommand` (nombre comercial, dirección, teléfono, RFC?, pie?, `LogoChange` = conservar | quitar | nuevo archivo), `SaveBusinessProfileValidator` (FluentValidation, errores `ValidationFailed` por campo con las longitudes del data-model) y `SaveBusinessProfileHandler` (crea la fila la primera vez y actualiza después; valida el logotipo con `IImageProcessor` como en `PrepareProductImage`, entrada máx. 1 MB; un logotipo inválido se rechaza y conserva el anterior) (depende de T012)
- [X] T017 [US1] Registrar en `src/Pos.Application/DependencyInjection.cs` los handlers y el validador de `Business`, y en `src/Pos.Infrastructure/DependencyInjection.cs` `IBusinessProfileRepository` (scoped)
- [X] T018 [US1] Crear el grupo de navegación **Configuración** y la pantalla **Datos del negocio** en `src/Pos.Desktop/Settings/`: `SettingsModule.cs` (`AddSettingsModule`, `AddNavigationGroup` + `AddPage`, ícono existente), `BusinessProfileViewModel.cs` y `BusinessProfileView.axaml(.cs)` (campos, selector de logotipo con vista previa y botón quitar, errores por campo, mensaje de guardado); registrar el módulo en `src/Pos.Desktop/Composition/HostBuilder.cs`; agregar todos los textos en español a `src/Pos.Desktop/Resources/Strings.resx` (depende de T015, T016, T017)

**Checkpoint**: US1 funciona por sí sola: los datos se guardan, sobreviven al reinicio y el logotipo inválido se rechaza

---

## Phase 4: User Story 2 - Configurar la impresora (Priority: P1)

**Goal**: Elegir impresora (o impresora virtual), ancho de papel, impresión automática y cajón automático, con impresión de prueba; configuración local por máquina.

**Independent Test**: Elegir la impresora virtual, 58 mm, pulsar la prueba y verificar el archivo `.txt` en `tickets/`; repetir con 80 mm (quickstart escenario 2).

> Esta historia incluye el armado del ticket y el adaptador de impresión, porque la impresión de prueba los necesita; US3, US4 y US5 los reutilizan.

### Tests for User Story 2

- [X] T019 [P] [US2] Pruebas de `TicketBuilder` en `tests/Pos.Application.Tests/Printing/TicketBuilderTests.cs` (cubren SC-003): descripción larga continúa en la línea siguiente con sangría y el importe alineado a la derecha en la última línea (32 y 48 columnas); palabra más larga que el espacio se parte sin perder caracteres; cantidad con decimales de la unidad (0 para piezas, 3 para kg); pagos mixtos con efectivo recibido y `CAMBIO` solo cuando el efectivo lo generó; leyendas `CANCELADA` y `REIMPRESIÓN`; ningún renglón supera las columnas
- [X] T020 [P] [US2] Prueba del handler de impresión en `tests/Pos.Application.Tests/Printing/PrintTicketHandlerTests.cs` (fuente `Sample`): con `ITicketPrinter` que devuelve falla o lanza una excepción de dispositivo, el handler devuelve un `Result` de error (`PrinterUnavailable` / `PrintFailed`) sin lanzar, y sin configuración devuelve `NotConfigured`

### Implementation for User Story 2

- [X] T021 [US2] Implementar `TextWrap` (ajuste por palabras, corte de palabras largas, alineación a izquierda/derecha/centro por ancho de columnas) en `src/Pos.Application/Printing/Ticket/TextWrap.cs`
- [X] T022 [US2] Implementar `TicketBuilder` en `src/Pos.Application/Printing/Ticket/TicketBuilder.cs` siguiendo [contracts/ticket-format.md](contracts/ticket-format.md): logotipo, nombre comercial (negrita, centrado), dirección, teléfono y RFC si existe, leyendas, folio y fecha local `dd/MM/yyyy HH:mm` (convertida desde UTC con el reloj/zona local del sistema), separadores de guiones, renglones `<cantidad> <descripción>` con importe a la derecha en la última línea, `TOTAL` en negrita, un renglón por forma de pago (efectivo muestra recibido y `CAMBIO`), pie centrado; importes desde centavos enteros sin recalcular; usar nombre y precio guardados en la línea; incluir `BuildSample(profile, columns)` con folio `PRUEBA` y líneas de ejemplo fijas (depende de T005, T021)
- [X] T023 [US2] Crear `PrintTicket` en `src/Pos.Application/Printing/PrintTicket/`: `PrintTicketCommand(Source, IsReprint)` con `Source` = `Sale(SaleId)` | `Sample`, `PrintedTicket` (destino) y `PrintTicketHandler` que carga la venta con `GetSale` (solo `Sale`), el perfil con `IBusinessProfileRepository`, la configuración con `IPrintingSettingsStore`, arma con `TicketBuilder` e imprime con `ITicketPrinter`; captura toda excepción de dispositivo, la registra en Serilog (operación, folio, impresora, sin datos sensibles) y devuelve `Result` con error `NotConfigured`/`PrinterUnavailable`/`PrintFailed`/`NotFound`; no escribe en la base ni consume folio (depende de T004, T022)
- [X] T024 [P] [US2] Crear `GetPrintingSettings`, `SavePrintingSettings` y `ListPrinters` en `src/Pos.Application/Printing/GetPrintingSettings/`, `SavePrintingSettings/` y `ListPrinters/` (`ListPrinters` devuelve lista vacía si el catálogo falla); registrar los handlers de Printing (incluidos T023) y `TicketBuilder` en `src/Pos.Application/DependencyInjection.cs`
- [X] T025 [P] [US2] Implementar `EscPosEncoder` en `src/Pos.Infrastructure/Printing/EscPosEncoder.cs`: `ESC @`, `ESC t 19` (CP858 vía `CodePagesEncodingProvider`, acentos y "ñ"), `ESC a n`, `ESC E n`, logotipo raster `GS v 0` (monocromo con SkiaSharp, ancho máximo 384 o 576 puntos según `PaperWidth`), `LF` × 4 y `GS V 66 0`, y `DrawerPulse()` = `ESC p 0 25 250`
- [X] T026 [P] [US2] Implementar `FileTicketPrinter` (impresora virtual) en `src/Pos.Infrastructure/Printing/FileTicketPrinter.cs`: escribe `tickets/<yyyyMMdd-HHmmss>-<folio>.txt` bajo la carpeta de datos (`IAppPaths`) con el mismo texto del ticket más una línea `[LOGOTIPO]` si aplica y devuelve la ruta como destino; carpeta sin permisos o disco lleno devuelve `IoError`; para el folio en el nombre, sanear caracteres no válidos; agregar el directorio de tickets a `IAppPaths`/`AppPaths` si hace falta
- [X] T027 [P] [US2] Crear la interfaz interna `IRawPrinterTransport` (`ListPrintersAsync`, `SendAsync(printerName, bytes)`) y `PrintGate` (semáforo que serializa trabajos: un ticket es un solo trabajo y no se mezcla) en `src/Pos.Infrastructure/Printing/IRawPrinterTransport.cs` y `PrintGate.cs`
- [X] T028 [P] [US2] Implementar `WinSpoolTransport` en `src/Pos.Infrastructure/Printing/Windows/WinSpoolTransport.cs` con P/Invoke a `winspool.drv` (`EnumPrinters`, `OpenPrinter`, `StartDocPrinter` tipo `RAW`, `StartPagePrinter`, `WritePrinter`, cierres en `finally`), anotado con `[SupportedOSPlatform("windows")]`; ante error devuelve falla sin excepciones hacia la interfaz
- [X] T029 [P] [US2] Implementar `CupsTransport` en `src/Pos.Infrastructure/Printing/Linux/CupsTransport.cs`: lista con `lpstat -e` y envía con `lp -d <impresora> -o raw` escribiendo los bytes por entrada estándar; el proceso se lanza con `ProcessStartInfo.ArgumentList` (nunca una cadena de shell) para que el nombre de impresora no inyecte comandos; si `lp`/`lpstat` no existen o fallan, se trata como "sin impresoras" / `Unavailable`; tiempo máximo razonable con `CancellationToken`
- [X] T030 [US2] Implementar `PlatformTicketPrinter`, `PlatformCashDrawer` y `PlatformPrinterCatalog` en `src/Pos.Infrastructure/Printing/PlatformPrinting.cs` (o un archivo por clase): selección del transporte con `OperatingSystem.IsWindows()`/`IsLinux()`; `PlatformTicketPrinter` usa `FileTicketPrinter` si `UseVirtualPrinter`, si no `EscPosEncoder` + transporte bajo `PrintGate`, y devuelve `NotConfigured` sin impresora; `PlatformCashDrawer` envía `DrawerPulse` por el mismo transporte y con impresora virtual no envía nada, devuelve "abierto (simulado)" y deja un registro en el log; todas las excepciones se capturan y se convierten en `PrintOutcome`/`DrawerOutcome` (depende de T025, T026, T027, T028, T029)
- [X] T031 [US2] Registrar en `src/Pos.Infrastructure/DependencyInjection.cs` `ITicketPrinter`, `ICashDrawer`, `IPrinterCatalog`, `PrintGate` (singleton) y el transporte elegido por sistema operativo (depende de T030)
- [X] T032 [US2] Crear la pantalla **Impresora** en `src/Pos.Desktop/Settings/`: `PrinterSettingsViewModel.cs` y `PrinterSettingsView.axaml(.cs)` con lista de impresoras del sistema + opción "Impresora virtual", ancho 58/80 mm, casillas de impresión automática y apertura automática de cajón, botón **Guardar**, botón **Impresión de prueba** (invoca `PrintTicket` con `Sample`; en éxito muestra destino o ruta del archivo, en falla un aviso comprensible sin cerrar la aplicación); registrar la página en `SettingsModule.cs` y los textos en `Strings.resx` (depende de T023, T024, T031, T018)

**Checkpoint**: US1 y US2 funcionan: la impresión de prueba genera el archivo al ancho elegido y una impresora inexistente solo avisa

---

## Phase 5: User Story 3 - Imprimir el ticket al cobrar (Priority: P1)

**Goal**: Al confirmar el cobro con impresión automática, imprimir el ticket sin bloquear ni afectar la venta; ante falla, avisar y ofrecer reintentar o continuar.

**Independent Test**: Con la impresora virtual, cobrar una venta con varias líneas (una de nombre muy largo) y pagos mixtos; revisar el archivo en 58 y 80 mm; forzar una falla y reintentar sin duplicar la venta (quickstart escenarios 3, 4 y 7).

### Implementation for User Story 3

- [X] T033 [US3] Leer `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs`, `CheckoutViewModel.cs` y `src/Pos.Application/Sales/ConfirmSale/` para ubicar el punto exacto posterior a la confirmación de la venta y cómo se presentan avisos; no hay cambios de código en esta tarea, solo anotar el punto de integración en el mensaje del commit de T034
- [X] T034 [US3] Modificar `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs` (y `CheckoutViewModel.cs` solo si hace falta): tras `ConfirmSale` exitoso, dejar el Punto de venta listo para la siguiente venta sin esperar y, si `AutoPrint` está activo, lanzar `PrintTicket` (`Sale(SaleId)`, `IsReprint = false`) en segundo plano con `Task.Run` sin `await` del flujo de venta, capturando cualquier excepción; ante `Result` de error mostrar un aviso con **Reintentar** (vuelve a llamar a `PrintTicket` con el mismo `SaleId`, sin tocar la venta) y **Continuar sin imprimir**; con impresora sin configurar y `AutoPrint` activo, avisar una sola vez por sesión con acceso directo a **Configuración → Impresora**; con `AutoPrint` desactivado no imprimir; textos en `Strings.resx` (depende de T033, T023)
- [X] T035 [US3] Verificar el orden y la no mezcla de tickets rápidos: confirmar que los trabajos de impresión del Punto de venta pasan por `PrintGate` y agregar una cola serial en Desktop solo si varias llamadas en segundo plano pudieran reordenarse antes de llegar al gate (por ejemplo un `Channel` de un solo consumidor en `src/Pos.Desktop/Sales/PrintJobQueue.cs`), sin persistir nada ni reintentar automáticamente (depende de T034)

**Checkpoint**: US3 funciona: el cobro imprime en segundo plano, una falla nunca afecta la venta y el reintento no la duplica

---

## Phase 6: User Story 4 - Reimprimir y ticket de venta cancelada (Priority: P2)

**Goal**: Reimprimir desde el detalle de una venta con la leyenda "REIMPRESIÓN"; las ventas canceladas llevan "CANCELADA".

**Independent Test**: Reimprimir una venta completada y una cancelada con la impresora virtual y verificar las leyendas (quickstart escenario 5).

### Implementation for User Story 4

- [X] T036 [US4] Agregar el botón **Reimprimir** en `src/Pos.Desktop/Sales/SaleDetailViewModel.cs` y `SaleDetailView.axaml`: comando que invoca `PrintTicket` (`Sale(SaleId)`, `IsReprint = true`) sin afectar la venta; éxito muestra el destino, falla muestra aviso comprensible con **Reintentar**; funciona para ventas completadas y canceladas; textos en `Strings.resx` (depende de T023)
- [X] T037 [US4] Confirmar en `TicketBuilder` (`src/Pos.Application/Printing/Ticket/TicketBuilder.cs`) que `CANCELADA` se deduce de `SaleStatus.Cancelled` aun sin reimpresión y que `REIMPRESIÓN` solo aparece con `IsReprint`, y que el ticket usa los datos vigentes del negocio también al reimprimir; si falta algún caso en `TicketBuilderTests.cs`, añadirlo (depende de T022)

**Checkpoint**: US4 funciona: reimpresión con leyendas correctas sobre ventas completadas y canceladas

---

## Phase 7: User Story 5 - Cajón de dinero (Priority: P2)

**Goal**: Abrir el cajón automáticamente al cobrar con efectivo (configurable) y manualmente sin venta con motivo auditado.

**Independent Test**: Cobrar en efectivo (abre) y solo con tarjeta (no abre); abrir sin venta con motivo y revisar la bitácora; motivo vacío no abre (quickstart escenario 6).

### Tests for User Story 5

- [X] T038 [P] [US5] Pruebas de `OpenCashDrawerHandler` en `tests/Pos.Application.Tests/Printing/OpenCashDrawerHandlerTests.cs` con dobles: motivo vacío devuelve `ValidationFailed` sin abrir ni auditar; apertura manual exitosa escribe `DRAWER_OPENED` con `Resultado: OK` y llama a `IAuditLog.SaveAsync`; apertura manual fallida escribe `Resultado: FALLO` y devuelve `DrawerFailed`; apertura de cobro fallida devuelve error sin lanzar y sin auditar
- [X] T039 [P] [US5] Prueba de auditoría con SQLite real en `tests/Pos.Infrastructure.Tests/Audit/CashDrawerAuditTests.cs`: `IAuditLog.Add` + `SaveAsync` persiste una entrada `DRAWER_OPENED` / `CashDrawer` con su `EntityId`, usuario y detalle recortado a 500 caracteres

### Implementation for User Story 5

- [X] T040 [US5] Crear `OpenCashDrawer` en `src/Pos.Application/Printing/OpenCashDrawer/`: `OpenCashDrawerCommand.ForSale(SaleId)` y `OpenCashDrawerCommand.Manual(Reason)`, `OpenCashDrawerValidator` (motivo obligatorio, recortado y limitado a 200 caracteres) y `OpenCashDrawerHandler`; `ForSale` abre con `ICashDrawer` sin auditar; `Manual` (cualquier operador con sesión, sin permiso especial) valida, abre y escribe `DRAWER_OPENED` con `EntityType = "CashDrawer"`, `EntityId` = GUID v7 nuevo y detalle `Motivo: <texto>; Resultado: OK|FALLO` (máx. 500) tanto en éxito como en fallo, persistiéndolo con `IAuditLog.SaveAsync`; las fallas de dispositivo se capturan, se registran en Serilog y devuelven `NotConfigured`/`DrawerFailed` sin lanzar; registrar en `src/Pos.Application/DependencyInjection.cs` (depende de T004, T006)
- [X] T041 [US5] Modificar `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs`: tras `ConfirmSale` exitoso, si algún pago es efectivo (total o parcial) y `AutoOpenDrawer` está activo, invocar `OpenCashDrawer.ForSale` en segundo plano; cobros sin efectivo o con la opción desactivada no abren; una falla muestra un aviso comprensible sin afectar la venta (depende de T034, T040)
- [X] T042 [US5] Crear el diálogo de motivo `DrawerReasonViewModel.cs` y `DrawerReasonView.axaml(.cs)` en `src/Pos.Desktop/Settings/` (registrar con `AddComponentView` en `SettingsModule.cs`) y el botón **Abrir cajón** en `PointOfSaleView.axaml`/`PointOfSaleViewModel.cs`: pide motivo (vacío no abre y lo pide de nuevo), invoca `OpenCashDrawer.Manual`, avisa el resultado y, si falla, avisa sin afectar la venta en curso; textos en `Strings.resx` (depende de T040)

**Checkpoint**: US5 funciona: apertura automática condicionada al efectivo y apertura sin venta auditada, con o sin falla

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentación de soporte, arquitectura y verificación final

- [X] T043 [P] Escribir la guía de soporte `docs/impresion.md`: configuración de impresora virtual y física en Windows y Linux (CUPS: `lpstat -e`), ubicación de `tickets/` y de `preferences/printing.json`, diagnóstico con el log de Serilog, y prueba en hardware real (quickstart "Verificación en hardware real")
- [X] T044 [P] Verificar las pruebas de arquitectura en `tests/Pos.ArchitectureTests` (Application sin referencias a Infrastructure/Avalonia, Domain sin dependencias, Desktop sin acceso al `DbContext`) y ajustarlas solo si una regla nueva lo requiere; ejecutar `dotnet test tests/Pos.ArchitectureTests --verbosity quiet`
- [X] T045 Ejecutar `dotnet build -v q` (0 errores, 0 advertencias) y `dotnet test --verbosity quiet` (suite completa, incluida la migración de bases de ejemplo) y corregir lo que falle
- [ ] T046 Recorrer a mano los escenarios 1 a 7 de [quickstart.md](quickstart.md) con la impresora virtual (58 y 80 mm) y revisar que el SQL de la migración no reconstruye tablas existentes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup; BLOQUEA todas las historias
- **US1 (Phase 3)**: depende de Foundational; no depende de otras historias
- **US2 (Phase 4)**: depende de Foundational; la impresión de prueba usa el perfil de US1 (`IBusinessProfileRepository`, T012–T013) y agrega la pantalla en el módulo de Configuración de T018
- **US3 (Phase 5)**: depende de US2 (`PrintTicket`, adaptadores)
- **US4 (Phase 6)**: depende de US2 (puede ir en paralelo con US3)
- **US5 (Phase 7)**: depende de US2 (adaptadores) y de T006; T041 depende de T034 (mismo archivo)
- **Polish (Phase 8)**: depende de las historias deseadas

### User Story Dependencies

- **US1 (P1)**: tras Foundational; MVP de datos del negocio
- **US2 (P1)**: tras Foundational y las entidades/repositorio de US1 (T011–T013, T015–T018 para la pantalla)
- **US3 (P1)**: tras US2
- **US4 (P2)**: tras US2; independiente de US3
- **US5 (P2)**: tras US2; el cobro automático se integra con US3 en `PointOfSaleViewModel`

### Within Each Story

- Dominio/puertos → casos de uso → adaptadores de Infrastructure → registro de DI → ViewModels y vistas
- Las pruebas de una regla se escriben junto a ella
- `Strings.resx`, `SettingsModule.cs`, `PointOfSaleViewModel.cs` y las dos `DependencyInjection.cs` son archivos compartidos: no editarlos en paralelo

### Parallel Opportunities

- Foundational: T002, T003, T004, T005 y T007 en paralelo
- US1: T009, T010, T011, T012 y T015 en paralelo
- US2: T019, T020, T024, T025, T026, T027, T028 y T029 en paralelo
- US5: T038 y T039 en paralelo
- Polish: T043 y T044 en paralelo
- US3/US4/US5 pueden ir en paralelo por distintas personas una vez cerrada US2, salvo los archivos compartidos

---

## Parallel Example: User Story 2

```bash
# Pruebas de Application en paralelo:
Task: "Pruebas de TicketBuilder en tests/Pos.Application.Tests/Printing/TicketBuilderTests.cs"
Task: "Prueba de PrintTicketHandler en tests/Pos.Application.Tests/Printing/PrintTicketHandlerTests.cs"

# Adaptadores de Infrastructure en paralelo (archivos distintos):
Task: "EscPosEncoder en src/Pos.Infrastructure/Printing/EscPosEncoder.cs"
Task: "FileTicketPrinter en src/Pos.Infrastructure/Printing/FileTicketPrinter.cs"
Task: "WinSpoolTransport en src/Pos.Infrastructure/Printing/Windows/WinSpoolTransport.cs"
Task: "CupsTransport en src/Pos.Infrastructure/Printing/Linux/CupsTransport.cs"
```

---

## Implementation Strategy

### MVP First (US1 + US2 + US3)

1. Phase 1 (Setup) y Phase 2 (Foundational)
2. Phase 3 (US1): datos del negocio
3. Phase 4 (US2): impresora, ticket de prueba y adaptadores
4. Phase 5 (US3): impresión al cobrar
5. **STOP y VALIDAR**: quickstart escenarios 1–4 y 7 con la impresora virtual

US1, US2 y US3 son las tres historias P1; juntas entregan el comprobante impreso. US1 sola es el primer
incremento demostrable.

### Incremental Delivery

1. Foundational → US1 (datos del negocio) → demo
2. US2 (impresora y prueba) → demo con la impresora virtual
3. US3 (ticket al cobrar) → MVP de impresión
4. US4 (reimpresión y cancelada) → demo
5. US5 (cajón) → demo
6. Polish y verificación en hardware real (una vez por sistema operativo)

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes
- La impresión y el cajón corren después de confirmar la venta y fuera de su transacción; nunca modificar `ConfirmSale`
- Sin paquetes NuGet nuevos: CP858 viene en el runtime y el acceso a impresoras usa P/Invoke y procesos del sistema
- La migración solo crea la tabla `BusinessProfile`; sin `HasData`
- El código de Windows solo compila en la integración continua en Linux; se verifica a mano (quickstart)
- Confirmar con la persona responsable antes de hacer commit
