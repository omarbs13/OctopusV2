# Contrato: interfaz de usuario

> **Nota**: la navegación lateral, la pantalla de inicio y la presentación del editor (panel
> lateral o pantalla completa, con confirmación de cambios sin guardar) se rigen ahora por
> `specs/002-navigation-forms/contracts/`. Este contrato sigue vigente para el comportamiento
> propio de Productos y de Acerca de.
>
> **Nota (003)**: el listado de Productos (paginación, filas inactivas, miniaturas y unidad) y el
> editor (unidad de medida, imagen y formatos de precio) se rigen ahora por
> `specs/003-product-catalog-improvements/contracts/ui.md`. Ya no existe el aviso de "primeros
> 200 resultados".

Pantallas y comportamiento visibles para el operador. Todos los textos están en español y viven
en archivos de recursos (`Resources/Strings.resx`), no en cadenas fijas en las vistas.

## Ventana principal

- Tiene una navegación lateral con **Productos** (predeterminado) y **Acerca de**.
- Muestra la versión de la aplicación en la barra de título.

## Productos (`ProductsView` / `ProductsViewModel`)

| Elemento | Comportamiento |
|---|---|
| Caja de búsqueda | Filtra al escribir, con 250 ms de espera tras la última tecla; Enter busca de inmediato. Tiene el foco al abrir la pantalla |
| Casilla "Mostrar inactivos" | Desmarcada por defecto (FR-016) |
| Lista | Columnas: Nombre, SKU, Código de barras, Precio (`$1,234.50`) y Estado (solo visible con "Mostrar inactivos") |
| Sin resultados | Texto "No se encontraron productos." (FR-017) |
| `HasMore` | Texto "Se muestran los primeros 200 resultados; refine la búsqueda." |
| Botón "Nuevo producto" | Abre el editor vacío |
| Botones "Editar" (o doble clic en la fila) y "Borrar" | Actúan sobre la fila seleccionada |

## Editor de producto (`ProductEditorView` / `ProductEditorViewModel`)

| Campo | Control | Notas |
|---|---|---|
| Nombre | Texto | Máximo 200 caracteres |
| SKU | Texto | Máximo 50 caracteres; se muestra en mayúsculas al salir del campo |
| Código de barras | Texto | Opcional |
| Precio | Texto | Formato `1234.50`, sin comas ni símbolo (clarificación 5) |
| Activo | Casilla | Solo en edición; al crear, el producto siempre es activo |

- El botón "Guardar" está deshabilitado mientras se guarda (FR-015). "Cancelar" cierra el editor
  sin guardar.
- Cuando hay errores, cada mensaje aparece debajo de su campo, lo capturado se conserva y el foco
  pasa al primer campo con error (FR-014).
- Con `Duplicate`, el mensaje aparece en el campo correspondiente: "Ya existe un producto con
  este SKU." o "Ya existe un producto con este código de barras."
- Con `Conflict` aparece un diálogo: "Otro proceso modificó este producto después de que lo
  abrió. ¿Recargar los datos actuales?" Si el operador acepta, se ejecuta GetProduct y se
  reemplazan los campos; si no, el editor sigue abierto con lo capturado.
- Con `NotFound` aparece un diálogo: "El producto ya no existe." Luego se cierra el editor y se
  refresca la lista.
- Después de guardar con éxito, el editor se cierra y la lista se refresca, mostrando el producto
  y seleccionándolo.

## Borrar

- Aparece el diálogo "¿Borrar el producto «{Nombre}» ({SKU})? Dejará de aparecer en el
  catálogo." con los botones Borrar y Cancelar. La opción predeterminada es Cancelar.
- Si se confirma, se ejecuta DeleteProduct y se refresca la lista. Con `Conflict` o `NotFound` se
  muestra un mensaje y se refresca.

## Acerca de (`AboutView` / `AboutViewModel`)

- Muestra la versión, la carpeta de datos (con un botón "Copiar ruta") y el sistema operativo.
- El botón "Exportar diagnóstico…" abre un selector para guardar con el nombre sugerido
  `pos-diagnostico-<yyyyMMdd-HHmm>.zip`. Mientras se exporta, muestra un indicador de progreso.
  Si termina bien, dice "Diagnóstico exportado en {ruta}."; si falla, muestra un mensaje de error
  comprensible.

## Errores inesperados

En cualquier pantalla aparece el diálogo "Ocurrió un error inesperado. La información quedó
registrada para soporte. Puede intentar de nuevo." La pantalla conserva lo capturado y la
aplicación sigue abierta (FR-026).

## Teclado

- Ctrl+N abre un producto nuevo y Ctrl+F lleva el foco a la búsqueda.
- F2 edita el producto seleccionado y Supr lo borra (con confirmación).
- En el editor, Enter guarda y Esc cancela.
- Todo el flujo se puede completar sin mouse.
