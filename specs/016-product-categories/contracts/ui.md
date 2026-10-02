# Contrato: interfaz de usuario

**Funcionalidad**: `016-product-categories` | **Casos de uso**: [application-ports.md](application-ports.md)

Textos en `Resources/Strings.resx` con prefijo `Category_`. Los ViewModels solo invocan casos de uso
y presentan resultados (Principio III).

## Catálogos > Categorías (nueva, `catalogs.categories`, permiso `ManageProducts`)

- Barra: búsqueda por nombre, filtro de estado (Activas por defecto, Inactivas, Todas), botón
  "Nueva categoría".
- Tabla ordenada por nombre: Nombre, Descripción, Productos, Estado.
- Acciones por fila: Editar, Desactivar o Reactivar, Eliminar.
- Formulario (diálogo): Nombre (máx. 50), Descripción (máx. 200, varias líneas). Errores junto a
  cada campo; duplicado: "Ya existe una categoría con ese nombre".
- Desactivar con productos: diálogo con el mensaje de `ConfirmationRequired`; Cancelar la deja activa.
  Sin productos: se desactiva directamente.
- Eliminar: confirmación "¿Eliminar la categoría {nombre}?"; si responde `CategoryInUse`, muestra su
  mensaje.
- `Conflict`: "La categoría cambió desde que la abriste. Vuelve a abrirla", y recarga la lista.

## Catálogos > Productos (cambia)

- Columna "Categoría" en la tabla ("Sin categoría" si no tiene; inactivas con "(inactiva)").
- Filtro "Categoría" junto a la búsqueda: Todas, Sin categoría y cada categoría no borrada (inactivas
  marcadas). Se combina con la búsqueda y con "Incluir inactivos".
- Formulario de producto: campo "Categoría" con `CategoryPicker`:
  - "Sin categoría" + activas por nombre.
  - Si el producto tiene una inactiva, aparece seleccionada como "{nombre} (inactiva)" y no se ofrece
    para otros productos.
  - Con más de 15 opciones se puede escribir para filtrar.
  - `CategoryNotAssignable` al guardar: mensaje en el campo y recarga de opciones.

## Reportes > Ventas (cambia)

- Filtro "Categoría" junto a período y cajero: Todas (por defecto), Sin categoría y cada categoría
  (inactivas marcadas).
- Con filtro: tarjetas, gráfica, comparativo y detalle sobre las líneas de la categoría. Las tarjetas de
  formas de pago muestran "—" con la nota "No se desglosa por categoría". Sin datos: "Sin datos en este
  período".
- Sección nueva "Ventas por categoría" (debajo de la gráfica):

```text
 Categoría          Unidades     Importe     % del total
 ▸ Bebidas             120.000    1,000.00        50.0 %
 ▾ Botanas              80.000      600.00        30.0 %
      Papas 45 g     50 pz         375.00
      Cacahuates     30 pz         225.00
 ▸ Sin categoría        40.000      400.00        20.0 %
 Total                 240.000    2,000.00
```

  - Ordenada por importe desc. Expandir muestra los productos por unidades desc con su unidad.

## Reportes > Inventario (cambia)

- Filtro "Categoría" con las mismas opciones; afecta tarjetas, gráfica y tabla.
- Columna "Categoría" ordenable.

## Exportaciones (PDF y Excel)

- Filtros aplicados: "Categoría: {nombre}" o "Categoría: Sin categoría" (se omite con "Todas").
- Ventas: tabla "Ventas por categoría" con filas de categoría, sus productos con sangría y total.
- Inventario: columna "Categoría".
