# Feature Specification: Campos de proveedor de loseta en el producto

**Feature Branch**: `026-tile-supplier-fields`

**Created**: 2026-10-22

**Status**: Draft

**Input**: User description: "Ampliar el producto de OctopusCovering con los campos con los que los proveedores de loseta manejan su catálogo: MEDIDAS, MODELO, EMBALAJE, precio X M2, precio X CAJA y MARCA, conservando todos los productos ya capturados. Las listas de precios y archivos de Excel de los proveedores identifican cada loseta por MODELO y MEDIDA, con su EMBALAJE (m² por caja) y dos precios (por m² y por caja); el producto debe tener exactamente esos datos para poder importar archivos y actualizar precios automáticamente después."

## Clarifications

### Session 2026-10-22

- Q: ¿Qué es un "producto de loseta"? → A: El que tiene unidad de venta caja o m². Los de unidad pieza (boquillas, adhesivos, sanitarios, muebles, perfiles) no son loseta y conservan su precio por pieza.
- Q: ¿Cómo se redondea la sugerencia de precio? → A: Al centavo más cercano (media hacia arriba); $196.25 × 1.86 = $365.025 → $365.03.
- Q: Al vender loseta, ¿el cajero puede elegir caja o m² en cada venta? → A: Sí. La unidad de venta del producto es la modalidad inicial, pero el cajero puede cambiarla a caja o m² en cada partida, siempre que exista precio o embalaje para derivarlo.
- Q: ¿De dónde sale el precio por pieza suelta de una loseta? → A: No se deriva. Un producto de loseta puede tener un precio por pieza capturado aparte; sin él, no se vende por pieza suelta.
- Q: ¿Cómo se localizan los productos "para revisión" tras la migración? → A: Con un filtro permanente "Para revisión" en el catálogo; la marca se quita sola cuando el producto deja de tener el problema, y se muestra también una insignia en la lista.
- Q: ¿Cómo se calcula la partida de venta? → A: Cantidad × precio aplicable, redondeada al centavo (3 × $287.05 = $861.15; 4.92 × $175.03 = $861.15).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Conservar todo al actualizar la app (Priority: P1)

Quien ya usa la app tiene productos, ventas, imágenes y existencias capturados. Al actualizar, nada se pierde: los productos existentes se convierten a la nueva estructura (el nombre pasa a ser el modelo y el precio actual se reparte según su unidad) y, al terminar, la app muestra una sola vez un resumen de la migración para revisar lo que quedó incompleto.

**Why this priority**: Es una condición de no regresión. Sin ella la funcionalidad no puede entregarse a usuarios con datos reales.

**Independent Test**: Cargar una base con productos de unidad caja y pieza, con ventas e imágenes; actualizar; verificar que todo sigue presente, que los totales de reportes no cambian y que el resumen aparece una única vez.

**Acceptance Scenarios**:

1. **Given** un producto vendido por caja con precio $287.05 (hoy no tiene medidas ni m² por caja), **When** se actualiza, **Then** X CAJA = $287.05, X M2 queda vacío, Modelo = nombre anterior, Medidas y Embalaje quedan vacíos y el producto se cuenta como "sin embalaje" en el resumen.
2. **Given** un producto vendido por caja o m² cuyo nombre tiene entre 151 y 200 caracteres, **When** se actualiza, **Then** se conserva completo como Modelo y se marca "para revisión" (el límite de captura de Modelo es 150).
3. **Given** un producto vendido por pieza, **When** se actualiza, **Then** conserva su precio por pieza sin X M2 ni X CAJA.
4. **Given** ventas registradas antes de la actualización, **When** se consultan, **Then** conservan su descripción y precio originales y los totales de los reportes son idénticos.
5. **Given** la migración terminó, **When** el usuario abre la app por primera vez, **Then** ve el resumen (productos migrados, sin modelo, sin embalaje, marcados por duplicado) y no vuelve a verlo en aperturas posteriores.
6. **Given** dos productos del mismo proveedor con el mismo nombre y misma medida, **When** se migran, **Then** la actualización no se bloquea, ambos se conservan y se marcan "para revisión".

---

### User Story 2 - Capturar y editar productos con los campos del proveedor (Priority: P1)

El usuario da de alta o edita un producto capturando Modelo (obligatorio), Medidas, Embalaje (m² por caja), Piezas por caja (opcional), Precio X M2, Precio X CAJA y Marca, además de código, categoría, unidad de venta, existencias e imágenes, y opcionalmente el proveedor. Acabado y costo no se agregan en esta funcionalidad. El código se genera automáticamente si no se captura. En tableta, los campos de loseta se agrupan en la sección "Datos del proveedor" a dos columnas.

**Why this priority**: Es el núcleo del cambio; habilita la captura con la misma estructura que los archivos de los proveedores.

**Independent Test**: Crear un producto de loseta con todos los campos y otro de pieza; verificar guardado, edición y validaciones.

**Acceptance Scenarios**:

1. **Given** el formulario de alta, **When** el usuario omite el Modelo, **Then** no se guarda y se indica que es obligatorio.
2. **Given** un producto de loseta sin X M2 ni X CAJA, **When** intenta guardar, **Then** se rechaza pidiendo al menos uno de los dos precios.
3. **Given** un producto sin código, **When** se guarda, **Then** se le asigna un código único automático que el usuario puede cambiar después.
4. **Given** el Embalaje capturado como "1.80" o "1.8", **When** se guarda, **Then** ambos se tratan como el mismo valor; "NA" o vacío significa que no aplica; valores no positivos o con más de 3 decimales se rechazan.
5. **Given** modelos como "URBANIA BLACK & WHITE", "MALIBU - AD BLANCO", "BOQUICHAM ROBLE C/ARENA" o "(CALIDAD COMERCIAL)", **When** se guardan, **Then** se aceptan y se muestran tal cual.
6. **Given** un producto con X M2, X CAJA y Embalaje donde X CAJA difiere más del 2 % de X M2 × Embalaje, **When** se guarda, **Then** se muestra una advertencia sin bloquear el guardado.
7. **Given** la app en tableta, **When** se abre el formulario, **Then** Modelo, Medidas, Embalaje, Piezas por caja, X M2, X CAJA y Marca aparecen en "Datos del proveedor" a dos columnas.

---

### User Story 3 - Sugerencia de precio X CAJA / X M2 (Priority: P2)

Al capturar un precio y el embalaje, la app sugiere el otro precio; el usuario puede aceptarlo o escribir otro valor, porque los proveedores redondean.

**Why this priority**: Ahorra captura y errores, pero el producto es usable sin ella.

**Independent Test**: Capturar X M2 y Embalaje y verificar la sugerencia; repetir a la inversa.

**Acceptance Scenarios**:

1. **Given** X M2 = $196.25 y Embalaje 1.86, **When** el usuario no ha capturado X CAJA, **Then** la app sugiere X CAJA = $365.03.
2. **Given** X CAJA y Embalaje capturados y X M2 vacío, **When** el usuario ve la sugerencia, **Then** propone X CAJA ÷ Embalaje redondeado al centavo.
3. **Given** una sugerencia visible, **When** el usuario escribe otro valor, **Then** se guarda el valor escrito sin sobrescribirlo.
4. **Given** que el Embalaje no aplica, **When** se captura un precio, **Then** no se muestra sugerencia.

---

### User Story 4 - Unicidad por proveedor + modelo + medidas (Priority: P1)

La combinación proveedor + modelo + medidas identifica un producto; el modelo por sí solo no es único.

**Why this priority**: Es la clave con la que se reconocerán los productos en archivos de proveedores (importaciones futuras) y evita duplicados.

**Independent Test**: Intentar guardar duplicados y variantes con distinta medida, mayúsculas, espacios o separadores.

**Acceptance Scenarios**:

1. **Given** SUNWOOD MIEL 18X60 del proveedor A, **When** se guarda SUNWOOD MIEL 20X90 del mismo proveedor, **Then** se permite.
2. **Given** el mismo producto, **When** se guarda otro con el mismo proveedor, modelo y medida, **Then** se rechaza indicando el producto existente.
3. **Given** "33 X 33" existente, **When** se captura "33x33" con el mismo modelo ("fort wood" vs "FORT WOOD", sin importar acentos o espacios dobles), **Then** se detecta como duplicado; el valor original de cada uno se conserva y muestra tal cual.
4. **Given** "120*240" existente, **When** se captura "120X240", **Then** se detecta como duplicado.
5. **Given** productos sin proveedor, **When** se guardan, **Then** la unicidad se valida solo por modelo + medidas entre los productos sin proveedor.
6. **Given** el mismo modelo y medida bajo distintas marcas pero distinto proveedor, **When** se guardan, **Then** se permite.

---

### User Story 5 - Ver, buscar y filtrar el catálogo (Priority: P2)

La lista muestra Modelo, Medidas, Marca, Precio X M2, Precio X CAJA y existencia. El usuario busca por modelo, medida y marca, y filtra por marca y por medida.

**Why this priority**: Hace útil la nueva información en el día a día.

**Independent Test**: Con catálogo de varias marcas y medidas, buscar y filtrar.

**Acceptance Scenarios**:

1. **Given** varios FORT WOOD en distintas medidas, **When** se busca "fort wood", **Then** aparecen todos, sin distinguir mayúsculas ni acentos.
2. **Given** el catálogo, **When** se busca "60X60" o "60 x 60", **Then** aparecen productos con esa medida.
3. **Given** el catálogo, **When** se filtra por una marca y/o una medida, **Then** solo aparecen los que coinciden.

---

### User Story 6 - Venta con el precio correcto (Priority: P1)

El punto de venta usa X CAJA al vender por caja, X M2 al vender por m², y el precio por pieza para piezas sueltas y productos que no son loseta. La descripción guardada en la venta incluye modelo y medidas.

**Why this priority**: Un precio equivocado afecta directamente el dinero.

**Independent Test**: Vender 3 cajas y 4.92 m² y verificar importes.

**Acceptance Scenarios**:

1. **Given** un producto de loseta con unidad de venta caja, **When** se agrega a la venta, **Then** la modalidad inicial es caja y el cajero puede cambiarla a m² en esa partida (y viceversa).
2. **Given** X CAJA = $287.05, **When** se venden 3 cajas, **Then** la partida es $861.15.
3. **Given** X M2 = $175.03, **When** se venden 4.92 m², **Then** la partida es $861.15.
4. **Given** un producto sin precio para la modalidad elegida (p. ej. solo X M2 y venta por caja con Embalaje), **When** se agrega, **Then** se usa el precio derivado con el embalaje; si no hay forma de derivarlo, se impide la venta en esa modalidad indicando el motivo.
5. **Given** una boquilla (precio por pieza), **When** se vende, **Then** usa su precio por pieza.
6. **Given** una venta nueva, **When** se guarda, **Then** su descripción incluye modelo y medidas y copia el precio del momento.

---

### User Story 7 - Reportes de productos con las nuevas columnas (Priority: P3)

Los reportes de productos agregan las columnas Modelo, Medidas, Embalaje, X M2 y X CAJA.

**Why this priority**: Complementa; no cambia totales.

**Independent Test**: Generar un reporte de productos y verificar columnas y valores.

**Acceptance Scenarios**:

1. **Given** un reporte de productos, **When** se genera, **Then** incluye las cinco columnas nuevas con los valores del producto y vacío cuando no aplican.

---

### Edge Cases

- Loseta sin precio por pieza: no se ofrece la venta por pieza suelta.
- Producto sin proveedor: unicidad solo entre productos sin proveedor.
- Medidas en metros ("1.22 X 2.44 X .03") o con tres dimensiones se aceptan como texto de medida y se normalizan igual.
- Medidas vacías: se permite; la unicidad considera la medida vacía como un valor más.
- Embalaje "NA": equivale a vacío; en el resumen cuenta como sin embalaje.
- Piezas por caja sin Embalaje (y viceversa) es válido; son campos independientes (ej. LVT/SPC: 20 piezas, 5.57 m²).
- Nombres repetidos en la migración que chocan por unicidad: se marcan para revisión sin bloquear; mientras estén marcados no impiden su edición, y al corregirlos se quita la marca.
- Un producto marcado para revisión puede seguir vendiéndose.
- Cambiar el embalaje de un producto no modifica ventas pasadas.
- Productos cuyo nombre actual esté vacío o no sirva como modelo: se cuentan como "sin modelo" en el resumen y se marcan para revisión.
- Si la unidad de venta de un producto cambia de pieza a caja/m², se exige al menos uno de los dos precios.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST permitir capturar y editar en cada producto: Modelo (obligatorio, hasta 150 caracteres), Proveedor (opcional), Medidas, Embalaje (m² por caja), Piezas por caja (opcional), Precio X M2, Precio X CAJA y Marca, además de los campos existentes. Modelo, Medidas, Marca, Embalaje y Proveedor son campos nuevos del producto.
- **FR-002**: El sistema MUST usar en toda la interfaz y los documentos los nombres "Medidas" y "Embalaje" (no existe hoy un "Formato/Medida" ni un "M² por caja" que renombrar).
- **FR-003**: El sistema MUST guardar el texto original de Modelo y Medidas y mostrarlo tal cual.
- **FR-004**: El sistema MUST comparar Modelo y Medidas de forma normalizada: mayúsculas, sin acentos, espacios múltiples reducidos; en Medidas, "X", "x" y "*" equivalen y los espacios se ignoran.
- **FR-005**: El sistema MUST impedir guardar un producto cuya combinación proveedor + modelo + medidas normalizados ya exista; para productos sin proveedor, la comparación es solo modelo + medidas entre productos sin proveedor.
- **FR-006**: El sistema MUST aceptar Embalaje como número positivo con hasta 3 decimales; vacío o "NA" significa que no aplica.
- **FR-007**: El sistema MUST exigir al menos uno de X M2 o X CAJA en productos de unidad caja o m²; los de unidad pieza usan un precio por pieza.
- **FR-008**: El sistema MUST mostrar una advertencia no bloqueante cuando X CAJA difiera más del 2 % de X M2 × Embalaje (con ambos precios y Embalaje presentes).
- **FR-009**: El sistema MUST sugerir X CAJA = X M2 × Embalaje (y X M2 = X CAJA ÷ Embalaje) redondeado al centavo, sin sobrescribir lo que el usuario escriba.
- **FR-010**: El sistema MUST guardar los precios en centavos, como el resto de la app.
- **FR-011**: El sistema MUST generar automáticamente un código único cuando el producto no lo traiga, y permitir cambiarlo conservando la unicidad del código.
- **FR-012**: La lista de productos MUST mostrar Modelo, Medidas, Marca, Precio X M2, Precio X CAJA y existencia.
- **FR-013**: El sistema MUST permitir buscar por modelo, medida y marca (normalizado: sin distinguir mayúsculas, acentos ni separadores de medida) y filtrar por marca y por medida.
- **FR-014a**: Un producto de loseta MUST poder tener además un precio por pieza capturado; sin él, el sistema MUST impedir venderlo por pieza suelta (Piezas por caja no se usa para derivar precios).
- **FR-014**: El punto de venta MUST iniciar cada partida de loseta con la unidad de venta del producto y permitir cambiarla a caja o m² en esa partida cuando exista precio o embalaje para derivarlo; MUST usar X CAJA para ventas por caja, X M2 para ventas por m² y el precio por pieza para piezas sueltas y productos que no son loseta; la partida es cantidad × precio redondeada al centavo.
- **FR-015**: Las ventas nuevas MUST guardar una descripción que incluya modelo y medidas; las ventas ya registradas MUST permanecer sin cambios.
- **FR-016**: La actualización MUST migrar sin pérdida: Modelo = nombre actual (completo, aunque pase de 150 caracteres); precio actual → X CAJA si la unidad era caja, o se conserva como precio por pieza en los demás casos; Medidas, Embalaje, Marca y Proveedor quedan vacíos. Los productos de caja sin Embalaje no tienen cómo calcular X M2 y se cuentan en el resumen.
- **FR-017**: La migración MUST marcar para revisión los productos que choquen en la regla de unicidad, sin bloquear la actualización ni eliminar ninguno.
- **FR-017a**: El catálogo MUST ofrecer un filtro permanente "Para revisión" y mostrar una insignia en los productos marcados; la marca se pone solo en la migración (nombre repetido, sin modelo válido, más de 150 caracteres o loseta sin embalaje) y MUST quitarse al guardar el producto cuando su combinación proveedor + modelo + medidas sea única y tenga modelo válido; guardar una loseta sin embalaje confirma que el embalaje no aplica y no la vuelve a marcar.
- **FR-018**: Al terminar la migración, la app MUST mostrar una sola vez un resumen: productos migrados, sin modelo, sin embalaje y marcados por duplicado.
- **FR-019**: Todos los productos, ventas, imágenes, existencias y totales de reportes previos MUST permanecer intactos tras la actualización.
- **FR-020**: Los reportes de productos MUST agregar las columnas Modelo, Medidas, Embalaje, X M2 y X CAJA.
- **FR-021**: En tableta, el formulario MUST agrupar los campos de loseta en la sección "Datos del proveedor" a dos columnas.
- **FR-022**: Los respaldos y restauraciones existentes MUST incluir los nuevos campos y restaurar respaldos anteriores a esta versión aplicando las mismas reglas de conversión.

### Key Entities

- **Producto**: Artículo del catálogo. Atributos nuevos o renombrados: Modelo (obligatorio), Medidas, Embalaje (m²/caja, opcional), Piezas por caja (opcional), Precio X M2, Precio X CAJA, Marca, precio por pieza, marca de "para revisión". Se identifica frente a un proveedor por proveedor + modelo + medidas.
- **Proveedor**: Fuente del producto (spec de proveedores existente); parte de la clave de unicidad.
- **Partida de venta**: Línea con copia de descripción (ahora con modelo y medidas), cantidad y precio del momento.
- **Resumen de migración**: Conteos mostrados una sola vez tras la actualización.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tras actualizar, el 100 % de los productos, ventas, imágenes y existencias previos siguen presentes y los totales de reportes son idénticos a los de antes.
- **SC-002**: El 100 % de los intentos de guardar un segundo producto con el mismo proveedor, modelo y medida (con cualquier variación de mayúsculas, acentos, espacios o separador "X/x/*") se rechaza, y el 100 % de las variantes con medida distinta se acepta.
- **SC-003**: Con X M2 = $196.25 y Embalaje 1.86, la sugerencia es X CAJA = $365.03.
- **SC-004**: 3 cajas a $287.05 y 4.92 m² a $175.03 producen partidas de $861.15.
- **SC-005**: La búsqueda "fort wood" devuelve todos los FORT WOOD de cualquier medida en menos de 1 segundo con un catálogo de 5,000 productos.
- **SC-006**: Un usuario captura un producto de loseta completo en menos de 2 minutos.
- **SC-007**: El resumen de migración aparece exactamente una vez y sus conteos coinciden con los datos migrados.
- **SC-008**: Ningún producto con nombres repetidos se pierde ni bloquea la actualización.

## Assumptions

- "Spec 1" y "spec 3" del texto original corresponden a las especificaciones existentes de productos y proveedores en este repositorio (001 y 020, con mejoras del catálogo en 003); los reportes de productos son los de la especificación de reportes (009).
- El valor de Modelo en la migración es el nombre actual; el campo "nombre" del producto se mantiene para compatibilidad y la descripción visible puede combinar modelo y medidas.
- Hoy el producto no tiene Marca, Acabado, Costo, Medidas, Embalaje ni proveedor. Esta funcionalidad crea Marca, Medidas, Embalaje y Proveedor; Acabado y Costo quedan fuera (el costo ya se guarda por compra en la especificación de compras).
- Se agrega la unidad de venta "Metro cuadrado" al catálogo fijo de unidades.
- La app funciona sin conexión (principio de la constitución); nada de esta función requiere red.
- La importación de archivos de proveedores y el historial de precios quedan fuera de alcance, pero la clave proveedor + modelo + medidas se diseña para usarse en ellos.
- El permiso para editar productos es el existente; no se crean permisos nuevos.
- La unicidad se valida con el valor normalizado; un producto con medidas vacías se trata como un valor de medida vacío.
