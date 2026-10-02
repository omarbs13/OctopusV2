# Contrato: interfaz

**Feature**: `021-barcode-scanner` | **Plan**: [../plan.md](../plan.md)

Textos en español en `Strings.resx`. Atajos existentes de 005 sin cambios salvo lo indicado.

## Detector de ráfagas (`Pos.Desktop/Common/Scanner/ScanBurstDetector.cs`)

| Constante | Valor | Uso |
|---|---|---|
| `MaxGap` | 50 ms | Intervalo máximo entre teclas de una ráfaga (incluye el Enter) |
| `MinBurstLength` | 3 caracteres | Mínimo para considerar escaneo |
| `IdleEnd` | 300 ms | Fin de una lectura sin terminador (pantalla de prueba) |
| `StarDeferral` | 60 ms | Espera del atajo `*` con el campo vacío (research §9) |

Entrada: `OnText(string, long timestamp)`, `OnTerminator(Terminator, long timestamp)`. Salida: la
lectura completa (`RawText`, `Terminator`, `IsBurst`) y la instantánea del `TextBox` enfocado al
inicio de la secuencia (para restaurarla, research §7).

## Punto de venta

| Situación | Comportamiento |
|---|---|
| Foco en el campo de captura, lectura + Enter | `Capture` encola `ScanInput(texto, IsScan)` (como hoy, con el origen) |
| Foco en la lista de líneas, un botón u otro control que no es `TextBox`, sin modal | El primer carácter mueve el foco al campo de captura y se escribe ahí; el Enter final captura. Un Enter sin texto previo conserva su efecto normal |
| Foco en cantidad u otro `TextBox` | No se intercepta (FR-007) |
| Modal abierto (selector, cobro, cajón, cliente, descuento, cupón) o diálogo en ventana abierto desde la venta | Una ráfaga con Enter: el Enter no llega al diálogo, el `TextBox` enfocado recupera su texto previo y aparece "Lectura ignorada: cierre la ventana para escanear" (FR-008) |
| `*` con el campo vacío | Abre la cantidad si en 60 ms no llega otro carácter; si llega, es parte de una lectura |
| Coincidencia exacta | Agrega con cantidad 1 o incrementa la línea (FR-010) |
| Código de barras de un producto y SKU de otro | Selector existente con nombre, SKU, precio (FR-013) |
| Sin coincidencias, formato `Unrecognized` | Aviso: "Código no válido: {texto}. F2: buscar por nombre o SKU" |
| Sin coincidencias, otro formato | Aviso: "Código no encontrado: {texto}. F2: buscar por nombre o SKU" |
| Producto inactivo o borrado | Mensaje de 005 (sin cambios) |
| Lectura vacía (solo Enter) | Sin aviso (cierra el resultado de la última venta, como hoy) |

- `{texto}` se muestra normalizado.
- Los avisos usan `ShowStatus(warning: true)` y se reemplazan con la siguiente lectura o acción
  (FR-014).
- F2 con el campo vacío enfoca la captura para escribir nombre o SKU (FR-012).
- `Sale_ProductNotFound` se reemplaza por `Scan_CodeNotFound` y `Scan_CodeInvalid`; se agrega
  `Scan_IgnoredInDialog`.

## Pantalla "Probar escáner"

- **Acceso**:
  - Página `help.scanner-test`, "Probar escáner", grupo "Ayuda", orden 10, sin permiso; visible
    para todos los roles.
  - Botón "Probar escáner" en "Acerca de" que navega a ella (FR-015).
- **Encabezado**: instrucciones breves: "Escanee un código. Esta pantalla no modifica la venta."
- **Lectura actual** (la más reciente, destacada):

| Dato | Ejemplo |
|---|---|
| Texto leído | `PROD·0042`, `ABC'123`, `750[U+00A0]1` (espacios como `·`, no imprimibles como `[TAB]`/`[U+XXXX]`) |
| Formato | `EAN-13`, `EAN-8`, `CODE128 / CODE39`, `Formato no reconocido` |
| Caracteres | `13` |
| Terminó con | `Enter` ✓ / `Tab` ⚠ / `Sin Enter` ⚠ con "Configure el lector para enviar Enter al final de cada lectura" (FR-017) |
| Velocidad | `Escáner` / `Escritura manual` |
| Producto | `Coca-Cola 600 ml · SKU CC600` / `(inactivo)` / `Sin producto con este código` |

- **Historial**:
  - Las últimas 10 lecturas, de la más reciente a la más antigua, en una lista compacta con hora,
    texto, formato y producto.
  - Botón "Borrar" (FR-018).
- **Teclado**: la página atiende la entrada en túnel; Tab no mueve el foco mientras forma parte de una
  lectura. Esc o la navegación normal salen.
- No toca `PointOfSaleViewModel`, `Cart` ni el borrador (FR-019).
