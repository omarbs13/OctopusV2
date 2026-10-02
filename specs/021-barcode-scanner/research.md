# Research: Integración de escáner de código de barras

**Feature**: `021-barcode-scanner` | **Date**: 2026-10-02 | **Plan**: [plan.md](plan.md)

Estado de partida (revisado en el código):

- `Product.Barcode` acepta `^[0-9]{8,14}$` (`Product.BarcodePattern`), se guarda recortado y es único
  entre productos no borrados (`IX_Products_Barcode`, filtrado por `DeletedAt IS NULL`).
- `FindProductsForSaleHandler` busca coincidencia exacta de SKU o código de barras
  (`IProductRepository.FindForSaleAsync`), después cupón (015) y al final por nombre; con más de una
  coincidencia exacta abre el selector (`LookupKind.NameMatches`).
- `PointOfSaleViewModel.Capture` toma el texto del campo de captura al pulsar Enter y lo encola en
  `ScanQueue` (un solo consumidor, en orden). Sin coincidencias muestra `Sale_ProductNotFound`.
- `PointOfSaleView` atiende atajos en la fase de túnel de `KeyDown`; con un modal abierto
  (`IsModalOpen`) no aplica atajos.

## §1 Reglas del código de barras en el catálogo (FR-003, FR-005)

- **Decision**: una sola regla de catálogo después de normalizar: `^[A-Z0-9 .$/+%-]{1,48}$`, sin
  espacios en los extremos (el recorte los quita). `Product.BarcodeMinLength = 1`,
  `Product.BarcodeMaxLength = 48`. El dígito verificador **no** se exige al registrar.
- **Rationale**: los códigos numéricos actuales de 8 a 14 dígitos son un subconjunto de esta regla, así
  que ningún producto existente queda inválido (FR-005). El juego de caracteres es el de CODE39, que
  también se representa en CODE128; así un código impreso en cualquiera de los dos formatos se puede
  registrar. No exigir el dígito verificador respeta los códigos internos existentes (supuesto de la
  spec).
- **Alternatives considered**: dos reglas separadas (numérica y alfanumérica) con un tipo de código en
  el producto: más campos y migración de datos sin beneficio para el operador. Todo ASCII imprimible de
  CODE128: deja pasar caracteres que suelen llegar alterados por la distribución de teclado (`'`, `?`,
  `ñ`) y que luego no se pueden escanear de forma confiable.

## §2 Normalización y comparación sin mayúsculas (FR-004)

- **Decision**: `Barcode.Normalize`: recortar extremos; si empieza y termina con `*` y tiene más de dos
  caracteres, quitar ambos asteriscos y volver a recortar; pasar a mayúsculas invariantes; vacío →
  nulo. Se guarda ya normalizado (como el SKU) y la búsqueda normaliza igual, de modo que la comparación
  en SQL sigue siendo de igualdad exacta sobre el índice existente.
- **Rationale**: guardar en mayúsculas hace que el índice único `IX_Products_Barcode` impida
  `abc-1` y `ABC-1` a la vez sin cambiar el índice ni la columna (supuesto "unicidad sin distinguir
  mayúsculas"). Los códigos existentes solo tienen dígitos: `ToUpperInvariant` no los cambia y no hace
  falta migrar datos.
- **Alternatives considered**: `COLLATE NOCASE` en la columna: obliga a reconstruir `Products` en
  SQLite. Comparar con `upper(Barcode)` en SQL: no usa el índice (SC-003 con 10 000 productos).

## §3 Migración y esquema

- **Decision**: migración `ScannerBarcodeFormats` generada por EF Core al cambiar
  `HasMaxLength(Product.BarcodeMaxLength)` de 14 a 48. Se verificó en una copia del repositorio con
  `dotnet ef migrations add`: el proveedor SQLite no genera operaciones (Up vacío, solo actualiza el
  snapshot), porque SQLite no guarda el largo de `TEXT`. **No hay reconstrucción de tablas** ni cambio
  de datos. `Version` 0.14.0 → 0.15.0 y base de ejemplo `v0.15.0.db` según
  [docs/migraciones.md](../../docs/migraciones.md), con un producto de código alfanumérico.
- **Rationale**: el modelo C# sigue siendo la fuente de verdad (Principio IV) y el snapshot queda
  sincronizado; una migración sin operaciones es inofensiva para las bases de clientes.
- **Alternatives considered**: no tocar `HasMaxLength` y dejar 14 en el modelo: el modelo mentiría
  sobre la regla de dominio y una futura generación de esquema quedaría inconsistente.

## §4 Clasificación de lecturas (FR-001, FR-002, FR-011)

- **Decision**: `Barcode.Classify(string? raw)` en Domain devuelve `BarcodeFormat`:
  1. Normalizado vacío → `Empty` (la lectura se ignora sin aviso).
  2. Caracteres fuera del juego de §1 o más de 48 → `Unrecognized`.
  3. Solo dígitos y largo 13 → `Ean13` si el dígito verificador (módulo 10 con pesos 1/3) es correcto;
     si no, `Unrecognized`.
  4. Solo dígitos y largo 8 → `Ean8` con el mismo cálculo (pesos 3/1); si no, `Unrecognized`.
  5. Cualquier otro → `Code128OrCode39` (se muestra "CODE128 / CODE39": un lector que emula teclado no
     informa la simbología, y el texto de CODE39 es también texto válido de CODE128).
- La clasificación **solo** se usa cuando no hubo coincidencia exacta (para elegir entre "Código no
  válido" y "Código no encontrado") y en la pantalla de prueba. La búsqueda exacta se hace siempre
  primero, así un código interno de 13 dígitos sin dígito verificador correcto se sigue encontrando.
- **Rationale**: cumple el caso límite de la spec ("el dígito verificador solo decide entre no válido y
  no encontrado"). El cálculo del dígito verificador es una regla con cálculo: se prueba (Principio VI).
- **Alternatives considered**: librería de códigos de barras (ZXing.Net): sirve para decodificar
  imágenes, no texto de teclado; sería una dependencia sin uso real (Principio VII).

## §5 Reconocer la lectura con el foco fuera del campo de captura (FR-006, FR-007)

- **Decision**: redirigir la escritura al campo de captura. `PointOfSaleView` atiende `TextInputEvent`
  en la fase de túnel: si no hay modal abierto y el foco **no** está en un `TextBox` (lista de líneas,
  botones, resumen), mueve el foco al campo de captura de forma síncrona, agrega el texto al final y
  marca el evento como atendido. Los caracteres siguientes llegan ya al campo de captura y el Enter
  final ejecuta `Capture`, igual que hoy.
- Si el foco está en otro `TextBox` (cantidad, efectivo recibido, búsqueda de cliente, cupón) no se
  intercepta nada (FR-007).
- **Rationale**: no depende de medir tiempos para funcionar; es el mismo camino que ya cumple el orden
  y la no mezcla de lecturas (FR-009, `ScanQueue`). Además evita que el Enter del lector "pulse" el
  botón que tenía el foco (por ejemplo "Cobrar").
- **Alternatives considered**: un búfer global independiente del campo de captura: duplica la lógica
  de `Capture`, y el texto parcial se perdería si el operador escribe a mano. Escucha a nivel de
  ventana: afectaría a otras pantallas (por ejemplo el formulario de producto, donde escanear debe
  llenar el campo de código).

## §6 Distinguir escaneo de escritura manual

- **Decision**: `ScanBurstDetector` en `Pos.Desktop/Common/Scanner/`: clase pura que recibe cada
  carácter y el Enter con una marca de tiempo monotónica (`Stopwatch.GetTimestamp`) y decide si la
  secuencia fue una ráfaga. Una lectura es **escaneo** cuando tiene al menos 3 caracteres y ningún
  intervalo entre teclas (incluido el Enter) supera **50 ms**. Una secuencia sin Enter se da por
  terminada tras **300 ms** sin teclas (para la pantalla de prueba, FR-017). Los umbrales son
  constantes, sin configuración (supuesto de la spec).
- Usos:
  - Punto de venta: `Capture` encola `ScanInput(Text, IsScan)`. Con `IsScan` el caso de uso no hace la
    búsqueda por nombre (§8).
  - Diálogos del Punto de venta (§7) y pantalla de prueba (§10).
- **Rationale**: los lectores USB envían cada carácter cada 1–15 ms y los inalámbricos rara vez pasan de
  30 ms; una persona difícilmente sostiene menos de 50 ms entre tres teclas seguidas y el Enter. Si la
  interfaz está ocupada y los intervalos medidos crecen, la lectura cae en el camino manual, que
  también busca el código exacto primero: el fallo es seguro.
- **Alternatives considered**: prefijo o sufijo configurado en el lector: exige configurar el lector
  (contra FR-001). Marca de tiempo del evento de entrada de Avalonia: no está expuesta en
  `KeyEventArgs`/`TextInputEventArgs`; el `Stopwatch` en el manejador es suficiente.
- **Pruebas**: el detector es lógica de interfaz, no regla de negocio; por la política mínima del
  Principio VI no lleva prueba unitaria. Se verifica con los escenarios de [quickstart.md](quickstart.md).

## §7 Lecturas con un diálogo abierto (FR-008)

- **Decision**: con `IsModalOpen` (selector de productos, cobro, motivo de cajón, cliente, descuento,
  cupón), el manejador de túnel del Punto de venta sigue alimentando el detector, sin interceptar los
  caracteres. Al recibir un Enter que cierra una ráfaga:
  1. marca el Enter como atendido (el diálogo no confirma ni elige nada);
  2. si el foco está en un `TextBox`, restaura el texto y la posición del cursor que tenía al empezar la
     ráfaga (el detector toma esa instantánea en el primer carácter de cada secuencia);
  3. muestra el aviso "Lectura ignorada: cierre la ventana para escanear" y lo registra en el log.
- Los diálogos en ventana propia (`DialogService`: mensajes, confirmaciones, autorización del
  Administrador) abiertos desde el Punto de venta reciben el mismo filtro: `DialogService` acepta un
  guardián opcional que el Punto de venta pasa al abrirlos. En el resto de la aplicación no se aplica
  (en el formulario de producto, escanear debe llenar el campo de código).
- **Rationale**: evita que un Enter de lector confirme un cobro o elija un producto en el selector, y
  que los dígitos escaneados queden en "efectivo recibido".
- **Alternatives considered**: bloquear todo teclado mientras hay modal: rompe la captura de efectivo.
  Encolar la lectura para procesarla al cerrar: la spec pide ignorarla y avisar.

## §8 Búsqueda por código en la venta (FR-010 a FR-014)

- **Decision**: `FindProductsForSaleQuery(Text, FromScanner)`; el orden de `FindProductsForSaleHandler`
  queda:
  1. Coincidencia exacta de SKU o código de barras normalizado (§2), incluidos inactivos y borrados
     para informar el motivo (005, sin cambios). Una coincidencia → `ExactMatch`; más de una (código de
     barras de uno y SKU de otro) → selector (FR-013, sin cambios).
  2. Cupón (015, sin cambios: un código de producto y de cupón es producto).
  3. Solo si **no** es escaneo: búsqueda por nombre (005). Un escaneo no busca por nombre: un código
     desconocido que fuera parte del código de otro producto abriría un selector equivocado.
  4. Sin resultados → `LookupKind.None` con `Format = Barcode.Classify(text)`.
- La interfaz muestra "Código no válido: {texto}" (`Unrecognized`) o "Código no encontrado: {texto}"
  (otro formato) con "F2: buscar por nombre o SKU". F2 ya existe: con el campo vacío enfoca la captura
  para escribir el nombre o el SKU (FR-012, SC-005). El aviso se reemplaza con la siguiente lectura o
  acción y no bloquea la captura (FR-014, `ShowStatus`).
- Escribir a mano un código y pulsar Enter sigue el mismo camino, con la búsqueda por nombre como
  último paso (caso límite de la spec y 005).
- **Rationale**: reutiliza el selector, `Cart.Add` (que ya incrementa la cantidad de la línea
  existente, FR-010/SC-002) y la cola ordenada (FR-009). El rendimiento no cambia: igualdad sobre
  índices de SKU y código de barras (SC-003).
- **Alternatives considered**: abrir directamente un diálogo de búsqueda manual en cada código no
  encontrado: interrumpe ráfagas de escaneo y obliga a cerrarlo.

## §9 Asterisco de CODE39 y el atajo "*" (caso límite)

- **Decision**: hoy `*` (o Shift+8) con el campo de captura vacío abre la cantidad. Para que un lector
  que envía `*ABC123*` no dispare el atajo, el `*` con el campo vacío se escribe primero como texto y
  se espera 60 ms: si llega otro carácter, era una lectura; si no, se quita y se abre la cantidad.
- **Rationale**: el operador no percibe 60 ms y el atajo de 005 se conserva.
- **Alternatives considered**: quitar el atajo `*`: cambia un hábito del mostrador documentado en 005.

## §10 Pantalla "Probar escáner" (FR-015 a FR-019)

- **Decision**:
  - Página `ScannerTestViewModel`/`ScannerTestView` en `Pos.Desktop/About/`, registrada en el grupo
    "Ayuda" (orden 10, sin permiso) y con un botón "Probar escáner" en "Acerca de" que navega a ella.
  - La página escucha `TextInputEvent` y `KeyDown` en la fase de túnel con su propio
    `ScanBurstDetector`; no tiene campo de texto donde el operador deba poner el foco.
  - Cada lectura termina con Enter, con Tab (se informa que terminó con Tab y no con Enter) o por
    silencio de 300 ms (FR-017).
  - Cada lectura consulta el caso de uso de solo lectura `InspectScan` (formato, largo y producto por
    código de barras con su estado).
  - El texto se muestra con los caracteres no imprimibles y los espacios visibles (`·`, `[TAB]`,
    `[U+XXXX]`).
  - Se conservan las últimas 10 lecturas, de la más reciente a la más antigua, con "Borrar"; viven
    solo en el ViewModel, que es *scoped*, así que se pierden al salir (FR-018).
- **Aislamiento (FR-019, SC-007)**: la página no referencia `PointOfSaleViewModel`, `Cart` ni el
  borrador. `InspectScan` solo lee y no escribe bitácora. Al salir del Punto de venta, su vista se
  desuscribe (comportamiento existente) y la venta en curso queda en el ViewModel singleton y en el
  borrador sin cambios.
- **Rationale**: el diagnóstico más común en campo es la distribución de teclado y el sufijo Enter;
  mostrar el carácter recibido y el terminador los hace evidentes sin soporte (SC-006).
- **Alternatives considered**: un diálogo modal desde "Acerca de": la spec pide una pantalla.
  Reutilizar `FindProductsForSale`: requiere permiso `Sell` y busca cupones y nombres; la prueba debe
  servir a cualquier rol.

## §11 Documentación, logging y pruebas

- **Logging (Principio VIII)**:
  - lecturas no encontradas o no válidas: nivel `Information`, con texto, formato y si fue escaneo;
  - lecturas ignoradas por diálogo abierto: nivel `Information`;
  - errores: con el contexto de `OperationRunner` (existente).
  - El código de barras no es dato sensible.
- **Docs**: `docs/escaner.md` (guía de soporte nueva: requisitos del lector, sufijo Enter,
  distribución de teclado, formatos y pantalla de prueba), `docs/ventas.md` (lectura sin foco, avisos,
  atajo `*`) y `docs/migraciones.md` (sección 0.15.0).
- **Pruebas (política mínima)**:
  - Domain: `BarcodeTests`, que cubre la regla del catálogo (válido, carácter no admitido, 49
    caracteres), la normalización (asteriscos, minúsculas, espacios) y el dígito verificador (EAN-13 y
    EAN-8 válidos e inválidos).
  - SQLite real:
    - unicidad sin distinguir mayúsculas (`abc-1` contra `ABC-1` → duplicado);
    - búsqueda para vender de un código alfanumérico escrito en minúsculas;
    - un escaneo desconocido no busca por nombre (protege que no se agregue un producto equivocado).
  - Obligatorias: `SampleDatabaseUpgradeTests` con `v0.15.0.db` y las de arquitectura sin reglas
    nuevas.
  - Sin pruebas de ViewModels, vistas ni del detector.
