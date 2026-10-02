# Feature Specification: Integración de escáner de código de barras

**Feature Branch**: `021-barcode-scanner`

**Created**: 2026-10-02

**Status**: Draft

**Input**: User description: "Integración de escáner de código de barras: calibración, multiformato y
acceso desde teclado. Objetivo: soportar lectores de código de barras en distintos formatos y que
funcionen sin configuración compleja. H1 (P3) Soporte de formatos: EAN-13, EAN-8, CODE128, CODE39;
el escáner emula un teclado; el POS reconoce un código y lo busca automáticamente. H2 (P3) Búsqueda
por código: si el código existe en un producto, lo agrega a la venta; si no existe, aviso y permite
búsqueda manual; si hay código duplicado en varios productos, muestra lista para seleccionar. H3
(P3) Calibración: pantalla de prueba 'Acerca de > Probar escáner' donde se escanea un código y se
muestra lo que lee; útil para verificar que el escáner funciona sin tocar la venta. Criterios: el
escáner añade el producto correctamente al primer escaneo; el segundo escaneo del mismo código
incrementa la cantidad; un código inválido o no encontrado muestra un aviso claro; la pantalla de
prueba funciona sin afectar la venta."

## Contexto

El Punto de venta (005) ya agrega productos al leer un código de barras con un lector que emula
teclado y termina cada lectura con Enter, incrementa la cantidad al repetir un producto y avisa
cuando el código no existe. Hoy el código de barras de un producto solo admite de 8 a 14 dígitos y
es único entre los productos no borrados (fundación y 003). Esta funcionalidad amplía lo existente:

- Admite códigos alfanuméricos de CODE128 y CODE39 en el catálogo y en la lectura.
- Reconoce la lectura del escáner en el Punto de venta aunque el foco no esté en el campo de captura.
- Distingue entre código inválido y código no encontrado, y ofrece pasar a la búsqueda manual.
- Agrega una pantalla de prueba del escáner que no toca la venta.

## Clarifications

### Session 2026-10-02

- Q: ¿Varios productos pueden compartir el mismo código de barras? → A: No. El código de barras
  sigue siendo único entre los productos no borrados; la lista de selección aparece solo cuando el
  código leído coincide con el código de barras de un producto y con el SKU de otro.

## User Scenarios & Testing *(mandatory)*

Las tres historias tienen prioridad P3 en el conjunto del producto. Dentro de esta funcionalidad se
ordenan por dependencia: el reconocimiento de formatos es la base de la búsqueda y de la pantalla de
prueba.

### User Story 1 - Soporte de formatos (Priority: P3, primera de la funcionalidad)

El dueño del negocio conecta cualquier lector de código de barras que funcione como teclado, sin
instalar ni configurar nada en el POS. El POS reconoce lecturas de EAN-13, EAN-8, CODE128 y CODE39.
El Administrador también puede registrar en un producto un código alfanumérico (CODE128 o CODE39),
no solo dígitos. En el Punto de venta, una lectura del escáner se reconoce y se busca de inmediato,
aunque el foco esté en otra parte de la pantalla.

**Why this priority**: sin reconocer los cuatro formatos, los productos con etiquetas internas o de
proveedores (que suelen usar CODE128 o CODE39) no se pueden escanear y obligan a buscar a mano.

**Independent Test**: dar de alta productos con un código EAN-13, uno EAN-8, uno CODE128
alfanumérico y uno CODE39 con guion; escanear cada uno en el Punto de venta con el foco en el campo
de captura y luego con el foco en la lista de líneas; verificar que cada lectura agrega el producto
correcto.

**Acceptance Scenarios**:

1. **Given** un producto con código EAN-13 válido, **When** el operador lo escanea en el Punto de
   venta, **Then** se reconoce el código y se agrega el producto.
2. **Given** un producto con código EAN-8, **When** se escanea, **Then** se agrega el producto.
3. **Given** un producto con código CODE128 alfanumérico (por ejemplo "ABC-12345"), **When** se
   escanea, **Then** se agrega el producto.
4. **Given** un producto con código CODE39 (por ejemplo "PROD-0042"), **When** se escanea, **Then**
   se agrega el producto.
5. **Given** el Administrador edita un producto, **When** captura un código alfanumérico que cumple
   las reglas de formato, **Then** se acepta; **When** captura un código con caracteres no
   admitidos o fuera del largo permitido, **Then** se rechaza indicando el campo con error.
6. **Given** el Punto de venta abierto con el foco en la lista de líneas o en un botón, **When** se
   escanea un código, **Then** la lectura se reconoce como escaneo y se busca, sin que el operador
   tenga que poner el foco en el campo de captura.
7. **Given** el operador edita la cantidad de una línea, **When** escribe a mano y pulsa Enter,
   **Then** el texto se trata como cantidad y no como un escaneo.

---

### User Story 2 - Búsqueda por código (Priority: P3, segunda de la funcionalidad)

Al reconocer un código, el POS lo busca entre los productos. Si lo encuentra, agrega el producto a
la venta; si el producto ya está en la venta, incrementa la cantidad de su línea. Si no lo encuentra,
muestra un aviso claro que distingue entre "código no válido" y "código no encontrado", y ofrece
pasar a la búsqueda manual con un atajo de teclado. Si el código corresponde a más de un producto,
muestra la lista para que el operador elija uno.

**Why this priority**: es el flujo diario del mostrador; un escaneo que falla en silencio o agrega
el producto equivocado cuesta dinero y tiempo al cliente.

**Independent Test**: en una venta vacía, escanear un código existente dos veces, uno inexistente,
uno ilegible o con formato inválido y uno que corresponda a dos productos; verificar en cada caso
las líneas, la cantidad, el aviso y la lista de selección.

**Acceptance Scenarios**:

1. **Given** una venta vacía, **When** se escanea el código de un producto activo, **Then** se
   agrega una línea con cantidad 1 en el primer escaneo.
2. **Given** una venta con el producto A, **When** se escanea otra vez el código de A, **Then** la
   cantidad de su línea aumenta en 1 y no se crea una línea nueva.
3. **Given** un código con formato válido que no corresponde a ningún producto, **When** se escanea,
   **Then** se muestra el aviso "Código no encontrado" con el código leído, la venta no cambia y se
   ofrece abrir la búsqueda manual.
4. **Given** una lectura que no cumple ninguno de los formatos admitidos (por ejemplo un EAN-13 con
   dígito verificador incorrecto que no corresponde a ningún producto), **When** se escanea,
   **Then** se muestra el aviso "Código no válido" con lo leído y la venta no cambia.
5. **Given** el aviso de código no encontrado, **When** el operador usa el atajo de búsqueda manual,
   **Then** se abre la búsqueda por nombre o SKU con el foco listo para escribir.
6. **Given** un código que corresponde a más de un producto, **When** se escanea, **Then** se
   muestra la lista de esos productos (nombre, SKU, precio) y solo se agrega el que el operador
   elija; si cancela, la venta no cambia.
7. **Given** un código de un producto inactivo o borrado, **When** se escanea, **Then** no se
   agrega y se informa el motivo (comportamiento de 005).
8. **Given** varios códigos escaneados muy rápido, **When** se procesan, **Then** se atienden todos
   en el orden de lectura, sin perder ni mezclar ninguno.

---

### User Story 3 - Calibración: probar el escáner (Priority: P3, tercera de la funcionalidad)

Desde "Acerca de > Probar escáner", cualquier usuario abre una pantalla de prueba. Al escanear un
código, la pantalla muestra el texto leído, el formato reconocido (o que no se reconoció), el
número de caracteres y, si existe, el producto que corresponde. Sirve para verificar que el lector
está conectado, que su distribución de teclado coincide con la del equipo y que agrega Enter al
final. Nada de lo que se escanea en esta pantalla afecta la venta en curso.

**Why this priority**: permite diagnosticar en campo un lector mal configurado sin arriesgar una
venta y sin ayuda del equipo de soporte.

**Independent Test**: con una venta en curso con dos líneas, abrir "Probar escáner", escanear tres
códigos (uno existente, uno inexistente, uno ilegible); verificar lo que muestra cada lectura;
volver al Punto de venta y verificar que la venta sigue con las mismas dos líneas y cantidades.

**Acceptance Scenarios**:

1. **Given** la pantalla "Probar escáner" abierta, **When** se escanea un EAN-13, **Then** se
   muestra el texto leído, "EAN-13", el número de caracteres y el producto asociado si existe.
2. **Given** la pantalla de prueba, **When** se escanea un código que no cumple ningún formato
   admitido, **Then** se muestra lo leído (incluidos caracteres inesperados de forma visible) y
   "Formato no reconocido".
3. **Given** un lector que no envía Enter al final, **When** se escanea, **Then** la pantalla indica
   que la lectura no terminó con Enter y que el lector debe configurarse para enviarlo.
4. **Given** una venta en curso, **When** se escanean códigos en la pantalla de prueba y se regresa
   al Punto de venta, **Then** la venta conserva exactamente sus líneas, cantidades y total.
5. **Given** varias lecturas en la pantalla de prueba, **When** se consultan, **Then** se ven las
   últimas 10 lecturas, de la más reciente a la más antigua, y se pueden borrar de la pantalla.
6. **Given** cualquier rol de usuario, **When** abre "Acerca de", **Then** encuentra "Probar
   escáner" sin necesitar permisos adicionales.

---

### Edge Cases

- Código con espacios al inicio o al final: se recortan antes de reconocer y buscar.
- CODE39 que el lector envía con los asteriscos de inicio y fin ("*ABC123*"): se quitan antes de
  buscar.
- Letras en minúsculas en un código alfanumérico: la búsqueda no distingue mayúsculas de
  minúsculas.
- Distribución de teclado distinta entre lector y equipo (por ejemplo, el guion llega como otro
  carácter): la búsqueda no encuentra el producto; la pantalla de prueba muestra el carácter
  recibido para que se note la diferencia.
- El operador teclea a mano un código en el campo de captura y pulsa Enter: se busca primero la
  coincidencia exacta de código de barras o SKU, igual que un escaneo; si no la hay, a diferencia
  del escaneo, se busca también por nombre (comportamiento de 005).
- El escaneo llega mientras hay una ventana de diálogo abierta en el Punto de venta (cobro,
  confirmación, lista de selección): la lectura no se mezcla con ese diálogo ni agrega productos
  hasta que el diálogo se cierre; el operador ve un aviso de que la lectura se ignoró.
- Un código que coincide con un producto y con un cupón: se trata como producto (regla de 015).
- Un código que coincide con el código de barras de un producto y con el SKU de otro: se muestran
  ambos en la lista para elegir (regla de 005).
- Productos existentes con códigos de 8 a 14 dígitos que no son EAN válidos (códigos internos): se
  siguen encontrando al escanearlos; el dígito verificador solo se usa para decidir entre "no
  válido" y "no encontrado" cuando no hay coincidencia.
- Lectura vacía (solo Enter): se ignora sin aviso.
- La pantalla de prueba recibe un código mientras la venta tiene un borrador pendiente de guardar:
  el borrador no cambia.

## Requirements *(mandatory)*

### Functional Requirements

**Formatos**

- **FR-001**: El sistema MUST reconocer lecturas de los formatos EAN-13, EAN-8, CODE128 y CODE39
  provenientes de un lector que emula teclado y termina cada lectura con Enter, sin instalar
  controladores ni configurar el lector dentro del POS.
- **FR-002**: El sistema MUST validar el dígito verificador de las lecturas de 13 y 8 dígitos para
  identificarlas como EAN-13 y EAN-8.
- **FR-003**: El catálogo de productos MUST admitir como código de barras, además de los códigos
  numéricos actuales de 8 a 14 dígitos, códigos alfanuméricos de 1 a 48 caracteres formados por
  letras, dígitos y los símbolos de CODE39 (espacio interior, guion, punto, $, /, + y %); el resto
  de caracteres se rechaza.
- **FR-004**: El sistema MUST normalizar cada código antes de guardarlo o buscarlo: recortar
  espacios de los extremos, quitar los asteriscos de inicio y fin de CODE39 y comparar sin
  distinguir mayúsculas de minúsculas.
- **FR-005**: Los códigos de barras ya registrados MUST seguir siendo válidos y encontrarse igual
  que antes; ningún producto existente requiere cambios.

**Reconocimiento en el Punto de venta**

- **FR-006**: En el Punto de venta, el sistema MUST reconocer una lectura del escáner aunque el foco
  no esté en el campo de captura, distinguiendo la lectura (ráfaga rápida de caracteres terminada
  en Enter) de la escritura manual.
- **FR-007**: El sistema MUST NOT tratar como escaneo lo que el operador escribe en un campo de
  edición distinto del campo de captura (cantidad, efectivo recibido, búsqueda manual).
- **FR-008**: Mientras haya un diálogo abierto en el Punto de venta, el sistema MUST ignorar las
  lecturas del escáner y avisar al operador que la lectura no se procesó.
- **FR-009**: El sistema MUST procesar las lecturas en el orden en que llegan, sin perder ni mezclar
  ninguna (regla de 005).

**Búsqueda por código**

- **FR-010**: Si el código corresponde a un único producto vendible, el sistema MUST agregarlo a la
  venta con cantidad 1, o incrementar en 1 la cantidad de su línea si ya está en la venta.
- **FR-011**: Si el código no corresponde a ningún producto, el sistema MUST mostrar un aviso que
  distinga "Código no válido" (no cumple ningún formato admitido) de "Código no encontrado"
  (formato válido sin producto), mostrar el código leído y dejar la venta sin cambios.
- **FR-012**: Junto al aviso de FR-011, el sistema MUST ofrecer abrir la búsqueda manual por nombre
  o SKU mediante un atajo de teclado visible.
- **FR-013**: Si el código corresponde a más de un producto (código de barras de uno y SKU de
  otro; el código de barras sigue siendo único entre productos), el sistema MUST mostrar una lista con
  nombre, SKU y precio de cada uno para que el operador elija; solo se agrega el elegido y, si
  cancela, la venta no cambia.
- **FR-014**: El aviso de código no válido o no encontrado MUST desaparecer con la siguiente lectura
  o acción del operador, sin bloquear la captura.

**Pantalla de prueba**

- **FR-015**: El sistema MUST ofrecer en "Acerca de" la opción "Probar escáner", disponible para
  cualquier usuario con sesión iniciada.
- **FR-016**: Por cada lectura, la pantalla de prueba MUST mostrar el texto leído (con caracteres
  no imprimibles o inesperados de forma visible), el formato reconocido o "Formato no reconocido",
  el número de caracteres y el producto asociado (nombre y SKU) si existe.
- **FR-017**: La pantalla de prueba MUST indicar cuando una lectura no terminó con Enter, con la
  recomendación de configurar el lector para enviarlo.
- **FR-018**: La pantalla de prueba MUST conservar en pantalla las últimas 10 lecturas, de la más
  reciente a la más antigua, y permitir borrarlas; no se guardan al salir.
- **FR-019**: Las lecturas en la pantalla de prueba MUST NOT modificar la venta en curso, su
  borrador ni el inventario, ni generar registros de auditoría.

### Key Entities

- **Producto** (existente): su código de barras amplía las reglas de formato (FR-003) y su
  comparación deja de distinguir mayúsculas de minúsculas.
- **Lectura de escáner**: texto recibido del lector, con el formato reconocido (EAN-13, EAN-8,
  CODE128/CODE39 o no reconocido), su largo y si terminó con Enter. No se persiste.
- **Venta en curso** (existente, 005): recibe los productos agregados por escaneo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100 % de los escaneos de un código registrado agrega el producto correcto en el
  primer intento, en los cuatro formatos admitidos.
- **SC-002**: Escanear un producto ya presente en la venta incrementa su cantidad en 1 en el 100 %
  de los casos, sin crear líneas duplicadas.
- **SC-003**: Agregar un producto por escaneo actualiza las líneas y el total en menos de 0.5
  segundos con un catálogo de 10 000 productos (meta de 005).
- **SC-004**: En una ráfaga de 20 escaneos seguidos no se pierde, duplica ni mezcla ninguna lectura.
- **SC-005**: Ante un código no válido o no encontrado, el operador ve el aviso y puede abrir la
  búsqueda manual con una sola tecla.
- **SC-006**: Un usuario sin conocimientos técnicos verifica con la pantalla de prueba que su lector
  funciona en menos de 1 minuto, sin consultar a soporte.
- **SC-007**: Después de usar la pantalla de prueba, la venta en curso conserva el 100 % de sus
  líneas, cantidades y total.

## Assumptions

- El lector se conecta como teclado (USB o inalámbrico con receptor) y está configurado para
  enviar Enter al final de cada lectura; los lectores en modo serie o con controlador propio están
  fuera de alcance.
- Otros formatos (UPC-A, QR, DataMatrix, GS1-128 con identificadores de aplicación) están fuera de
  alcance. Un UPC-A de 12 dígitos se sigue encontrando como código numérico si está registrado en
  un producto.
- El largo máximo de 48 caracteres para códigos alfanuméricos cubre las etiquetas habituales de
  CODE128 y CODE39 en comercio minorista.
- CODE128 se admite solo con los caracteres de CODE39 (letras, dígitos, espacio interior y
  - . $ / + %), sin distinguir mayúsculas de minúsculas. Una etiqueta CODE128 con otros caracteres
  (por ejemplo `_`, `#` o `'`) no se puede registrar en un producto y, al escanearla, se informa como
  "Código no válido". Ampliar el juego de caracteres queda fuera de alcance hasta que un cliente lo
  requiera.
- La distinción entre escaneo y escritura manual se basa en la velocidad de los caracteres; el
  umbral exacto se define en el plan y no requiere configuración del usuario.
- El dígito verificador solo se usa para reconocer el formato y elegir el aviso; no impide registrar
  ni encontrar códigos numéricos internos ya existentes.
- El código de barras sigue siendo único entre los productos no borrados (sin migración de esa
  regla). La lista de selección de FR-013 reutiliza la de 005 y aparece cuando el código leído
  coincide con el código de barras de un producto y con el SKU de otro.
- La pantalla de prueba no pide permisos porque no modifica datos; el producto asociado se muestra
  aunque esté inactivo, indicando su estado.
- La unicidad del código de barras, cuando aplique, se compara sin distinguir mayúsculas de
  minúsculas.
