---

description: "Tareas de implementación: Registro global de excepciones"
---

# Tareas: Registro global de excepciones

**Entrada**: documentos de diseño en `/specs/010-global-exception-logging/`

**Prerrequisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/diagnostics-contracts.md](contracts/diagnostics-contracts.md)

**Pruebas**: solo las que exige la política mínima de la constitución (Principio VI): retención de 30 días, redacción de datos sensibles, agrupación de repeticiones, exportación y resiliencia del registro (FR-014). Al implementar se ejecutan solo las pruebas del proyecto modificado (`dotnet test --verbosity quiet <proyecto>`); compilar con `dotnet build -v q`.

**Organización**: por historia de usuario. Rutas relativas a la raíz del repositorio. Los casos de uso (`src/Pos.Application/**/*Handler.cs`) NO se modifican (FR-015).

## Formato: `[ID] [P?] [Historia] Descripción`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[USn]**: historia de usuario a la que pertenece

---

## Fase 1: Preparación

**Propósito**: sin proyectos ni paquetes nuevos; solo verificar la base.

- [X] T001 Ejecutar `dotnet build -v q` y `dotnet test --verbosity quiet` desde la raíz y anotar el estado inicial (la compilación debe terminar sin errores ni advertencias).
- [X] T002 [P] Crear la carpeta `src/Pos.Desktop/Diagnostics/` y la carpeta de pruebas `tests/Pos.Desktop.Tests/Diagnostics/` (con un archivo `.gitkeep` si hace falta).

---

## Fase 2: Fundamentos (bloquea todas las historias)

**Propósito**: el contexto ambiental y las constantes que usan todas las historias.

- [X] T003 Crear `src/Pos.Desktop/Diagnostics/DiagnosticContext.cs`: servicio singleton, seguro entre hilos, que expone una instantánea inmutable (`UserId`, nombre de usuario o vacío, `Screen`, `SaleFolio`, `SaleLines`) leyendo `IUserSession` y `Navigator.CurrentEntryId`, y aceptando que la pantalla de venta publique su instantánea. Sin sesión, el usuario queda vacío (escenario 4 de US1). Registrarlo en `src/Pos.Desktop/Composition/HostBuilder.cs` como singleton; no debe capturar servicios de la sesión (validado por `tests/Pos.Desktop.Tests/Composition/CompositionRootTests.cs`).
- [X] T004 Crear `src/Pos.Desktop/Diagnostics/DiagnosticContextEnricher.cs`: `ILogEventEnricher` que agrega a cada evento las propiedades `UserId`, `Screen`, `SaleFolio`/`SaleLines` (solo si hay venta sin guardar) leyendo `DiagnosticContext` en el momento de escribir. Cualquier excepción dentro del enriquecedor se traga (FR-014).
- [X] T005 Crear `src/Pos.Desktop/Diagnostics/LevelNameEnricher.cs`: agrega la propiedad `LevelName` con `INFO`, `WARNING`, `ERROR` o `FATAL` según el nivel del evento (Debug/Verbose se asignan a `INFO`).
- [X] T006 [P] Crear `src/Pos.Desktop/Diagnostics/SensitiveDataRedactor.cs`: `ILogEventEnricher` que sustituye por `***` el valor de toda propiedad cuyo nombre contenga (sin distinguir mayúsculas) `password`, `contraseña`, `pin`, `card`, `tarjeta`, `token`, `secret` o `cvv`, incluidas propiedades anidadas en diccionarios; debe ejecutarse **después** de los demás enriquecedores y de `ForContext` (FR-012).
- [X] T007 Hacer que `PointOfSaleViewModel` publique en `DiagnosticContext` la cantidad de líneas y el `DraftId` de la venta sin guardar (el folio de venta solo existe al cobrar; si ya existe, publicarlo también) cada vez que cambia el carrito (en el punto donde ya llama a `SaveDraft()`/`RefreshCart`), y la limpie al cobrar o descartar. Archivo: `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs`. Solo publica un valor inmutable; no toca la colección de la interfaz desde otro hilo.

**Punto de control**: compila y el grafo de dependencias sigue válido.

---

## Fase 3: Historia 1 - Captura global de excepciones (P1) 🎯 MVP

**Objetivo**: toda excepción no controlada se registra como FATAL con contexto, el operador ve el mensaje en español y la aplicación sigue funcionando.

**Prueba independiente**: ver escenarios 1, 2, 3 y 5 de [quickstart.md](quickstart.md).

- [X] T008 [P] [US1] Crear `src/Pos.Desktop/Diagnostics/ErrorEpisodeGate.cs`: identifica el error por (tipo de excepción + primera línea de la traza); la primera vez lo marca como nuevo (se registra completo y se muestra el mensaje); las repeticiones dentro de una ventana de 5 s las cuenta y, al cerrarse la ventana, emite una sola entrada resumen con el conteo. Recibe `IClock`/`TimeProvider` para poder probarlo y es seguro entre hilos.
- [X] T009 [P] [US1] Prueba de la agrupación en `tests/Pos.Desktop.Tests/Diagnostics/ErrorEpisodeGateTests.cs`: caso válido (error distinto se trata como nuevo) y caso límite (10 repeticiones en 5 s producen un solo mensaje y un resumen con conteo 10).
- [X] T010 [US1] Actualizar `src/Pos.Desktop/Resources/Strings.resx` (y `Strings.Designer.cs` si existe): el texto de `Common_UnexpectedError` pasa a "Ocurrió un error inesperado. Los detalles se registraron para soporte técnico." (FR-003).
- [X] T011 [US1] Reescribir `src/Pos.Desktop/Composition/GlobalExceptionHandlers.cs`: (a) `Dispatcher.UIThread.UnhandledException` y `TaskScheduler.UnobservedTaskException` registran nivel FATAL con tipo, mensaje y traza (el contexto lo agrega el enriquecedor), marcan la excepción como manejada/observada y muestran el mensaje por el hilo de la UI; (b) usan `ErrorEpisodeGate` para mostrar un solo mensaje por episodio; (c) `AppDomain.UnhandledException` registra FATAL y vacía el log (límite de .NET, se mantiene el comentario); (d) todo el manejador está envuelto para que una falla del registro o del diálogo nunca lance (FR-014).
- [X] T012 [US1] Recuperación en `src/Pos.Desktop/Composition/GlobalExceptionHandlers.cs`: tras el mensaje el operador permanece en su pantalla; si la misma pantalla vuelve a fallar en el mismo episodio, navegar a la pantalla principal de venta con `Navigator` (research D7). Si no hay `Navigator` (arranque), solo se registra.
- [X] T013 [US1] En `src/Pos.Desktop/Common/OperationRunner.cs` cambiar a nivel FATAL el registro de excepciones inesperadas en `RunAsync`, `RunQuietlyAsync` y `RunQuietlyResultAsync`; asignar la propiedad `Operation` y mantener los identificadores del contexto; pasar por `ErrorEpisodeGate` para no mostrar más de un diálogo por episodio. Las firmas públicas no cambian.
- [X] T014 [US1] Cablear en `src/Pos.Desktop/Composition/App.axaml.cs` la nueva firma de `GlobalExceptionHandlers.Register` (pasando `DiagnosticContext`, `Navigator` y `ErrorEpisodeGate` desde el contenedor).

**Punto de control**: la historia 1 funciona sola; ejecutar los escenarios 1, 2, 3 y 5 del quickstart.

---

## Fase 4: Historia 2 - Almacenamiento en archivo y exportación (P1)

**Objetivo**: archivos de texto diarios, 30 días de retención por fecha, exportación con la base opcional.

**Prueba independiente**: escenarios 6, 8 y 9 de [quickstart.md](quickstart.md).

- [X] T015 [US2] Cambiar `src/Pos.Desktop/Composition/Logging.cs`: reemplazar `CompactJsonFormatter` por la plantilla de texto `{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{LevelName}] {Message:lj} {Properties:j}{NewLine}{Exception}`, agregar los enriquecedores `LevelNameEnricher`, `DiagnosticContextEnricher` y `SensitiveDataRedactor` (creado en T006), mantener `RollingInterval.Day`, `shared: true`, y quitar `retainedFileCountLimit` (la retención pasa a T016). Quitar el paquete `Serilog.Formatting.Compact` de `src/Pos.Desktop/Pos.Desktop.csproj` y de `Directory.Packages.props` solo si ya nadie lo usa.
- [X] T016 [P] [US2] Crear `src/Pos.Infrastructure/Diagnostics/LogRetention.cs`: `Clean(string logsDirectory, DateOnly today, int days = 30)` elimina los `pos-YYYYMMDD.log` cuya fecha del nombre sea más de 30 días anterior a `today`; ignora archivos que no cumplan el patrón; captura `IOException`/`UnauthorizedAccessException` por archivo sin lanzar.
- [X] T017 [P] [US2] Prueba en `tests/Pos.Infrastructure.Tests/Diagnostics/LogRetentionTests.cs` con carpeta temporal: archivos de hace 29, 30 y 45 días → se conservan los de 29 y 30 y se elimina el de 45; un archivo con otro nombre no se toca.
- [X] T018 [US2] Crear `src/Pos.Desktop/Diagnostics/LogRetentionScheduler.cs`: ejecuta `LogRetention.Clean` al iniciar y revisa cada hora si cambió la fecha local para volver a ejecutarla; si falla, solo lo ignora. Dejarlo en la composición de arranque (`src/Pos.Desktop/Composition/`), que ya referencia Infrastructure, no en las pantallas. Registrarlo en `src/Pos.Desktop/Composition/HostBuilder.cs` e iniciarlo en `src/Pos.Desktop/Composition/App.axaml.cs` después de abrir la ventana principal; detenerlo en `desktop.Exit`.
- [X] T019 [US2] (el permiso `ExportDiagnostics` sigue siendo solo del Administrador; no cambiarlo) Actualizar `src/Pos.Application/Diagnostics/IDiagnosticsExporter.cs` a `ExportAsync(string destinationFile, bool includeDatabase, CancellationToken cancellationToken)` y `src/Pos.Application/Diagnostics/ExportDiagnostics/ExportDiagnosticsCommand.cs` a `ExportDiagnosticsCommand(string DestinationFilePath, bool IncludeDatabase = false)`; `ExportDiagnosticsHandler` solo reenvía el valor. Actualizar también las implementaciones y usos de prueba de la interfaz: `tests/Pos.Desktop.Tests/TestSupport/DesktopFakes.cs`, `tests/Pos.Desktop.Tests/TestSupport/DesktopTestHost.cs` y `tests/Pos.Application.Tests/Diagnostics/ExportDiagnosticsHandlerTests.cs`.
- [X] T020 [US2] Actualizar `src/Pos.Infrastructure/Diagnostics/ZipDiagnosticsExporter.cs`: `LogWindow` pasa de 7 a 30 días; incluir `info.json` (versión, SO, carpeta de datos, hora) y `logs/*.log` siempre; crear la copia de la base, quitar imágenes y agregar `pos.db` solo si `includeDatabase`; si no hay archivos de registro, igualmente generar el zip con `info.json` y devolver una indicación de "sin registros" para que la pantalla lo informe (resuelve el caso límite de la spec).
- [X] T021 [US2] Actualizar `tests/Pos.Infrastructure.Tests/Diagnostics/ZipDiagnosticsExporterTests.cs`: sin base → el zip trae `info.json` y `logs/` y no `pos.db`; con base → también `pos.db`; un log de hace 31 días queda fuera y uno de hace 29 días dentro.
- [X] T022 [US2] En `src/Pos.Desktop/About/AboutViewModel.cs` agregar la propiedad `IncludeDatabase` (por omisión `false`) y pasarla en `ExportDiagnosticsCommand`; en `src/Pos.Desktop/About/AboutView.axaml` agregar la casilla "Incluir respaldo de la base" junto al botón de exportar, y su texto en `src/Pos.Desktop/Resources/Strings.resx` (FR-009). Si la exportación indica que no había registros, mostrar además un aviso claro al operador en `AboutViewModel` (texto en `Strings.resx`). Actualizar `tests/Pos.Desktop.Tests/About/` si alguna prueba usa el comando anterior.
- [X] T023 [P] [US2] Prueba de resiliencia (FR-014) en `tests/Pos.Desktop.Tests/Diagnostics/LoggingResilienceTests.cs`: un sumidero o enriquecedor que lanza una excepción no se propaga a quien registra, y `GlobalExceptionHandlers` sigue mostrando el mensaje.

**Punto de control**: historias 1 y 2 completas; ejecutar escenarios 6, 8 y 9.

---

## Fase 5: Historia 3 - Contexto útil en cada registro (P2)

**Objetivo**: cada entrada trae operación, usuario, venta e identificadores, sin datos sensibles.

**Prueba independiente**: escenarios 3 y 7 de [quickstart.md](quickstart.md).

- [X] T024 [US3] Registrar `SensitiveDataRedactor` (T006) como último enriquecedor en `src/Pos.Desktop/Composition/Logging.cs` y comprobar que se aplica también a propiedades agregadas con `ForContext` en `OperationRunner`.
- [X] T025 [P] [US3] Prueba en `tests/Pos.Desktop.Tests/Diagnostics/SensitiveDataRedactorTests.cs` con un sumidero en memoria (`CollectingSink` de `tests/Pos.Desktop.Tests/TestSupport`): `Password`, `CardNumber` y `Token` salen como `***`; `ProductId` y `SaleId` se conservan.
- [X] T026 [US3] Revisar las llamadas existentes de registro en `src/Pos.Desktop/**` y `src/Pos.Infrastructure/**` (por ejemplo `OperationRunner` y los `context` de `AboutViewModel`) para que los identificadores usen los nombres `ProductId`, `SaleId`, `ShiftId` del contrato y ninguna llamada interpole contraseñas ni datos de tarjeta; documentar en el mensaje del commit cualquier cambio.
- [X] T027 [US3] Asegurar que la pantalla de venta (`src/Pos.Desktop/Sales/PointOfSaleViewModel.cs`) y la de cobro (`src/Pos.Desktop/Sales/CheckoutViewModel.cs`) pasen `SaleId` en el `context` de `OperationRunner` al cobrar, y que turnos (`src/Pos.Desktop/CashShifts/`) pasen `ShiftId`.

**Punto de control**: escenarios 3 y 7 del quickstart pasan.

---

## Fase 6: Historia 4 - Niveles de registro (P2)

**Objetivo**: INFO, WARNING, ERROR y FATAL según el contrato, sin editar los casos de uso.

**Prueba independiente**: escenario 4 de [quickstart.md](quickstart.md).

- [X] T028 [US4] (Implementado solo el ERROR: los INFO de venta, turno e inicio de sesión ya los emiten los casos de uso con su propio ILogger, y la allow-list de errores esperados excluye permisos, credenciales y flujos de turno.) En `src/Pos.Desktop/Common/UseCases.cs` inyectar `ILogger` y, tras `call(...)`, registrar: INFO con mensaje en español si el resultado es exitoso y el tipo de handler está en la lista de operaciones críticas (venta registrada: `ConfirmSaleHandler`; turno abierto: `OpenShiftHandler`; usuario conectado: `SignInHandler`; agregar cierre de turno y anulación de venta si existen); ERROR si el `Result` es fallido (incluye validaciones y permisos denegados, según FR-013), con el tipo de error y los nombres de campo (nunca los valores ingresados). No capturar ni relanzar excepciones (las atiende `OperationRunner`). Mantener la firma pública.
- [X] T029 [US4] (Ya existía: `PrintTicketHandler` y `OpenCashDrawerHandler` registran WARNING al fallar la impresora. Solo se cambió a FATAL el error inesperado de `TicketPrintingService`.) WARNING de impresora desconectada: en `src/Pos.Desktop/Sales/PrintJobQueue.cs` (callback `onError`) y en el punto donde se informa de una impresora no disponible, registrar WARNING en vez de ERROR cuando la causa es una impresora desconectada o no disponible; el resto de fallas se mantiene como está.
- [X] T030 [US4] WARNING de existencia negativa: en `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs`, donde se procesa la revisión de la venta (`InsufficientStock`) y se permite continuar con existencia insuficiente, registrar un WARNING con `ProductId` y cantidad; no modificar `src/Pos.Application/Sales/SaleLineReview.cs`.
- [X] T031 [US4] Verificar que los eventos de `Program.cs` y `Navigator.cs` usen el nivel correcto (`Information` para inicio, `Warning` para anomalías esperadas) y que no quede ningún `Error` para excepciones no controladas.

**Punto de control**: escenario 4 del quickstart muestra los cuatro niveles.

---

## Fase 7: Pulido y verificación

- [X] T032 [P] Ejecutar `dotnet build -v q` en la raíz: cero errores y cero advertencias (las advertencias son errores).
- [X] T033 [P] Ejecutar `dotnet test --verbosity quiet` en `tests/Pos.Desktop.Tests`, `tests/Pos.Infrastructure.Tests` y `tests/Pos.ArchitectureTests`; corregir lo que falle.
- [X] T034 Verificar que ningún archivo de `src/Pos.Application/**/*Handler.cs` (salvo `ExportDiagnosticsHandler`) cambió: `git diff --stat -- src/Pos.Application` (FR-015).
- [ ] T035 Recorrer manualmente [quickstart.md](quickstart.md) en Linux (y en Windows si hay equipo) y anotar el resultado, incluyendo SC-005 (retomar el trabajo en menos de 10 s) y SC-007 (exportar 30 días de logs en menos de 30 s).
- [X] T036 [P] Actualizar la documentación de soporte en español (por ejemplo `docs/` o el README si existe) con la ubicación de los logs, el formato, los niveles y cómo exportar el diagnóstico.

---

## Dependencias y orden

- **Fase 1** → **Fase 2** (bloquea todo) → historias.
- **US1** necesita T003–T007. **US2** necesita T004, T005 y T006 (para T015) pero es independiente de US1. **US3** necesita T015. **US4** necesita T003 y T015.
- Orden sugerido: US1 → US2 → US3 → US4 (US1 y US2 pueden hacerse en paralelo por distintas personas).
- Dentro de una historia: las pruebas marcadas [P] pueden escribirse junto con el componente que prueban.

## Oportunidades de paralelismo

- Fase 2: T004, T005 y T006 en paralelo tras T003.
- US1: T008 y T009 en paralelo con T010.
- US2: T016, T017 y T019 en paralelo; T020–T022 después de T019.
- US3: T025 puede escribirse en paralelo con T026 y T027.
- Pulido: T032, T033 y T036 en paralelo.

## Estrategia de implementación

1. **MVP**: Fases 1, 2 y 3 (US1). Con eso toda excepción no controlada se registra como FATAL con contexto y el operador ve el mensaje.
2. Agregar US2 (formato, 30 días, exportación) → validar escenarios 6, 8 y 9.
3. Agregar US3 y US4 (privacidad y niveles) → validar.
4. Cerrar con el pulido (Fase 7).

## Notas

- Sin migraciones ni cambios de esquema.
- Si una tarea exige editar un handler de `Pos.Application` (salvo la exportación), detenerse y reconsiderar: contradice FR-015.
- Hacer commit después de cada historia.
