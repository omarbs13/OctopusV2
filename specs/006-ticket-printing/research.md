# Research: Impresión de ticket, cajón de dinero y datos del negocio

No quedaron puntos "NEEDS CLARIFICATION". Cada sección es una decisión de diseño.

## 1. Dónde viven los datos del negocio

- **Decisión**: tabla `BusinessProfile` con una sola fila, en SQLite.
- **Motivo**: son de la instalación (spec, Suposiciones) y deben respaldarse y migrarse junto con
  la base. Reutiliza auditoría, concurrencia y respaldos.
- **Alternativas**: JSON local (se pierde en respaldos y no es de la instalación); reutilizar
  `logo.png` de `IAppPaths.LogoFile` (es el logotipo de la interfaz, otro propósito).

## 2. Configuración de impresión por máquina

- **Decisión**: `PrintingSettings` serializado con `IPreferencesStore` (clave `printing`).
- **Motivo**: FR-006 exige que sea local; el almacén ya existe, escribe de forma atómica y tolera
  archivos dañados leyéndolos como nulos (se usan valores predeterminados: sin impresora,
  80 mm, impresión automática desactivada, cajón automático activado).
- **Alternativas**: columna en la base (se compartiría entre equipos con la misma base).

## 3. Armado del ticket

- **Decisión**: `TicketBuilder` (Application) produce un `TicketDocument`: una lista de líneas ya
  ajustadas, cada una con alineación y énfasis, más el logotipo opcional. El ancho es 32 columnas
  (58 mm) o 48 (80 mm), fuente A.
- **Ajuste**: la descripción se parte por palabras; una palabra más larga que el ancho se corta.
  La línea del renglón es `cantidad descripción` y el importe se alinea a la derecha en la
  última línea de la descripción, sin cortar el importe.
- **Motivo**: es lógica con cálculo de anchos (regla que la constitución pide probar) y no debe
  vivir en un ViewModel ni en el adaptador. Al ser texto plano, la impresora virtual y la real
  muestran lo mismo.
- **Importes**: se formatean desde centavos enteros; el ticket no recalcula (Suposiciones).
- **Fecha**: se muestra en hora local a partir del UTC guardado.
- **Alternativas**: plantillas de texto o HTML (más piezas sin ventaja para un ticket térmico).

## 4. Codificación ESC/POS

- **Decisión**: `EscPosEncoder` en Infrastructure. Comandos usados: inicializar (`ESC @`), tabla de
  caracteres CP858 (`ESC t 19`), alineación, negrita, imagen raster (`GS v 0`), avance y corte
  (`GS V`), pulso del cajón (`ESC p 0 25 250`).
- **Motivo**: es un subconjunto pequeño y estable; una librería de terceros agregaría una
  dependencia que la constitución exige justificar (Principio VII). `CodePagesEncodingProvider`
  ya viene en el runtime y cubre CP858 (acentos y "ñ", spec Edge Cases).
- **Logotipo**: se convierte a monocromo con SkiaSharp (ya referenciado) a un máximo de 384 puntos
  (58 mm) o 576 puntos (80 mm) de ancho.
- **Alternativas**: EscPrinter/ESCPOS_NET (dependencia nueva, más superficie de fallas).

## 5. Acceso a la impresora por sistema operativo

- **Decisión**: interfaz interna `IRawPrinterTransport` con dos implementaciones, elegidas en el
  arranque con `OperatingSystem.IsWindows()` / `IsLinux()`:
  - **Windows**: `winspool.drv` por P/Invoke (`EnumPrinters`, `OpenPrinter`, `StartDocPrinter`
    con tipo de datos `RAW`, `WritePrinter`).
  - **Linux**: CUPS con `lpstat -e` para listar y `lp -d <impresora> -o raw` con los bytes por
    entrada estándar. El proceso se lanza con la lista de argumentos (nunca una cadena de shell),
    para que el nombre de impresora no pueda inyectar comandos.
- **Motivo**: modo RAW respeta los comandos ESC/POS y el cajón, y no necesita dependencias. Las
  impresoras térmicas suelen estar instaladas en CUPS o en el spooler de Windows.
- **Riesgo**: `lp`/`lpstat` pueden faltar; se trata como "no hay impresoras" y falla de
  impresión con aviso (spec Edge Cases). El adaptador de Windows no se puede probar en Linux; se
  verifica a mano (quickstart) y en la integración continua solo compila.
- **Alternativas**: acceso directo a `/dev/usb/lp0` o a puertos seriales (requiere permisos y
  configuración por equipo); `System.Drawing.Printing` (solo Windows y pensado para gráficos).

## 6. Logotipo

- **Decisión**: se valida con `IImageProcessor` (formato por contenido, tamaño máximo de entrada
  de 1 MB) y se guarda normalizado (PNG, lado máximo 576 px) en la columna `Logo` de
  `BusinessProfile`. Un logotipo inválido se rechaza y conserva el anterior (spec US1 #4).
- **Motivo**: reutiliza el código y las pruebas de 003. La reducción a monocromo se hace al
  imprimir, según el ancho vigente, sin degradar el original guardado.
- **Alternativas**: guardar el archivo en disco (fuera del respaldo de la base).

## 7. La impresión nunca afecta la venta

- **Decisión**: el flujo es `ConfirmSale` (sin cambios) → la venta ya está confirmada → el Punto de
  venta pide `PrintTicket` y `OpenCashDrawer` y queda libre. Ambos handlers devuelven un
  `Result` con error de negocio, sin lanzar excepciones de dispositivo.
  - `PrintGate` (semáforo dentro de Infrastructure) serializa los trabajos, así que los tickets
    rápidos salen en orden y sin mezclarse (spec Edge Cases).
  - Fallo → el ViewModel muestra un aviso con "Reintentar" y "Continuar sin imprimir". Reintentar
    vuelve a llamar a `PrintTicket` con el mismo `SaleId`; no toca la venta, así que no la
    duplica (US3 #5).
  - Los detalles técnicos (excepción, impresora, folio) van a Serilog; el operador solo ve un
    mensaje comprensible.
- **Motivo**: Principio I. Imprimir después de confirmar evita mantener abierta la transacción
  de escritura durante una E/S lenta o bloqueada.
- **Impresora sin configurar** con impresión automática activa: un único aviso por sesión con
  acceso directo a la configuración (spec Edge Cases).
- **Alternativas**: cola persistente con reintentos automáticos (YAGNI; la reimpresión cubre el
  caso).

## 8. Cajón y bitácora

- **Decisión**: `ICashDrawer.OpenAsync` envía el pulso `ESC p` por el mismo transporte. Con
  impresora virtual no hay cajón: el resultado es "abierto (simulado)" y queda un registro en
  el log.
  - **Cobro con efectivo**: `OpenCashDrawer` se invoca solo si algún pago es efectivo y la opción
    local está activa. No se audita (no es una operación sensible).
  - **Sin venta**: cualquier operador con sesión (aclaración de la spec) captura un motivo
    obligatorio; el handler escribe `DRAWER_OPENED` con usuario, motivo y resultado. El intento
    fallido también se audita, con resultado "falló".
- **Auditoría fuera de otra transacción**: `IAuditLog.Add` no guarda. Se agrega
  `IAuditLog.SaveAsync` para persistir la entrada cuando no hay otra escritura que la lleve. Es
  un cambio compatible: los usos actuales no lo llaman.
- **Entidad de la entrada**: `EntityType = "CashDrawer"`, `EntityId` = un GUID v7 nuevo por apertura.
  El detalle guarda el motivo y el resultado y se recorta a 500 caracteres (límite existente).
- **Alternativas**: tabla propia de aperturas (la bitácora ya cubre quién, cuándo y por qué).

## 9. Reimpresión y venta cancelada

- **Decisión**: el ticket se genera siempre a partir de `SaleDetailDto` (`GetSale`, ya existente),
  con `TicketOptions { IsReprint, ... }`. La leyenda "CANCELADA" depende de `SaleStatus`; la
  reimpresión agrega "REIMPRESIÓN". El ticket del cobro original no lleva leyenda.
- **Motivo**: un solo camino de armado para cobro, reimpresión y venta cancelada; refleja lo
  registrado sin recalcular.

## 10. Impresión de prueba

- **Decisión**: `PrintTicket` acepta una fuente `Sample` con datos de ejemplo fijos y folio
  `PRUEBA`; no toca `ISaleRepository`, no consume folio ni genera venta (Suposiciones).

## 11. Impresora virtual

- **Decisión**: escribe `tickets/<yyyyMMdd-HHmmss>-<folio>.txt` en la carpeta de datos, con el
  mismo texto que saldría en papel más una línea `[LOGOTIPO]` si aplica; el aviso al operador
  incluye la ruta. Una carpeta sin permisos o disco lleno es una falla de impresión como
  cualquier otra.
- **Alternativas**: guardar los bytes ESC/POS crudos (no legibles para revisar).

## 12. Pruebas

Según la política mínima (constitución v1.2.0):

- Pruebas de `TicketBuilder`: descripción larga, palabra más larga que el ancho, alineación de
  importe en 32 y 48 columnas, pagos mixtos con cambio, leyendas. Cubren SC-003.
- `SaveBusinessProfileValidator`: obligatorios y logotipo inválido conserva el anterior.
- `PrintTicketHandler` y `OpenCashDrawerHandler` con dobles: una falla no lanza y devuelve error;
  la apertura sin venta audita el resultado exitoso y el fallido; motivo vacío se rechaza.
- Migración de las bases de ejemplo a la versión actual (ya obligatoria).
- Sin pruebas de los adaptadores de plataforma ni de la UI.
