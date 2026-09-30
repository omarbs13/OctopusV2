# Feature Specification: Mejoras al catálogo de Productos

**Feature Branch**: `003-product-catalog-improvements`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "Mejoras al catálogo de Productos: imagen, paginación, validaciones
reforzadas y corrección del filtro de inactivos. Hacer el catálogo de Productos apto para uso real
con catálogos grandes y corregir el defecto del filtro de inactivos."

## Contexto y objetivo

Parte de la fundación (`001-pos-foundation`: gestión básica de Productos, respaldos automáticos y
exportación de diagnóstico) y de la estructura de navegación y formularios
(`002-navigation-forms`). El catálogo debe soportar miles de productos sin volverse lento, validar
con más rigor los datos que usarán las ventas, permitir identificar cada producto con una imagen y
corregir un defecto del filtro "Mostrar inactivos".

**Usuarios**:

- **Operador del POS**: consulta, registra y mantiene el catálogo de productos.
- **Soporte técnico**: restaura respaldos y analiza paquetes de diagnóstico.

**Cambios respecto a la fundación** (esta especificación prevalece sobre `001-pos-foundation` en
estos puntos):

- El precio de venta debe ser **mayor que 0** (antes se aceptaba 0).
- La captura del precio acepta **separador de miles** con coma ("1,234.50"); antes cualquier coma
  se rechazaba. La coma como separador decimal ("12,50") se sigue rechazando.
- El producto gana dos datos: **unidad de medida** (obligatoria) e **imagen** (opcional).
- El listado deja de mostrar un número máximo de resultados y pasa a mostrarse **por páginas**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Corrección: mostrar inactivos (Priority: P1)

Como operador, quiero que el filtro "Mostrar inactivos" realmente incluya los productos inactivos
en el listado, para poder encontrarlos y reactivarlos.

**Defecto actual**: al activar "Mostrar inactivos" solo aparece la columna de estado, pero los
productos inactivos no se incluyen en el listado.

**Why this priority**: es un defecto de una funcionalidad ya entregada; según la constitución, los
defectos se corrigen antes de agregar funcionalidad nueva y cada corrección incluye una prueba que
lo reproduce.

**Independent Test**: con un catálogo que tiene productos activos, inactivos y borrados, se activa
y desactiva el filtro, con y sin texto de búsqueda, y se verifica qué productos aparecen.

**Acceptance Scenarios**:

1. **Given** productos activos, inactivos y borrados, **When** el operador abre el listado con el
   filtro desactivado (valor por defecto), **Then** solo aparecen los activos.
2. **Given** el mismo catálogo, **When** el operador activa "Mostrar inactivos", **Then** aparecen
   activos e inactivos, la columna de estado es visible y los inactivos se distinguen visualmente
   de los activos; los borrados no aparecen.
3. **Given** un producto activo "Leche entera" y uno inactivo "Leche deslactosada", **When** el
   operador busca "leche" con el filtro activado, **Then** aparecen ambos; con el filtro
   desactivado, solo "Leche entera".
4. **Given** un producto borrado que coincide con la búsqueda, **When** el operador busca con el
   filtro activado o desactivado, **Then** el producto borrado nunca aparece.
5. **Given** más de 100 productos inactivos, **When** el operador activa el filtro y navega entre
   páginas, **Then** los inactivos aparecen en todas las páginas que les correspondan, y el total
   de registros los incluye.
6. **Given** la suite de pruebas, **When** se ejecuta, **Then** incluye una prueba que reproduce el
   defecto (fallaría con el comportamiento anterior) y pasa con la corrección.

---

### User Story 2 - Listado por páginas (Priority: P1)

Como operador, quiero que el listado cargue por páginas, para que sea rápido aunque haya miles de
productos.

**Why this priority**: sin paginación, un catálogo real de miles de productos vuelve lenta la
pantalla o deja productos fuera del alcance del operador.

**Independent Test**: con 10,000 productos de prueba, se navega a primera, anterior, siguiente y
última página, se busca y se cambia el filtro de inactivos, midiendo tiempos y verificando los
indicadores de página.

**Acceptance Scenarios**:

1. **Given** 250 productos activos, **When** el operador abre el listado, **Then** se muestran los
   primeros 100 ordenados por nombre ascendente y se indica "250 registros", página 1 de 3.
2. **Given** el listado en la página 1 de 3, **When** el operador elige "siguiente", **Then** se
   muestran los registros 101 a 200 y se indica página 2 de 3; **When** elige "última", **Then**
   se muestran los registros 201 a 250 y se indica página 3 de 3.
3. **Given** el listado en la página 1, **Then** "primera" y "anterior" no están disponibles;
   **Given** el listado en la última página, **Then** "siguiente" y "última" no están disponibles.
4. **Given** el listado en la página 3, **When** el operador escribe un texto de búsqueda o cambia
   el filtro "Mostrar inactivos", **Then** el listado vuelve a la página 1 del nuevo resultado.
5. **Given** un catálogo de 10,000 productos, **When** el operador cambia de página o busca,
   **Then** el resultado aparece en menos de 1 segundo.
6. **Given** una búsqueda sin coincidencias, **When** se ejecuta, **Then** se indica explícitamente
   que no hay resultados, con 0 registros y sin navegación disponible.
7. **Given** el operador en la página 2, **When** edita un producto y guarda, **Then** el listado se
   actualiza y permanece en la página 2 si sigue existiendo.

---

### User Story 3 - Validaciones reforzadas (Priority: P1)

Como operador, quiero que el formulario de producto me indique con precisión qué datos faltan o
son incorrectos, para no registrar productos que después causen errores al vender.

**Why this priority**: los datos del catálogo alimentarán las ventas; un precio en cero o sin
unidad de medida generaría cobros incorrectos.

**Independent Test**: se captura el formulario con cada dato faltante o inválido y con los valores
límite del precio, verificando el mensaje junto a cada campo y que no se guarda nada.

**Acceptance Scenarios**:

1. **Given** el formulario de producto, **When** el operador lo abre, **Then** nombre, SKU, precio
   de venta y unidad de medida están marcados visualmente como obligatorios.
2. **Given** el formulario sin nombre, SKU, precio o unidad de medida, **When** el operador guarda,
   **Then** junto a cada campo faltante aparece un mensaje en español que indica que es
   obligatorio, no se guarda nada y lo capturado se conserva.
3. **Given** un precio "0" o "0.00", **When** el operador guarda, **Then** se rechaza indicando que
   el precio debe ser mayor que 0.
4. **Given** un precio "0.01", "999999.99" o "999,999.99", **When** el operador guarda, **Then** se
   acepta y se guarda exactamente ese valor.
5. **Given** un precio "1000000" o "999999.991", **When** el operador guarda, **Then** se rechaza
   con un mensaje específico (máximo permitido o máximo 2 decimales) y el valor no se redondea.
6. **Given** un precio "1,234.50", **When** el operador guarda, **Then** se guarda 1234.50;
   **Given** "1234.50", **Then** se guarda el mismo valor.
7. **Given** un precio "12,50", "1,23.45" o "$10", **When** el operador guarda, **Then** se rechaza
   indicando los formatos válidos ("1234.50" o "1,234.50").
8. **Given** un nombre, SKU o código de barras que incumple las reglas de la fundación, **When** el
   operador guarda, **Then** se rechaza con el mismo criterio que en la fundación y un mensaje
   específico junto al campo.

---

### User Story 4 - Imagen del producto (Priority: P2)

Como operador, quiero asignar una imagen a cada producto, para identificarlo visualmente.

**Why this priority**: mejora la identificación de productos, pero el catálogo es utilizable sin
imágenes; por eso va después del defecto, la paginación y las validaciones.

**Independent Test**: se asigna una imagen a un producto, se verifica la miniatura en el listado y
la vista previa en el formulario, se reemplaza, se quita, se intenta cargar archivos inválidos y se
restaura un respaldo que contiene un producto con imagen.

**Acceptance Scenarios**:

1. **Given** el formulario de un producto, **When** el operador selecciona un archivo JPG, PNG o
   WEBP válido de hasta 5 MB, **Then** se muestra una vista previa de la imagen antes de guardar.
2. **Given** una imagen seleccionada, **When** el operador guarda, **Then** el producto queda con su
   imagen, optimizada, y en el listado aparece su miniatura.
3. **Given** un producto con imagen, **When** el operador selecciona otra imagen y guarda, **Then**
   la nueva imagen reemplaza a la anterior en la vista previa y en la miniatura.
4. **Given** un producto con imagen, **When** el operador elige quitarla y guarda, **Then** el
   producto queda sin imagen y el listado muestra un indicador neutro en lugar de la miniatura.
5. **Given** una imagen seleccionada o quitada, **When** el operador cancela sin guardar, **Then**
   el producto conserva la imagen que tenía.
6. **Given** un archivo de más de 5 MB, con formato distinto de JPG, PNG o WEBP, o dañado (por
   ejemplo con extensión ".jpg" pero contenido inválido), **When** el operador lo selecciona,
   **Then** se rechaza con un mensaje claro que indica el motivo y el producto no cambia.
7. **Given** un producto con imagen incluido en un respaldo automático, **When** se restaura ese
   respaldo, **Then** el producto conserva su imagen y su miniatura.
8. **Given** productos con imagen, **When** soporte exporta el diagnóstico, **Then** el paquete no
   contiene las imágenes de los productos.

---

### Edge Cases

- Productos existentes sin unidad de medida: al actualizar la aplicación se les asigna la unidad
  "Pieza" automáticamente, sin intervención del operador.
- Productos existentes con precio 0 (válidos en la fundación): siguen apareciendo y pueden
  consultarse, pero al editarlos no se pueden guardar hasta capturar un precio mayor que 0.
- El operador está en la última página y borra su único registro: el listado pasa a la nueva
  última página válida.
- Otra operación agrega o borra productos mientras el operador navega: los totales y páginas se
  recalculan en cada carga; nunca se muestra una página fuera de rango.
- Varios productos con el mismo nombre: el orden entre ellos es estable (no cambian de página al
  navegar ida y vuelta).
- Espacios al inicio o final del precio: se recortan antes de validar.
- Precio con separadores de miles mal agrupados ("1,23.45", "12,3456"): se rechaza.
- Imagen muy grande en dimensiones pero menor a 5 MB: se acepta y se reduce al tamaño máximo al
  guardar.
- Imagen muy pequeña: se acepta sin ampliarse.
- Imagen PNG o WEBP con transparencia: la transparencia se conserva o se muestra sobre un fondo
  neutro, nunca como fondo negro.
- WEBP animado: se usa solo el primer cuadro.
- Falla al guardar la imagen (por ejemplo, disco lleno): no se guarda ningún cambio del producto,
  se informa al operador y lo capturado se conserva.
- La imagen de un producto falta o está dañada al mostrarla: se muestra el indicador neutro, se
  registra en el log y el listado sigue funcionando.
- Un producto borrado conserva su imagen junto con su registro.

## Requirements *(mandatory)*

### Functional Requirements

**Filtro de inactivos (corrección)**

- **FR-001**: Con el filtro "Mostrar inactivos" desactivado (valor por defecto), el listado y la
  búsqueda MUST incluir solo productos activos.
- **FR-002**: Con el filtro activado, el listado y la búsqueda MUST incluir productos activos e
  inactivos, mostrar la columna de estado y distinguir visualmente a los inactivos (por ejemplo,
  atenuados y con la etiqueta "Inactivo").
- **FR-003**: Los productos borrados MUST NOT aparecer nunca en el listado ni en la búsqueda,
  independientemente del filtro.
- **FR-004**: El filtro MUST combinarse con la búsqueda y la paginación: el total de registros, el
  total de páginas y el contenido de cada página reflejan el filtro y el texto buscado.
- **FR-005**: MUST existir una prueba automática que reproduzca el defecto y verifique la
  corrección.

**Paginación**

- **FR-006**: El listado de productos MUST mostrarse en páginas de 100 registros.
- **FR-007**: El listado MUST mostrar el total de registros, la página actual y el total de páginas.
- **FR-008**: El operador MUST poder navegar a la primera, anterior, siguiente y última página;
  las opciones que no aplican a la página actual MUST mostrarse no disponibles.
- **FR-009**: Al cambiar el texto de búsqueda o el filtro de inactivos, el listado MUST volver a la
  primera página.
- **FR-010**: El orden por defecto MUST ser por nombre ascendente, sin distinguir mayúsculas ni
  acentos, con un criterio de desempate estable para nombres iguales.
- **FR-011**: El sistema MUST obtener solo los registros de la página solicitada y el conteo total,
  sin cargar el catálogo completo en memoria.
- **FR-012**: Tras registrar, editar o borrar un producto, el listado MUST permanecer en la página
  actual si sigue existiendo; si deja de existir, MUST mostrar la última página válida. Se
  mantiene el comportamiento de la fundación de mostrar y seleccionar el producto recién guardado.
- **FR-013**: Las reglas de búsqueda de la fundación (coincidencias por nombre, SKU y código de
  barras) MUST mantenerse sin cambios.

**Validaciones**

- **FR-014**: Nombre, SKU, precio de venta y unidad de medida MUST ser obligatorios y estar marcados
  visualmente como tales en el formulario.
- **FR-015**: El precio de venta MUST ser mayor que 0, con máximo 2 decimales y máximo 999,999.99,
  en pesos mexicanos. Valores fuera de estas reglas se rechazan; nunca se redondean en silencio.
- **FR-016**: La captura del precio MUST aceptar dígitos con un punto decimal opcional, con o sin
  separador de miles con coma en grupos de 3 (por ejemplo "1234.50" y "1,234.50"). MUST rechazar la
  coma como separador decimal, los separadores mal agrupados, los símbolos de moneda y cualquier
  otro carácter.
- **FR-017**: La unidad de medida MUST elegirse de un catálogo fijo: Pieza, Kilogramo, Gramo,
  Litro, Mililitro, Metro, Caja y Paquete. Un producto nuevo propone "Pieza" como valor inicial.
- **FR-018**: Las reglas de nombre, SKU y código de barras de la fundación (FR-010 a FR-012 de
  `001-pos-foundation`) MUST mantenerse sin cambios.
- **FR-019**: Cada mensaje de validación MUST estar en español, indicar la regla específica
  incumplida (por ejemplo "El precio debe ser mayor que 0", "El precio admite máximo 2 decimales")
  y aparecer junto al campo afectado, conservando lo capturado.
- **FR-020**: Las validaciones MUST aplicarse igual al registrar y al editar.

**Imagen del producto**

- **FR-021**: Cada producto MUST poder tener cero o una imagen, seleccionada desde un archivo del
  equipo.
- **FR-022**: El sistema MUST aceptar solo archivos JPG, PNG o WEBP de hasta 5 MB, identificando el
  formato por su contenido y no solo por la extensión.
- **FR-023**: El sistema MUST rechazar un archivo inválido, dañado, de otro formato o mayor a 5 MB
  con un mensaje claro que indique el motivo, sin modificar el producto.
- **FR-024**: Al guardar, el sistema MUST optimizar la imagen reduciéndola, si excede, a un máximo
  de 1024 píxeles en su lado mayor sin deformarla, y MUST generar una miniatura para el listado.
- **FR-025**: El formulario MUST mostrar una vista previa de la imagen actual o seleccionada y
  permitir reemplazarla y quitarla. Los cambios de imagen se aplican solo al guardar el producto;
  cancelar los descarta.
- **FR-026**: El listado MUST mostrar la miniatura de cada producto con imagen y un indicador
  neutro para los productos sin imagen.
- **FR-027**: Guardar un producto con cambios de imagen MUST ser una operación completa: se guardan
  juntos los datos y la imagen, o no se guarda nada.
- **FR-028**: Las imágenes MUST incluirse en los respaldos automáticos y restaurarse junto con la
  base al restaurar un respaldo, de modo que cada producto recupere la imagen que tenía en el
  momento del respaldo.
- **FR-029**: El paquete de exportación de diagnóstico MUST NOT incluir las imágenes de los
  productos.
- **FR-030**: Las imágenes que dejan de estar asociadas a un producto (reemplazadas o quitadas)
  MUST NOT acumularse indefinidamente en el equipo.
- **FR-031**: Todas las funciones de esta especificación MUST operar sin conexión a internet.

### Key Entities *(include if feature involves data)*

- **Producto** (ampliado): se agregan la unidad de medida (obligatoria) y una referencia opcional a
  su imagen. Mantiene los atributos y la auditoría de la fundación.
- **Unidad de medida**: catálogo fijo con las unidades de FR-017; cada producto tiene exactamente
  una.
- **Imagen de producto**: imagen optimizada y su miniatura, asociadas a un solo producto. Se
  conserva mientras el producto la tenga asignada, incluso si el producto está borrado.
- **Página de productos**: resultado de una consulta con número de página, tamaño (100), total de
  registros, total de páginas y los productos de esa página.
- **Respaldo de base de datos** (ampliado): los respaldos automáticos incluyen las imágenes de los
  productos; el respaldo del paquete de diagnóstico no.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En el 100 % de las pruebas, con el filtro desactivado solo aparecen activos; activado
  aparecen activos e inactivos; los borrados nunca aparecen, con y sin búsqueda, en cualquier
  página.
- **SC-002**: Con un catálogo de 10,000 productos, cambiar de página, buscar o cambiar el filtro
  muestra el resultado en menos de 1 segundo en el equipo de referencia de la fundación.
- **SC-003**: Cada regla de validación (obligatoriedad, precio mayor que 0, máximo 2 decimales,
  máximo 999,999.99, formatos de captura con y sin separador de miles, unidad de medida) tiene
  pruebas automáticas, incluidos los casos límite 0, 0.01, 999,999.99, 1,000,000 y 3 decimales.
- **SC-004**: Un producto puede guardarse con imagen, mostrarse con su miniatura, cambiar de imagen
  y quedarse sin imagen; tras restaurar un respaldo automático, el 100 % de los productos con imagen
  la conservan.
- **SC-005**: El 100 % de los archivos inválidos de las pruebas (formato no permitido, mayor a
  5 MB, dañado o con extensión engañosa) se rechazan con un mensaje claro y sin cambiar el
  producto.
- **SC-006**: El paquete de diagnóstico exportado no contiene ninguna imagen de producto.
- **SC-007**: La suite completa pasa sin errores ni advertencias en Windows y Linux, incluida la
  prueba de actualización desde la base de ejemplo de la versión anterior (productos existentes
  quedan con unidad "Pieza" y sin imagen, sin pérdida de datos).

## Assumptions

- El tamaño de página es fijo (100); el operador no puede cambiarlo en esta funcionalidad.
- El orden del listado es fijo (nombre ascendente); ordenar por otras columnas queda fuera de
  alcance.
- La página actual forma parte del estado de pantalla que `002-navigation-forms` conserva durante
  la sesión al salir y regresar; no se conserva entre reinicios.
- El catálogo de unidades de medida es fijo y no editable por el operador en esta funcionalidad;
  la relación de la unidad con ventas por peso o báscula se definirá con Ventas.
- Tamaños de imagen: máximo 1024 px en el lado mayor para la imagen optimizada; la miniatura tiene
  un tamaño adecuado para una fila del listado (alrededor de 64 px).
- Las imágenes aumentan el tamaño de los respaldos automáticos; con imágenes optimizadas se
  considera aceptable para catálogos de algunos miles de productos.
- Fuera de alcance: captura desde cámara, varias imágenes por producto, edición de imágenes
  (recorte, rotación, filtros), arrastrar y soltar archivos y pegar imágenes desde el
  portapapeles.
- Los respaldos previos a cambios de esquema protegen la base; las imágenes no cambian con las
  migraciones de esquema.
