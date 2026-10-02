# Especificación de funcionalidad: Categorías de productos

**Rama de funcionalidad**: `016-product-categories`

**Creado**: 2026-10-01

**Estado**: Borrador

**Entrada**: Categorías de productos: clasificación para reportes, filtros y análisis. Organizar productos por categoría para facilitar búsqueda, reportes y análisis de margen futuro.

## Objetivo

Que el Administrador pueda clasificar los productos en categorías y consultar ventas, productos más vendidos y existencias filtrados o agrupados por categoría, con totales que cuadran exactamente con los reportes sin filtro. La categoría es opcional: un producto sin categoría sigue siendo válido y vendible.

## Conceptos

- **Categoría**: grupo de productos con nombre único y descripción opcional. Puede estar activa o inactiva.
- **Categoría inactiva**: categoría que se conserva con sus productos y aparece en los reportes, pero que no se ofrece al asignar categoría a un producto.
- **Sin categoría**: agrupación de los productos que no tienen categoría asignada. Aparece en filtros y agrupaciones como una opción más, no como una categoría registrada.
- **Categoría vigente**: la categoría que tiene el producto al momento de consultar el reporte. Los reportes agrupan las ventas por la categoría vigente del producto, no por la que tenía cuando se vendió.

## Clarifications

### Session 2026-10-01

- Q: Cuando un producto cambia de categoría, ¿sus ventas pasadas aparecen bajo la categoría que tiene hoy o bajo la que tenía al venderse? → A: Bajo la categoría vigente; la venta no guarda la categoría y reclasificar un producto mueve también sus ventas pasadas.
- Q: ¿El punto de venta debe permitir buscar o filtrar productos por categoría? → A: No; la categoría solo se usa en el catálogo de categorías, el listado de productos y los reportes.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia 1 - Catálogo de categorías (Prioridad: P2)

El Administrador entra a "Catálogos > Categorías", donde ve el listado de categorías con nombre, descripción, número de productos y estado. Puede crear una categoría con nombre y descripción opcional, editarla, desactivarla, reactivarla y eliminarla si no tiene productos.

**Por qué esta prioridad**: sin catálogo no hay categorías que asignar ni por las cuales filtrar; es la base de las otras dos historias.

**Prueba independiente**: se puede probar creando, editando, desactivando, reactivando y eliminando categorías, y verificando que el listado, la búsqueda y las validaciones se comportan como se describe, sin tocar productos ni reportes.

**Escenarios de aceptación**:

1. **Dado** el catálogo vacío, **cuando** el Administrador crea la categoría "Bebidas" sin descripción, **entonces** aparece en el listado como activa y con 0 productos.
2. **Dado** que existe "Bebidas", **cuando** se intenta crear "bebidas" o "  BEBIDAS " (aunque "Bebidas" esté inactiva), **entonces** se rechaza con el mensaje "Ya existe una categoría con ese nombre".
3. **Dado** una categoría, **cuando** se guarda con nombre vacío o solo espacios, o con nombre de más de 50 caracteres o descripción de más de 200, **entonces** se rechaza indicando el campo y el motivo.
4. **Dado** "Bebidas" con 12 productos, **cuando** el Administrador la desactiva, **entonces** el sistema avisa "La categoría tiene 12 productos. Seguirán asignados a ella, pero no podrá asignarse a productos nuevos" y solo la desactiva si el Administrador confirma; al cancelar, queda activa.
5. **Dado** una categoría sin productos, **cuando** se desactiva, **entonces** se desactiva sin aviso adicional.
6. **Dado** una categoría inactiva, **cuando** el Administrador la reactiva, **entonces** vuelve a ofrecerse al asignar categoría a productos.
7. **Dado** "Bebidas" con al menos un producto, **cuando** el Administrador intenta eliminarla, **entonces** se rechaza con el mensaje "No se puede eliminar: la categoría tiene N productos. Reasígnalos o desactívala".
8. **Dado** una categoría sin productos, **cuando** el Administrador la elimina y confirma, **entonces** desaparece del listado y de los filtros.
9. **Dado** el listado, **cuando** el Administrador busca "beb" o filtra por estado (activas, inactivas, todas), **entonces** ve solo las categorías que coinciden, ordenadas por nombre.

---

### Historia 2 - Asignar categoría a producto (Prioridad: P2)

En el formulario de alta y edición de producto hay un campo "Categoría" opcional que lista las categorías activas y la opción "Sin categoría". En el listado de productos se muestra la categoría de cada uno y se puede filtrar por ella.

**Por qué esta prioridad**: sin asignación los reportes por categoría no tienen datos; además permite encontrar productos más rápido en catálogos grandes.

**Prueba independiente**: se puede probar creando y editando productos con y sin categoría, filtrando el listado de productos por categoría y verificando que un producto sin categoría se guarda y se vende sin restricciones.

**Escenarios de aceptación**:

1. **Dado** el formulario de producto nuevo, **cuando** se guarda sin elegir categoría, **entonces** el producto se crea como válido con "Sin categoría".
2. **Dado** un producto existente, **cuando** el Administrador le asigna "Bebidas" y guarda, **entonces** el listado de productos muestra "Bebidas" en su fila y el contador de productos de "Bebidas" aumenta en 1.
3. **Dado** un producto en "Bebidas", **cuando** se cambia a "Sin categoría", **entonces** queda sin categoría y el contador de "Bebidas" disminuye en 1.
4. **Dado** que "Lácteos" está inactiva, **cuando** se abre el selector de categoría de un producto, **entonces** "Lácteos" no aparece como opción.
5. **Dado** un producto asignado a "Lácteos" (inactiva), **cuando** se edita otro dato y se guarda, **entonces** conserva "Lácteos"; el selector la muestra marcada como "(inactiva)" y solo permite cambiarla a una activa o a "Sin categoría".
6. **Dado** el listado de productos, **cuando** se filtra por "Bebidas" o por "Sin categoría", **entonces** se muestran solo los productos que corresponden, combinando el filtro con la búsqueda por nombre o SKU.
7. **Dado** un producto sin categoría, **cuando** se vende en el punto de venta, **entonces** la venta se registra igual que con cualquier otro producto.

---

### Historia 3 - Reportes por categoría (Prioridad: P2)

En "Reportes > Ventas" y "Reportes > Inventario" (spec 009) se agrega un filtro por categoría. En "Reportes > Ventas" se agrega además la sección "Ventas por categoría": una tabla con cada categoría, sus unidades vendidas, su importe vendido y su porcentaje del total, en la que cada categoría se puede expandir para ver sus productos más vendidos.

**Por qué esta prioridad**: es el objetivo de la funcionalidad: saber qué familias de productos venden más y revisar existencias por familia.

**Prueba independiente**: se puede probar con un conjunto de ventas conocido sobre productos de dos categorías y algunos sin categoría, comparando los totales del reporte filtrado y agrupado contra el cálculo manual y contra el reporte sin filtro.

**Escenarios de aceptación**:

1. **Dado** ventas del período por 1,000.00 en "Bebidas", 600.00 en "Botanas" y 400.00 en productos sin categoría, **cuando** el Administrador abre "Ventas por categoría", **entonces** ve Bebidas 1,000.00 (50 %), Botanas 600.00 (30 %), Sin categoría 400.00 (20 %) y un total de 2,000.00 igual al total vendido del reporte sin filtro.
2. **Dado** el mismo período, **cuando** filtra el reporte de ventas por "Bebidas", **entonces** las tarjetas, la gráfica por día y la tabla de detalle muestran solo lo vendido de productos de "Bebidas", el total vendido es 1,000.00 y las formas de pago muestran "—" con la nota "No se desglosa por categoría".
3. **Dado** una venta con líneas de "Bebidas" y de "Botanas", **cuando** se filtra por "Bebidas", **entonces** la venta aparece en el detalle con solo el importe de sus líneas de "Bebidas", y la cantidad de ventas cuenta cada venta que tenga al menos una línea de la categoría.
4. **Dado** la tabla "Ventas por categoría", **cuando** el Administrador expande "Bebidas", **entonces** ve sus productos ordenados de mayor a menor unidades vendidas, con unidades e importe, y la suma de sus importes es igual al importe de "Bebidas".
5. **Dado** ventas con descuentos (spec 015), **cuando** se agrupa por categoría, **entonces** el importe de cada línea es el importe final después de su descuento de línea y de su parte del descuento global, de modo que la suma de las categorías es igual al total vendido.
6. **Dado** ventas canceladas en el período, **cuando** se consulta cualquier vista por categoría, **entonces** quedan excluidas, igual que en el reporte de ventas sin filtro.
7. **Dado** un producto que se vendió cuando estaba en "Botanas" y después se movió a "Bebidas", **cuando** se consulta el período de esa venta, **entonces** la venta se cuenta en "Bebidas" (categoría vigente).
8. **Dado** el reporte de inventario, **cuando** se filtra por "Lácteos" o por "Sin categoría", **entonces** las tarjetas (total, activos, existencia baja, sin existencia), la gráfica y la tabla consideran solo esos productos, y la tabla incluye la columna "Categoría".
9. **Dado** cualquiera de los reportes filtrado por categoría, **cuando** se exporta a PDF o a Excel, **entonces** el documento indica la categoría filtrada en los filtros aplicados y contiene las mismas cifras que la pantalla; la exportación del reporte de ventas incluye la sección "Ventas por categoría".

---

### Casos límite

- Categoría sin ventas en el período: no aparece en "Ventas por categoría"; al filtrar por ella, el reporte muestra "Sin datos en este período".
- "Sin categoría" sin productos o sin ventas: no aparece en la agrupación, pero sigue disponible como filtro.
- Categoría inactiva con productos: aparece en los filtros de reportes y del listado de productos, marcada como "(inactiva)", y sus ventas se agrupan normalmente.
- Categoría eliminada: solo es posible si no tenía productos no eliminados, por lo que no deja productos vigentes huérfanos. Los productos eliminados lógicamente que aún la tenían quedan "Sin categoría" en la misma operación, y sus ventas pasadas pasan a agruparse en "Sin categoría"; una categoría eliminada nunca aparece en los reportes.
- Producto inactivo o eliminado lógicamente: no cuenta para impedir la eliminación de una categoría si está eliminado; si solo está inactivo, sí cuenta. Sus ventas pasadas se agrupan por su categoría vigente (o "Sin categoría" si su categoría se eliminó).
- Edición simultánea: si dos usuarios editan la misma categoría, el segundo en guardar recibe el aviso de que la categoría cambió y debe volver a abrirla.
- Desactivar o eliminar una categoría mientras otro usuario edita un producto que la tiene seleccionada: al guardar el producto, si la categoría elegida ya no está activa o ya no existe, se rechaza con un mensaje que pide elegir otra.
- Redondeo: los porcentajes del total se muestran con un decimal; los importes se suman en centavos, por lo que la suma de las categorías es exactamente el total aunque los porcentajes mostrados sumen 99.9 % o 100.1 %.
- Muchas categorías: el selector de categoría permite escribir para buscar cuando hay más de 15 opciones.

## Requisitos *(obligatorio)*

### Requisitos funcionales

**Catálogo de categorías**

- **FR-001**: El sistema MUST ofrecer la pantalla "Catálogos > Categorías" (junto a "Productos"), accesible solo para el Administrador, con listado de categorías (nombre, descripción, número de productos y estado), búsqueda por nombre y filtro por estado, ordenado por nombre.
- **FR-002**: El Administrador MUST poder crear una categoría con nombre obligatorio (1 a 50 caracteres, sin espacios al inicio ni al final) y descripción opcional (hasta 200 caracteres).
- **FR-003**: El nombre de la categoría MUST ser único sin distinguir mayúsculas, minúsculas ni acentos, considerando también las categorías inactivas.
- **FR-004**: El Administrador MUST poder editar el nombre y la descripción de una categoría, con las mismas validaciones del alta.
- **FR-005**: El Administrador MUST poder desactivar y reactivar una categoría. Si la categoría tiene productos, el sistema MUST mostrar el número de productos afectados y pedir confirmación antes de desactivarla.
- **FR-006**: Desactivar una categoría MUST NOT quitar la categoría a sus productos ni alterar sus ventas o reportes.
- **FR-007**: El sistema MUST impedir eliminar una categoría que tenga productos asignados (activos o inactivos, no eliminados) e indicar cuántos tiene. Una categoría sin productos MUST poder eliminarse tras confirmación.
- **FR-008**: Crear, editar, desactivar, reactivar y eliminar categorías MUST quedar registrado con usuario y fecha.

**Asignación a productos**

- **FR-009**: Cada producto MUST tener cero o una categoría. Un producto sin categoría MUST ser válido para guardarse, venderse, ajustar inventario y aparecer en reportes.
- **FR-010**: El formulario de alta y edición de producto MUST incluir un campo "Categoría" opcional que ofrezca las categorías activas y la opción "Sin categoría".
- **FR-011**: El sistema MUST rechazar la asignación de una categoría inactiva o inexistente al guardar un producto, salvo que el producto ya la tuviera asignada y no se cambie.
- **FR-012**: El listado de productos MUST mostrar la categoría de cada producto y permitir filtrar por una categoría o por "Sin categoría", en combinación con la búsqueda existente.

**Reportes**

- **FR-013**: Los reportes "Reportes > Ventas" y "Reportes > Inventario" MUST ofrecer un filtro por categoría con las opciones "Todas" (por defecto), cada categoría (activas e inactivas, estas marcadas) y "Sin categoría".
- **FR-014**: Con el filtro de categoría aplicado, todas las métricas, gráficas y tablas del reporte MUST considerar solo las líneas de venta o los productos de esa categoría, combinándose con los demás filtros existentes (período, cajero, estado). Excepción: las formas de pago no se desglosan por categoría (un pago cubre la venta completa y no se prorratea); con el filtro aplicado se muestran como "—" con la nota "No se desglosa por categoría", en pantalla y en las exportaciones.
- **FR-015**: Al filtrar ventas por categoría, el detalle MUST mostrar cada venta con al menos una línea de la categoría y solo el importe de esas líneas; la cantidad de ventas y el ticket promedio MUST calcularse con esas ventas e importes. Las devoluciones se tratan igual que en el reporte sin filtro: el total vendido resta lo devuelto de las líneas de la categoría, mientras que el importe de cada fila del detalle es el vendido, sin restar devoluciones.
- **FR-016**: "Reportes > Ventas" MUST incluir la sección "Ventas por categoría" con, por cada categoría con ventas en el período (incluido "Sin categoría"), unidades vendidas, importe vendido y porcentaje del total, ordenada de mayor a menor importe, y una fila de total. Las unidades de una categoría suman tal cual las cantidades de productos con unidades distintas (piezas, kilos, etc.), por lo que son indicativas; el desglose por producto muestra cada cantidad con su unidad.
- **FR-017**: Cada categoría de "Ventas por categoría" MUST poder expandirse para mostrar sus productos más vendidos ordenados por unidades vendidas de mayor a menor, con unidades e importe.
- **FR-018**: El importe por categoría y por producto MUST ser el importe final de la línea después de todos sus descuentos, de modo que la suma de todas las categorías sea exactamente igual al total vendido del período sin filtro de categoría.
- **FR-019**: Los reportes por categoría MUST excluir ventas canceladas y agrupar por la categoría vigente del producto al consultar.
- **FR-020**: La tabla del reporte de inventario MUST incluir la columna "Categoría" y permitir ordenar por ella.
- **FR-021**: Las exportaciones a PDF y Excel MUST incluir el filtro de categoría entre los filtros aplicados, y la del reporte de ventas MUST incluir la sección "Ventas por categoría" con el desglose de productos de cada categoría.
- **FR-022**: Los permisos de acceso a cada reporte MUST ser los de la spec 009; el filtro y la agrupación por categoría no amplían lo que cada rol puede ver.

### Entidades clave

- **Categoría**: nombre (único), descripción opcional, estado activo o inactivo, y los datos de auditoría y concurrencia comunes a toda entidad de negocio. Se relaciona con muchos productos.
- **Producto**: ya existente; se le agrega una referencia opcional a una categoría.
- **Venta y línea de venta**: ya existentes; la categoría de cada línea se obtiene del producto al consultar, sin guardarse en la venta.
- **Movimiento de inventario**: ya existente; el reporte de inventario lo filtra por la categoría vigente del producto.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: El Administrador crea una categoría y la asigna a un producto en menos de 1 minuto.
- **SC-002**: En las pruebas de aceptación, la suma de los importes de "Ventas por categoría" coincide al centavo con el total vendido del período en el 100 % de los casos, incluidas ventas con descuentos y productos sin categoría.
- **SC-003**: Para cada categoría, la suma de los importes de sus productos coincide al centavo con el importe de la categoría en el 100 % de los casos probados.
- **SC-004**: Con 10,000 ventas en el período, el reporte de ventas filtrado o agrupado por categoría muestra sus resultados en menos de 2 segundos, igual que el reporte sin filtro.
- **SC-005**: El 100 % de los productos existentes antes de esta funcionalidad siguen siendo válidos, vendibles y visibles en reportes como "Sin categoría" tras la actualización.
- **SC-006**: Ninguna categoría con productos puede eliminarse, y ninguna se desactiva sin que el Administrador confirme el aviso, en el 100 % de los intentos probados.

## Supuestos

- Solo el Administrador gestiona el catálogo de categorías; la asignación a productos la hace quien ya puede editar productos (spec 003/007), sin permisos nuevos.
- Las categorías forman parte del catálogo de productos básico, no de un módulo licenciado nuevo; el filtro y la agrupación en reportes están disponibles donde ya lo estén los reportes de la spec 009.
- La categoría es de un solo nivel; subcategorías, jerarquías y varias categorías por producto quedan fuera de alcance.
- "Productos más vendidos agrupado por categoría" se entrega como la sección "Ventas por categoría" del reporte de ventas, con desglose de productos por categoría. La tarjeta "Productos más vendidos" de Inicio no cambia.
- Las ventas se agrupan por la categoría vigente del producto; no se guarda la categoría histórica en cada venta. Reclasificar un producto cambia también cómo aparecen sus ventas pasadas.
- Los productos existentes quedan "Sin categoría" tras la actualización; no hay asignación automática ni importación masiva de categorías.
- El importe de una línea con descuento global usa el reparto proporcional definido en la spec 015.
- Las devoluciones (spec 013) se tratan en los reportes por categoría igual que en el reporte de ventas sin filtro.
- El análisis de margen por categoría es un objetivo futuro: esta funcionalidad no muestra costos ni márgenes.

## Fuera de alcance

- Subcategorías, jerarquías de categorías y etiquetas múltiples por producto.
- Asignación masiva de categoría a varios productos e importación de categorías.
- Reportes de margen, costo o rentabilidad por categoría.
- Descuentos, cupones o límites de descuento por categoría.
- Búsqueda, filtro o selección de productos por categoría en el punto de venta (incluida la venta táctil por categoría).
- Filtro por categoría en el reporte de arqueo.
