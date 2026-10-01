# Investigación: Registro global de excepciones

Sin marcadores `NEEDS CLARIFICATION` pendientes. Decisiones tomadas tras revisar el código existente.

## D1. Reutilizar lo existente y cerrar brechas

- **Decisión**: ampliar `Logging`, `GlobalExceptionHandlers`, `OperationRunner` y `ZipDiagnosticsExporter`; no crear un subsistema nuevo.
- **Razón**: ya existen y están cableados (`Program`, `App.axaml.cs`). Principios VII y I.
- **Alternativa descartada**: un servicio de registro propio sobre Serilog (duplica y obliga a migrar llamadas).

## D2. Contexto ambiental con un enriquecedor de Serilog

- **Decisión**: `DiagnosticContext` (singleton) reúne usuario (`IUserSession`), pantalla (`Navigator.CurrentEntryId`) y venta en curso (la pantalla de venta publica folio/cantidad de líneas). Un `ILogEventEnricher` lo lee **en el momento de escribir**.
- **Razón**: toda entrada, incluso las de handlers y las de la UI, recibe el contexto sin editar casos de uso (FR-015). Los manejadores globales no pueden conocer la operación; el enriquecedor sí.
- **Alternativa descartada**: pasar el contexto en cada llamada (hoy `OperationRunner` lo hace a mano y se omite fuera de él).
- **Nota**: las pantallas de venta son singletons por sesión; leer su estado desde el hilo de otra tarea requiere una instantánea inmutable publicada por la pantalla, para no tocar la colección de la UI desde otro hilo.

## D3. Niveles

- **Decisión**:
  - FATAL: excepción que llega a un manejador global o a `OperationRunner` (nadie la esperaba). Hoy `OperationRunner` y los manejadores usan ERROR.
  - ERROR: `Result` fallido de un caso de uso o falla controlada (validación, base inaccesible, exportación fallida); se registra solo el tipo de falla y los nombres de campo (spec FR-013).
  - INFO: operaciones críticas (venta registrada, turno abierto, usuario conectado), emitidas desde `UseCases.RunAsync` por tipo de handler, sin editar handlers.
  - WARNING: impresora desconectada, existencia negativa; se emiten donde ya se detectan (cola de impresión y resultado de la venta).
- **Razón**: `UseCases.RunAsync` es el único punto por el que pasan todos los casos de uso desde la UI.
- **Riesgo**: registrar como ERROR cada validación fallida puede ser ruidoso; se registra solo el tipo de error y los nombres de campos, nunca los valores.

## D4. Formato de texto legible

- **Decisión**: plantilla de texto `{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{LevelName}] {Message:lj} {Properties:j}{NewLine}{Exception}`, con una propiedad `LevelName` (INFO, WARNING, ERROR, FATAL) agregada por un enriquecedor.
- **Razón**: `{Level:u}` da "INF/WRN/ERR/FTL"; la especificación pide INFO/ERROR/FATAL. Evita sumar `Serilog.Expressions` (Principio VII).
- **Alternativa descartada**: mantener CLEF; es JSON compacto, poco legible para soporte.

## D5. Retención de 30 días por fecha

- **Decisión**: `retainedFileCountLimit` se quita y una `LogRetention` elimina por la fecha del nombre (`pos-YYYYMMDD.log`) los archivos con más de 30 días. Corre al iniciar y una vez al cambiar el día (temporizador por hora que compara la fecha).
- **Razón**: el límite por número de archivos no equivale a 30 días si la app no se usa algunos días (SC-003). La aplicación puede quedar abierta varios días (FR-006).

## D6. Episodio de error y agrupación

- **Decisión**: `ErrorEpisodeGate` identifica errores por (tipo + primera línea de la traza). Primera aparición: se registra completa y se muestra el mensaje. Repeticiones en una ventana de 5 s: se cuentan y se escribe una sola entrada resumen con el conteo al cerrar la ventana; no se vuelve a mostrar el mensaje mientras el diálogo siga abierto o dure el episodio.
- **Razón**: spec FR-017 y caso límite de errores simultáneos. La ventana de 5 s resuelve el pendiente de la clarificación.

## D7. Recuperación

- **Decisión**: tras el diálogo, el operador queda donde estaba con la venta intacta. Si el manejador de la UI detecta que la excepción proviene de la pantalla actual **y** vuelve a fallar al activarla (segunda falla del mismo episodio), navega a la pantalla principal de venta con `Navigator`.
- **Razón**: spec FR-004. El borrador de venta ya se autoguarda (`DraftAutosaver`), por lo que la venta no se pierde.
- **Pendiente de validar en implementación**: criterio exacto de "no puede continuar"; se prueba a mano con el escenario del quickstart.

## D8. Excepciones fuera del hilo de la UI

- **Decisión**: `TaskScheduler.UnobservedTaskException` marca la excepción como observada, registra FATAL con contexto y notifica al operador por el despachador de la UI. `AppDomain.UnhandledException` solo puede registrar y vaciar el log (el runtime cierra el proceso; límite de .NET ya documentado en el código).
- **Razón**: criterio 4. Las tareas de fondo propias deben pasar por `OperationRunner`; la barrera global es la última defensa.

## D9. Redacción de datos sensibles

- **Decisión**: un enriquecedor sustituye por `***` el valor de propiedades cuyo nombre contenga `password`, `contraseña`, `pin`, `card`, `tarjeta`, `token`, `secret`, `cvv`. Regla para quien escribe mensajes: nunca interpolar esos valores.
- **Límite conocido**: no se puede redactar texto libre dentro de un mensaje de excepción de terceros; se mitiga porque la aplicación no maneja números de tarjeta (las ventas guardan solo el método de pago).

## D10. Exportación

- **Decisión**: `ExportDiagnosticsCommand` gana `IncludeDatabase` (por omisión `false`); el exportador incluye todos los `*.log` de los últimos 30 días y `info.json` (ya trae versión y SO), y la base solo si se pidió. La casilla en "Acerca de" se muestra al administrador. El permiso `ExportDiagnostics` sigue siendo del administrador.
- **Razón**: spec FR-008 y FR-009; hoy la base se incluye siempre y los logs se limitan a 7 días.
- **Compatibilidad**: la prueba existente del exportador se ajusta al nuevo comportamiento.
