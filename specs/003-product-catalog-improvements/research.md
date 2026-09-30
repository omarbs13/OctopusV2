# Research: Mejoras al catálogo de Productos

Decisiones técnicas de la Fase 0 para [plan.md](plan.md). Parten del código actual de la
fundación (`001-pos-foundation`) y de navegación y formularios (`002-navigation-forms`):
`ProductRepository.SearchAsync`, `SearchProductsHandler` (límite de 200 filas), `Money`,
`ProductRules`, `SqliteBackupService`, `ZipDiagnosticsExporter`, `FieldLabel` y `FormHost`.

## 1. Causa del defecto "Mostrar inactivos"

> **Causa confirmada (T014, 2026-09-29)**: en un árbol de trabajo sobre el commit `4058f46`
> (código anterior a la paginación), con una prueba de `ProductsViewModel`:
>
> - Con 1 activo y 1 inactivo, al marcar la casilla el inactivo aparece: el enlace de la casilla y
>   la búsqueda funcionan.
> - Con 210 activos y 1 inactivo llamado "ZZ Inactivo", al marcar la casilla el inactivo **no**
>   aparece: queda después de la fila 200 del corte.
>
> Se descartan el enlace de la casilla y la carrera entre búsquedas. La corrección es la
> paginación, verificada por `ProductsViewModelInactiveFilterTests` y `ProductPagingTests`.

- **Hallazgo**: el filtro de la consulta es correcto (`ProductRepository.SearchAsync` solo agrega
  `IsActive` cuando `IncludeInactive` es falso, y hay pruebas de repositorio que lo cubren). La
  causa más probable está en el límite de 200 filas de `SearchProductsHandler`. El listado se
  ordena por nombre y se corta en la fila 200, así que en un catálogo real con más de 200
  productos los inactivos cuyo nombre queda después de esa fila nunca aparecen. Lo único que el
  operador ve cambiar es la columna de estado. Además, ninguna prueba de `ProductsViewModel`
  ejercita la casilla.
- **Decision**: primero se escribe la prueba que reproduce el defecto, a nivel de
  `ProductsViewModel` y con SQLite real. El escenario: más de 200 productos activos y un producto
  inactivo cuyo nombre queda después del corte; al activar la casilla, el inactivo debe estar en
  el resultado (en la página que le corresponda) y en el total. Si la prueba no falla con el
  código actual, se busca la causa en el enlace de la casilla y en la carrera entre búsquedas
  (`_searchVersion`) antes de continuar. La corrección definitiva es la paginación (§2): desaparece
  el corte y el total refleja el filtro.
- **Rationale**: constitución, Principio VI ("todo defecto corregido incluye una prueba que lo
  reproduce"). Escribir la prueba antes de corregir confirma la causa en lugar de suponerla.
- **Alternatives considered**: subir el límite a 1,000 (solo aplaza el defecto); ordenar los
  activos primero (oculta aún más a los inactivos y cambia el orden pedido).

## 2. Paginación en la base de datos

- **Decision**:
  - `SearchProductsQuery(Text, IncludeInactive, Page, LocateProductId?)` devuelve
    `ProductPage(Items, TotalCount, Page, PageSize = 100, TotalPages)`.
  - El repositorio ejecuta sobre la misma consulta filtrada un `COUNT(*)` y un
    `ORDER BY NameSearch, Sku LIMIT 100 OFFSET (Page-1)*100`.
  - Si la página pedida supera el total, el caso de uso devuelve la última página válida. Con 0
    resultados devuelve la página 1 de 1, vacía.
  - `LocateProductId`: si el producto recién guardado es visible con los filtros actuales, el
    caso de uso calcula la página que lo contiene: cuenta cuántos productos visibles van antes
    que él por `(NameSearch, Sku)` y divide entre 100.
  - Se elimina `HasMore` y el aviso "Se muestran los primeros 200 resultados".
- **Rationale**:
  - FR-006 a FR-012. `OFFSET` sobre 10,000 filas en SQLite tarda milisegundos. La consulta ya se
    apoya en `IX_Products_NameSearch`, un índice filtrado por no borrados.
  - El desempate por `Sku` es estable, porque el SKU es único entre no borrados y los borrados
    nunca se listan.
  - Localizar el producto guardado conserva el comportamiento de la fundación (mostrarlo y
    seleccionarlo) sin cargar el catálogo.
- **Índice**: el índice `IX_Products_NameSearch` pasa a ser compuesto `(NameSearch, Sku)`, con el
  mismo filtro, para que el orden completo salga del índice sin paso de ordenamiento. La mejora
  se mide con la prueba de rendimiento; si no se nota, el índice se deja como está.
- **Alternatives considered**: paginación por cursor o *keyset* (no permite saltar a la última
  página ni mostrar "página N de M"); cargar todo y paginar en memoria (lo prohíbe FR-011).

## 3. Precio: formatos aceptados y mensajes específicos

- **Decision**:
  - `Money.TryParse` cambia a `Money.Parse(string?) → MoneyParseResult` (valor o
    `MoneyParseError`: `Empty`, `Format`, `TooManyDecimals`, `TooLarge`).
  - La expresión acepta `^\d+(\.\d+)?$` o `^\d{1,3}(,\d{3})+(\.\d+)?$`, tras recortar espacios.
    Los decimales se evalúan aparte, para rechazar "999999.991" con un mensaje de decimales y no
    de formato. Nunca redondea.
  - La regla "mayor que 0" vive en `Product` (Domain): `Product.Create` y `Update` lanzan
    `DomainException` con precio 0. `ProductRules` la valida antes con el mensaje
    `PriceNotPositive`.
  - `Money` sigue admitiendo 0, porque es un importe genérico que usarán descuentos y totales.
- **Mensajes** (en `ProductMessages`):
  - "El precio es obligatorio."
  - "El precio debe ser mayor que 0."
  - "El precio admite máximo 2 decimales."
  - "El precio máximo es $999,999.99."
  - "Capture el precio como 1234.50 o 1,234.50."
- **Productos existentes con precio 0**: EF Core los materializa sin pasar por `Apply`, así que
  se leen y se listan sin error. Solo fallan al guardarlos (clarificación 2).
- **Rationale**: FR-015, FR-016 y FR-019; constitución, Principio III (la regla de negocio vive en
  el dominio).
- **Alternatives considered**: aceptar la coma según la cultura del sistema (ambigua entre "1,234"
  y "12,34"); poner la regla "> 0" en `Money` (impediría importes en cero legítimos en el futuro).

## 4. Unidad de medida

- **Decision**:
  - Catálogo fijo en Domain: `UnitOfMeasure` con clave, nombre y orden. Se persiste en la tabla
    `UnitsOfMeasure` sembrada con `HasData`, y `Products.UnitCode` es una clave foránea `NOT NULL`
    con valor por defecto `'H87'` (Pieza).
  - Las claves son las del catálogo de unidades del SAT: H87 Pieza, KGM Kilogramo, GRM Gramo,
    LTR Litro, MLT Mililitro, MTR Metro, XBX Caja, XPK Paquete.
- **Rationale**:
  - Clarificación 1 y FR-017. Constitución, Principio IV: los catálogos fijos se siembran con
    `HasData`.
  - Usar las claves del SAT no cuesta nada hoy y evita migrar datos si algún día se factura
    (CFDI), aunque la facturación queda fuera de alcance (Principio VII).
  - La clave foránea garantiza que la base nunca guarde una unidad desconocida.
- **Migración**:
  - SQLite no permite `ADD COLUMN ... REFERENCES` con un valor por defecto distinto de NULL, así
    que EF Core reconstruye la tabla `Products`. El SQL generado se revisa (constitución) y se
    verifica que conserva los índices filtrados.
  - La prueba de la base de ejemplo `v0.1.0.db` comprueba que los 20 productos quedan con
    `H87`, conservan precio y estado, y que no se pierde el borrado lógico.
- **Alternatives considered**: `enum` guardado como entero (legible solo con código y sin
  `HasData`); texto libre (inconsistente y contrario a la clarificación 1); columna sin clave
  foránea para evitar la reconstrucción (pierde integridad; con 10,000 filas, la reconstrucción
  tarda menos de un segundo).

## 5. Almacenamiento de imágenes: dentro de la base SQLite

- **Decision**:
  - Tabla `ProductImages` con relación 1 a 0..1 con `Products` (PK = FK `ProductId`, con
    `ON DELETE CASCADE` aunque el borrado de productos es lógico). Columnas: `Content` (BLOB, la
    imagen optimizada), `Thumbnail` (BLOB), `Width`, `Height`, `ContentType` y auditoría de
    creación y modificación.
  - La imagen forma parte del agregado Producto. Cambiarla incrementa `Product.Version` y
    `UpdatedAt` dentro de la misma transacción.
- **Rationale**:
  - FR-027: datos e imagen se guardan en una sola transacción de SQLite, sin estados a medias
    entre la base y archivos sueltos.
  - FR-028: el respaldo en línea de SQLite ya copia la base completa, así que los respaldos
    automáticos, los previos a migración y las restauraciones (incluida la de base dañada)
    conservan las imágenes sin cambiar `SqliteBackupService`.
  - FR-030: al reemplazar o quitar la imagen se actualiza o borra la fila. SQLite reutiliza las
    páginas liberadas, así que no quedan archivos huérfanos.
  - Principio V: no hay rutas de archivos adicionales.
- **Tamaño estimado**: 800 px en WEBP con calidad 80 ocupa de 40 a 90 KB, y la miniatura de
  128 px, de 3 a 6 KB. Con 5,000 productos con imagen, la base crece unos 300 a 450 MB y cada
  respaldo automático también. El respaldo en línea copia a más de 100 MB/s en disco local, así
  que el respaldo diario al arrancar agrega de 3 a 5 s en ese escenario extremo. Esto se
  documenta en `docs/carpeta-de-datos.md`.
- **Alternatives considered**: archivos en `<datos>/images/` con la ruta en la base (obliga a
  convertir los respaldos en paquetes zip, a coordinar transacción y archivos, y a limpiar
  huérfanos); imagen en una columna de `Products` (cargaría BLOBs en cada lectura del producto y
  en el listado).

## 6. Procesamiento de imágenes: SkiaSharp

- **Decision**:
  - Puerto `IImageProcessor` en Application e implementación `SkiaImageProcessor` en
    Infrastructure, con **SkiaSharp 3.119.4**.
  - El procesamiento:
    1. Rechaza archivos de más de 5 MB antes de decodificar.
    2. Identifica el formato por su firma: JPEG `FF D8 FF`, PNG `89 50 4E 47 0D 0A 1A 0A` y
       WEBP `RIFF....WEBP`.
    3. Decodifica con `SKCodec`, reduciendo la escala durante la decodificación cuando la imagen
       es muy grande.
    4. Aplica la orientación EXIF (fotos de teléfono).
    5. Reduce a un máximo de 800 px por lado sin deformar ni ampliar.
    6. Genera la miniatura de 128 px por lado.
    7. Codifica ambas en WEBP con calidad 80, conservando la transparencia.
  - En un WEBP animado se usa el primer cuadro.
  - Una imagen con dimensiones que exceden 50 megapíxeles o con un lado mayor a 20,000 px se
    rechaza como no soportada, para evitar que una imagen diseñada para agotar la memoria lo
    logre.
- **Rationale**:
  - Principio VII (cada dependencia nueva se justifica): Avalonia 12.1.3 ya trae SkiaSharp
    3.119.4 y sus binarios nativos para Windows y Linux. Referenciarlo directamente en
    Infrastructure no agrega binarios al instalador; solo fija la versión, que debe coincidir
    con la de Avalonia.
  - Tiene licencia MIT y es multiplataforma (Principio V).
  - Soporta los tres formatos de entrada y la salida WEBP.
- **Pruebas**: `Pos.Infrastructure.Tests` agrega `SkiaSharp.NativeAssets.Linux` (el paquete base ya
  trae los de Windows) para decodificar en CI en ambos sistemas. Las imágenes de prueba se generan
  en memoria con SkiaSharp; los archivos dañados o con extensión engañosa se construyen con bytes.
- **Alternatives considered**: SixLabors.ImageSharp (licencia comercial para empresas con más de
  1 M USD de ingresos); System.Drawing (solo Windows); decodificar con Avalonia en Desktop (dejaría
  una regla del producto en la capa de presentación y no se podría probar sin UI).

## 7. Flujo de selección, vista previa y guardado

- **Decision**:
  1. El editor pide el archivo con `IDialogService.PickOpenFileAsync` (nuevo; filtro `*.jpg`,
     `*.jpeg`, `*.png`, `*.webp`).
  2. Lee a lo más 5 MB + 1 byte y llama al caso de uso `PrepareProductImage`, que devuelve un
     `PreparedProductImage` o un error `InvalidImage(Reason)`.
  3. La vista previa muestra la imagen ya optimizada.
  4. Al guardar, `CreateProductCommand` y `UpdateProductCommand` llevan un `ProductImageChange`:
     `Keep`, `Replace(PreparedProductImage)` o `Remove`.
  5. Cancelar descarta el cambio.
- **Garantía**: `PreparedProductImage` tiene un constructor `internal` en Application. Solo el caso
  de uso de preparación puede crearlo, así que un comando nunca trae bytes sin validar.
- **Rationale**:
  - FR-022, FR-023 y FR-025: la validación ocurre al seleccionar, como pide el escenario 6 de la
    historia 4.
  - El procesamiento corre una sola vez.
  - FR-027: el guardado es atómico.
- **Alternatives considered**: guardar la imagen al seleccionarla (violaría "cancelar descarta");
  mandar los bytes originales en el comando y procesar al guardar (duplica el trabajo y valida
  tarde).

## 8. Miniaturas en el listado

- **Decision**:
  - `ProductListItemDto` agrega `byte[]? Thumbnail`. El repositorio hace un `LEFT JOIN` con
    `ProductImages` y proyecta solo la miniatura, nunca `Content`.
  - En Desktop, `ThumbnailConverter` convierte los bytes en `Bitmap` y muestra un ícono neutro
    cuando no hay imagen o la decodificación falla. La falla se registra una vez por producto.
  - `GetProduct` agrega `byte[]? Image` para la vista previa del editor, que se carga bajo
    demanda.
- **Rationale**: FR-026. Una página son 100 miniaturas de unos 5 KB, alrededor de 500 KB por
  página, dentro del objetivo de menos de 1 s (SC-002).
- **Alternatives considered**: cargar las miniaturas aparte y de forma diferida (más complejo, sin
  necesidad medida).

## 9. Diagnóstico sin imágenes

- **Decision**: `ZipDiagnosticsExporter` abre la copia temporal de la base antes de comprimirla y
  ejecuta `DELETE FROM ProductImages;` y luego `VACUUM;`. El respaldo del paquete conserva los
  productos, pero sin imágenes ni páginas residuales.
- **Rationale**: FR-029 y SC-006. Las imágenes pueden ser pesadas y no aportan al diagnóstico. El
  `VACUUM` garantiza que los bytes borrados no queden en páginas libres del archivo.
- **Alternatives considered**: excluir la tabla durante la copia (la API de respaldo copia la base
  completa); dejarlas (el paquete crecería cientos de MB).

## 10. Rendimiento con 10,000 productos

- **Decision**: la prueba de rendimiento se amplía:
  - 10,000 productos, 1,000 de ellos con imagen y 500 inactivos.
  - Mide la primera página, la última página, una búsqueda parcial y el cambio de filtro, cada una
    en menos de 1 s en el equipo de referencia.
  - Se ejecuta después de un calentamiento, igual que la prueba actual.
- **Rationale**: SC-002 y el criterio de aceptación 2 de la solicitud.
