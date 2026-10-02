# Data Model: Integración de escáner de código de barras

**Feature**: `021-barcode-scanner` | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

No hay tablas, columnas ni índices nuevos. Cambian las reglas de un campo existente y se agregan tipos
de dominio y de aplicación que no se guardan.

## Producto (existente, `Pos.Domain/Products/Product.cs`)

| Campo | Antes | Ahora |
|---|---|---|
| `Barcode` | Opcional; `^[0-9]{8,14}$`; recortado | Opcional; normalizado con `Barcode.Normalize` (recorte, sin `*…*`, mayúsculas); `^[A-Z0-9 .$/+%-]{1,48}$` |
| `BarcodeMinLength` / `BarcodeMaxLength` | 8 / 14 | 1 / 48 |
| Columna `Products.Barcode` | `TEXT`, `HasMaxLength(14)` | `TEXT`, `HasMaxLength(48)`; mismo índice `IX_Products_Barcode` (único, `DeletedAt IS NULL AND Barcode IS NOT NULL`) |

**Validación** (FR-003, FR-004, FR-005):

- `Product.Create`/`Update` lanzan `DomainException` si el código normalizado no cumple la regla.
- `ProductRules` (FluentValidation) muestra `ProductMessages.BarcodeFormat` en el campo `Barcode`.
- La unicidad sigue en `BarcodeExistsAsync` más el índice. Como se guarda en mayúsculas, la unicidad no
  distingue mayúsculas de minúsculas.
- Los códigos existentes (solo dígitos, 8–14) cumplen la regla nueva y no cambian al normalizarse.

**Migración** `ScannerBarcodeFormats` (research §3): solo actualiza el snapshot; sin SQL, sin
reconstrucciones ni cambios de datos. `Version` 0.15.0, base de ejemplo `v0.15.0.db` con un producto
cuyo código es `PROD-0042`.

## Barcode (nuevo, `Pos.Domain/Products/Barcode.cs`)

Clase estática con las reglas del código de barras; `Product` delega en ella.

| Miembro | Descripción |
|---|---|
| `MaxLength = 48` | Largo máximo después de normalizar |
| `string? Normalize(string? raw)` | Recorta; quita `*` inicial y final si ambos existen y quedan caracteres; mayúsculas invariantes; vacío → `null` |
| `bool IsValidForCatalog(string? normalized)` | `null` es válido (campo opcional); si no, juego de caracteres y largo de research §1 |
| `BarcodeFormat Classify(string? raw)` | Normaliza y clasifica (research §4) |
| `bool HasValidEanCheckDigit(string digits)` | Módulo 10 con pesos alternos de derecha a izquierda (3, 1, 3, …); aplica a 8 y 13 dígitos |

`Product.LooksLikeFullBarcode` (búsqueda exacta en los listados de Productos e Inventario) conserva el
patrón numérico de 8 a 14 dígitos; los códigos alfanuméricos se encuentran con la búsqueda por
contenido existente.

## BarcodeFormat (nuevo, enum en `Pos.Domain/Products/`)

| Valor | Cuándo | Texto en pantalla |
|---|---|---|
| `Empty` | Lectura vacía después de normalizar | (se ignora) |
| `Ean13` | 13 dígitos con dígito verificador correcto | `EAN-13` |
| `Ean8` | 8 dígitos con dígito verificador correcto | `EAN-8` |
| `Code128OrCode39` | Cumple el juego de caracteres y el largo, y no es un EAN con dígito incorrecto | `CODE128 / CODE39` |
| `Unrecognized` | Caracteres no admitidos, más de 48, o 8/13 dígitos con dígito verificador incorrecto | `Formato no reconocido` |

## Lectura de escáner (nuevo, no se guarda)

Vive solo en Desktop (detector y pantalla de prueba) y en el resultado de `InspectScan`.

| Campo | Tipo | Origen |
|---|---|---|
| `RawText` | `string` | Caracteres recibidos, sin el terminador |
| `Terminator` | `Enter` \| `Tab` \| `None` | Tecla que cerró la lectura; `None` = silencio de 300 ms |
| `IsBurst` | `bool` | ≥ 3 caracteres y ningún intervalo > 50 ms (research §6) |
| `ReceivedAt` | hora local | Solo para mostrar en la pantalla de prueba |
| `Format`, `Length`, `Product` | de `InspectScan` | Ver [contracts/application-ports.md](contracts/application-ports.md) |

**Ciclo de vida**: en el Punto de venta la lectura se convierte en `ScanInput(Text, IsScan)` y entra a
`ScanQueue` (en orden, FR-009). En la pantalla de prueba se agrega al inicio de una lista de máximo 10
elementos; "Borrar" la vacía y al salir de la página se pierde (FR-018).

## Venta en curso (existente, 005)

Sin cambios de modelo. `Cart.Add` ya agrega con cantidad 1 o incrementa la línea del mismo producto
(FR-010). Las lecturas en la pantalla de prueba no tienen acceso a `Cart` ni al borrador (FR-019).
