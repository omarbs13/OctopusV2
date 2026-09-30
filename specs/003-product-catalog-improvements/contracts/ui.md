# Contrato: interfaz de Productos (cambios)

Amplía `specs/001-pos-foundation/contracts/ui.md` (Productos) y respeta
`specs/002-navigation-forms/contracts/forms.md` (editor en panel lateral, cambios sin guardar).
Todos los textos nuevos viven en `Resources/Strings.resx`.

## Listado (`ProductsView` / `ProductsViewModel`)

| Elemento | Comportamiento |
|---|---|
| Columna Imagen (primera, 40 × 40) | Miniatura del producto, o un ícono neutro si no tiene imagen o la miniatura no se puede leer |
| Columnas | Imagen, Nombre, SKU, Código de barras, Unidad, Precio y Estado |
| Casilla "Mostrar inactivos" | Desmarcada por defecto. Al cambiarla, se vuelve a la página 1 y se muestra u oculta la columna Estado (FR-001 a FR-004) |
| Fila inactiva | Opacidad reducida (0.55) y etiqueta "Inactivo" en la columna Estado. La distinción no depende solo del color |
| Barra de paginación (debajo de la lista) | Texto "{total} registros · Página {n} de {m}" (por ejemplo "250 registros · Página 2 de 3") y botones Primera, Anterior, Siguiente y Última con íconos y descripción accesible |
| Botones de paginación | Primera y Anterior se deshabilitan en la página 1; Siguiente y Última, en la última página. Con 0 registros, todos se deshabilitan |
| Teclado | Alt+Inicio, Alt+RePág, Alt+AvPág y Alt+Fin para primera, anterior, siguiente y última |
| Búsqueda | Igual que en la fundación (espera de 250 ms, Enter inmediato). Cada búsqueda nueva vuelve a la página 1 |
| Sin resultados | "No se encontraron productos." y "0 registros · Página 1 de 1" |
| Se elimina | El aviso "Se muestran los primeros 200 resultados" (`HasMore`) |

**Estado de la pantalla**:

- `CurrentPage`, `TotalCount` y `TotalPages` son parte del estado que la pantalla conserva durante
  la sesión (002: se conserva al salir y regresar, y los datos se refrescan al volver).
- Tras guardar en el editor, se busca con `LocateProductId` y el producto guardado queda visible y
  seleccionado en su página.
- Tras borrar, se recarga la página actual; si dejó de existir, se muestra la última página válida.

## Editor (`ProductEditorView` / `ProductEditorViewModel`)

| Campo | Control | Obligatorio (asterisco con `FieldLabel.IsRequired`) |
|---|---|---|
| Nombre | Texto | Sí |
| SKU | Texto | Sí |
| Código de barras | Texto | No |
| Precio | Texto; acepta `1234.50` o `1,234.50`. Al abrir un producto, se muestra sin separador | Sí |
| Unidad de medida ➕ | Lista desplegable con las 8 unidades; "Pieza" por defecto en alta | Sí |
| Imagen ➕ | Área de vista previa (máximo 200 × 200) con los botones "Seleccionar imagen…" / "Cambiar imagen…" y "Quitar imagen" | No |
| Activo | Casilla, solo en edición | — |

**Imagen**:

- "Seleccionar imagen…" abre el selector de archivos del sistema con el filtro "Imágenes (*.jpg,
  *.jpeg, *.png, *.webp)".
- Mientras se procesa, se muestra un indicador de progreso y los botones de imagen y "Guardar" se
  deshabilitan.
- Si la imagen es válida, la vista previa muestra la imagen optimizada.
- Si no es válida, aparece el mensaje de `InvalidImage` debajo del área de la imagen y la imagen
  anterior se conserva.
- "Quitar imagen" vacía la vista previa y muestra el marcador "Sin imagen".
- Los cambios de imagen cuentan como cambios sin guardar (002, FR de cambios sin guardar).
- Cancelar descarta los cambios de imagen.

**Errores de validación**:

- Aparecen debajo de su campo. El foco pasa al primer campo con error y lo capturado se conserva
  (fundación).
- Los mensajes son los de [use-cases.md](use-cases.md).

## Diálogos (`IDialogService`) ➕

```text
Task<FileSelection?> PickOpenFileAsync(string title, IReadOnlyList<FileTypeFilter> filters)
  // FileSelection: nombre, tamaño en bytes y Func<Task<Stream>> para abrir el archivo.
  // Devuelve nulo si el operador cancela.
```

`FakeDialogService` agrega una respuesta programable para las pruebas de ViewModel.
