# Contrato de interfaz (Desktop)

## Formulario de producto
- Orden: Código (SKU, opcional), **Modelo**, categoría, unidad de venta, existencias, imágenes, y el bloque
  **Datos del proveedor**: Proveedor, Marca, Medidas, Embalaje (m²/caja), Piezas por caja, Precio X M2,
  Precio X CAJA, Precio por pieza.
- Ancho de tableta/pantalla grande: "Datos del proveedor" a dos columnas; angosto: una columna.
- Bajo Precio X CAJA / X M2: texto de sugerencia con botón "Usar" (no rellena solo).
- Advertencia amarilla si la diferencia supera el 2 %; no bloquea Guardar.
- Duplicado: mensaje con enlace al producto existente.
- Producto no loseta: se ocultan Embalaje, Piezas por caja, X M2 y X CAJA.

## Listado
- Columnas: Modelo, Medidas, Marca, X M2, X CAJA, Existencia (más las actuales). Insignia "Para revisión".
- Filtros: Marca, Medida, Para revisión. Búsqueda por modelo, medida o marca.

## Punto de venta
- Cada partida de loseta muestra un selector Caja / m² (y Pieza si hay precio por pieza); inicia con la unidad
  del producto; las modalidades sin precio derivable no aparecen.

## Resumen de migración
- Diálogo una sola vez al primer inicio de sesión tras actualizar: migrados, sin modelo, sin embalaje,
  marcados por duplicado; botón "Ver productos para revisión".
