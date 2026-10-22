<!--
Sync Impact Report
- Versión: 1.2.0 → 1.3.0 (MENOR)
- Cambio: el principio V se amplía a la actualización de precios y existencias desde PDF
  del proveedor (lectura local, mismo flujo validado y atómico) y la importación Excel con
  una hoja por proveedor; se agrega el historial de precios (principio III); la impresión
  pasa a ser "nota de venta" en hoja (PDF + servicio de impresión de Android) o tira
  térmica Bluetooth.
- Secciones modificadas: Principios III y V, Stack (Impresión, PDF), Alcance funcional.
- Plantillas a revisar: ninguna adicional.

Historial: 1.1.0 → 1.2.0 (MENOR)
- Cambio: se agrega el principio IX (Licenciamiento sin conexión): prueba de 30 días y
  activación con código firmado Ed25519 atado al dispositivo, sin borrar datos al vencer.
- Secciones modificadas: Principios (IX nuevo), Alcance funcional (licenciamiento).
- Plantillas a revisar: ninguna adicional.

Historial: 1.0.0 → 1.1.0 (MENOR)
- Cambio: se incorpora al alcance la impresión de la nota de venta (y solo de ella) y la
  identidad visual de la app (ícono). Se permite el permiso de Bluetooth únicamente para
  imprimir. "Impresión de tickets" se retira de la lista de fuera de alcance.
- Secciones modificadas: Alcance funcional (Punto de venta y lista de exclusiones),
  Stack tecnológico y restricciones (Impresión, Identidad visual), Principio I (permisos).
- Plantillas a revisar: ninguna adicional.

Historial: (plantilla) → 1.0.0
- Ratificación inicial de la constitución de OctopusCovering.
- Principios añadidos: I a VIII (todos nuevos).
- Secciones añadidas: Stack tecnológico y restricciones, Alcance funcional, Flujo de desarrollo, Gobernanza.
- Plantillas a revisar: .specify/templates/plan-template.md (Constitution Check),
  .specify/templates/spec-template.md, .specify/templates/tasks-template.md — ⚠ pendiente de verificar alineación.
- TODOs: ninguno.
-->

# Constitución de OctopusCovering

OctopusCovering es una aplicación Android nativa para la operación diaria de un negocio
de venta de loseta (piso y recubrimientos). Cubre punto de venta, clientes, proveedores,
productos y reportes, y funciona por completo sin conexión a internet.

## Principios fundamentales

### I. Offline primero (NO NEGOCIABLE)

- La app DEBE funcionar al 100 % sin conexión a internet. Ninguna función puede depender
  de red, servicios en la nube, APIs externas ni cuentas en línea.
- No se declara el permiso `android.permission.INTERNET` mientras no exista una
  especificación aprobada que lo justifique.
- Los permisos de Bluetooth se permiten solo para imprimir la nota de venta en una
  impresora térmica, se piden en el momento de usarlos y la app funciona sin ellos.
- Todos los datos viven en el dispositivo. Las dependencias de terceros que requieran red
  (analítica, crash reporting remoto, fuentes o imágenes remotas) están prohibidas.
- El diseño DEBE dejar la puerta abierta a una futura sincronización (IDs estables,
  marcas de tiempo `createdAt`/`updatedAt` en entidades principales), sin implementarla.

**Razón:** el negocio necesita vender aunque no haya señal; la conectividad es una
posible evolución futura, no un requisito actual.

### II. Kotlin nativo y arquitectura por capas

- Lenguaje: Kotlin (100 %). UI con Jetpack Compose y Material 3.
- Arquitectura MVVM con capas claras: `ui` (Compose + ViewModel) → `domain`
  (casos de uso y modelos) → `data` (repositorios, Room, archivos).
- La UI nunca accede directamente a DAOs ni al sistema de archivos; siempre a través de
  ViewModel → caso de uso/repositorio.
- Organización por módulo funcional (feature): `pos`, `clientes`, `proveedores`,
  `productos`, `reportes`, más un núcleo compartido (`core`: base de datos, imágenes,
  formato de moneda, utilidades).
- Concurrencia con Coroutines y Flow; inyección de dependencias con Hilt.
- Simplicidad (YAGNI): no se agregan capas, librerías ni abstracciones sin un caso de uso
  concreto en una especificación.

### III. Integridad de datos con SQLite (Room)

- La persistencia DEBE usar SQLite a través de Room. Esquema versionado con migraciones
  explícitas y probadas; `fallbackToDestructiveMigration` está prohibido en builds de
  producción.
- Claves foráneas activas, restricciones `NOT NULL`/`UNIQUE` donde aplique
  (p. ej. SKU/código de producto único).
- Montos monetarios se guardan como enteros en centavos (`Long`); nunca `Float`/`Double`.
  Cantidades de loseta admiten la unidad del producto (pieza, caja, m²) con precisión
  definida en la especificación de productos.
- Una venta se registra en una sola transacción: encabezado, partidas, descuento de
  inventario y pago se confirman juntos o no se confirma nada.
- Las ventas confirmadas no se editan ni se borran físicamente; se cancelan/devuelven con
  registro y motivo, para que los reportes sean auditables. Catálogos (productos, clientes,
  proveedores) usan baja lógica (`activo = false`) cuando tienen historial.
- Las ventas guardan una copia del precio y descripción al momento de la venta, para que
  cambios posteriores al catálogo no alteren el historial.
- Todo cambio de precio de un producto se registra en un historial de precios inmutable
  (valor anterior, nuevo, fecha, origen y proveedor); las actualizaciones masivas se
  agrupan en lotes reversibles.

### IV. Imágenes de producto como archivos, no BLOBs

- Las imágenes se guardan como archivos en el almacenamiento interno privado de la app
  (`context.filesDir/product_images/`). En SQLite solo se guarda la ruta relativa (o
  nombre de archivo) y metadatos; está prohibido guardar imágenes como BLOB en la base.
- Al subir una imagen (cámara o galería vía Photo Picker, sin pedir permisos de
  almacenamiento) se DEBE: corregir la orientación EXIF, redimensionar a un máximo de
  1280 px por lado, comprimir (JPEG/WebP calidad ~80) y generar una miniatura (~300 px)
  para listados.
- Nombres de archivo generados (UUID), nunca el nombre original del usuario.
- Al eliminar o reemplazar una imagen se borra el archivo correspondiente; existe una
  rutina de limpieza de archivos huérfanos.
- Carga de imágenes en la UI con Coil (lectura local, con caché), siempre fuera del hilo
  principal.
- Un producto puede tener varias imágenes en una tabla relacionada, con una marcada como
  principal.

**Razón:** los BLOBs inflan la base, vuelven lentas las consultas y rompen el límite de
`CursorWindow` (~2 MB); los archivos comprimidos en almacenamiento interno son rápidos,
privados y fáciles de respaldar junto con la base.

### V. Importación de productos desde Excel validada y atómica

- Los productos se dan de alta por formulario o por carga masiva de un archivo Excel
  (`.xlsx`), seleccionado con el selector de documentos del sistema (SAF).
- La app DEBE proveer/documentar una plantilla con columnas fijas; la especificación de
  productos define las columnas exactas.
- Flujo obligatorio: leer → validar cada fila → mostrar vista previa con errores por
  fila (fila, columna, motivo) → el usuario confirma → insertar/actualizar en una sola
  transacción. Nada se escribe si el usuario no confirma.
- Se define explícitamente la regla de duplicados (por SKU/código): actualizar o
  rechazar, según la especificación.
- El parseo se hace en segundo plano y DEBE soportar al menos varios miles de filas sin
  bloquear la UI. La librería de lectura de Excel se elige en el plan, priorizando que sea
  liviana y compatible con Android.
- Las imágenes no se importan por Excel en la primera versión; se agregan desde el
  formulario del producto.
- El libro de Excel puede traer una hoja por proveedor; cada hoja se relaciona con un
  proveedor con confirmación del usuario.
- La actualización de precios y existencias desde PDF del proveedor sigue el mismo flujo
  (leer → validar → vista previa → confirmar → una sola transacción), se procesa en el
  dispositivo sin internet, solo toca productos del proveedor elegido y nunca aplica
  coincidencias aproximadas sin confirmación.

### VI. Pruebas proporcionales al riesgo

- Obligatorio tener pruebas unitarias para: cálculos de venta (subtotales, descuentos,
  impuestos si aplican, totales), descuento de inventario, validación de la importación
  de Excel y procesamiento de imágenes (redimensionado/rutas).
- Obligatorio tener pruebas de DAO/Room (instrumentadas o con base en memoria) y pruebas
  de cada migración de esquema.
- Pruebas de UI (Compose) para el flujo crítico de punto de venta: agregar productos,
  cobrar y ver la venta registrada.
- Un cambio que rompa pruebas existentes no se integra.

### VII. UX simple para mostrador

- Diseñada para uso rápido en mostrador: cobrar una venta común DEBE tomar pocos toques,
  con búsqueda de producto por nombre o código.
- Interfaz en español (México), moneda en pesos (MXN) con formato local; textos en
  `strings.xml`, nunca hardcodeados.
- Funcionar en teléfonos y tabletas Android (diseños adaptables); orientación vertical y
  horizontal en tableta.
- Toda acción destructiva (cancelar venta, desactivar producto, sobrescribir en
  importación) pide confirmación.
- Mensajes de error claros y accionables, sin jerga técnica.
- Accesibilidad básica: tamaños táctiles ≥ 48 dp, contraste adecuado,
  `contentDescription` en imágenes e íconos.

### VIII. Respaldo y restauración local

- Al ser offline, la app DEBE permitir exportar un respaldo completo (base de datos +
  carpeta de imágenes) a un archivo `.zip` en una ubicación que el usuario elija (SAF:
  memoria, USB, Drive del sistema si el usuario lo usa manualmente), y restaurarlo.
- El respaldo se genera con la base en estado consistente (checkpoint de WAL o cierre
  previo) e incluye la versión de esquema; restaurar valida la versión y aplica
  migraciones si corresponde.
- La app recuerda al usuario hacer respaldos periódicamente (p. ej. si pasaron más de
  7 días desde el último).

### IX. Licenciamiento sin conexión

- La app incluye un periodo de prueba de 30 días desde la primera apertura y se activa con
  un código generado por el emisor de licencias, sin internet ni servidores.
- El código está firmado digitalmente (Ed25519) y atado al ID del dispositivo; la app solo
  contiene la llave pública. La llave privada nunca se guarda en la app ni en el
  repositorio.
- Al vencer la prueba o la licencia NUNCA se borran ni se bloquean los datos: se permite
  consultar, respaldar, restaurar y exportar; solo se bloquean las operaciones que crean o
  modifican información.
- La lógica de licencia se aísla en su propio módulo y no debe impedir el uso de la app en
  desarrollo ni en pruebas automatizadas.

**Razón:** permite distribuir la app para prueba y cobrar por ella sin depender de internet,
sin poner en riesgo la información del negocio.

## Stack tecnológico y restricciones

- **Plataforma:** Android; `minSdk` 26 (Android 8.0), `targetSdk` la versión estable más
  reciente exigida por Google Play.
- **Lenguaje/UI:** Kotlin, Jetpack Compose, Material 3, Navigation Compose.
- **Datos:** SQLite vía Room; DataStore para preferencias.
- **Impresión:** solo la nota de venta, sin internet: en hoja carta o media carta como PDF
  impreso con el servicio de impresión de Android (o compartido), o en tira térmica
  Bluetooth ESC/POS de 58 u 80 mm.
- **PDF:** librería de lectura de texto de PDF que funcione en el dispositivo, definida en
  el plan; sin OCR.
- **Identidad visual:** ícono adaptativo propio (con versión monocromática) y nombre
  visible "OctopusCovering".
- **DI / asincronía:** Hilt, Coroutines, Flow.
- **Imágenes:** Photo Picker / cámara, procesamiento propio (Bitmap/ImageDecoder), Coil.
- **Excel:** librería de lectura `.xlsx` compatible con Android, definida en el plan.
- **Build:** Gradle con Kotlin DSL y catálogo de versiones (`libs.versions.toml`).
- **Calidad:** ktlint o detekt en el build; sin warnings nuevos de lint de Android.
- **Seguridad:** datos en almacenamiento privado; `allowBackup` y reglas de backup
  definidas explícitamente; sin datos sensibles en logs.

## Alcance funcional

Módulos de la primera versión:

1. **Punto de venta:** registrar ventas con búsqueda de productos, cantidades, descuento,
   cliente opcional (público en general por defecto), forma de pago, nota de venta en
   pantalla e impresión de la nota de venta (y reimpresión) en la impresora del negocio.
2. **Clientes:** alta, edición, búsqueda y baja lógica; historial de compras del cliente.
3. **Proveedores:** alta, edición, búsqueda y baja lógica; relación proveedor ↔ productos.
4. **Productos:** alta por formulario y por Excel, edición, existencias, precio, imágenes,
   búsqueda y baja lógica.
5. **Reportes (exactamente dos en esta versión):**
   - **Reporte de ventas:** ventas por rango de fechas con totales.
   - **Reporte de productos:** catálogo con existencias y precios.
6. **Precios:** actualización de precios y existencias desde PDF del proveedor e historial
   de precios (consulta dentro de Productos, no es un reporte adicional).
7. **Licenciamiento:** prueba de 30 días y activación con código, según el principio IX.

Cualquier reporte, módulo o integración adicional (facturación electrónica, sincronización
en la nube, multiusuario, impresión de cualquier documento distinto de la nota de venta,
compras a proveedor) queda fuera de alcance
hasta que una especificación aprobada lo incorpore y, si contradice esta constitución, se
enmiende primero.

## Flujo de desarrollo

- Se trabaja con spec-kit: `specify` → `clarify` → `plan` → `tasks` → `implement`, una
  especificación por módulo o funcionalidad.
- Cada plan DEBE pasar el "Constitution Check" contra estos principios; las excepciones
  se documentan con justificación en la sección de complejidad del plan.
- Control de versiones con Git; una rama por especificación; commits pequeños y
  descriptivos.
- Versionado de la app con SemVer (`versionName`) y `versionCode` incremental; cada
  cambio de esquema de base incrementa la versión de Room con su migración.

## Gobernanza

- Esta constitución prevalece sobre cualquier otra práctica o preferencia del proyecto.
- Las enmiendas se documentan en este archivo con su Sync Impact Report, se actualiza la
  versión y la fecha, y se revisan las plantillas de `.specify/templates/` afectadas.
- Versionado de la constitución (SemVer): MAYOR para eliminar o redefinir principios de
  forma incompatible; MENOR para agregar principios o secciones; PARCHE para aclaraciones
  y redacción.
- Toda revisión de código y plan verifica el cumplimiento de estos principios.

**Versión**: 1.3.0 | **Ratificada**: 2026-10-20 | **Última enmienda**: 2026-10-22
