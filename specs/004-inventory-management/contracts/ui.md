# Contrato: interfaz de Inventario

Amplía `specs/002-navigation-forms/contracts/navigation.md`, `forms.md` y `dashboard.md`, y
`specs/003-product-catalog-improvements/contracts/ui.md`. Todos los textos van en `Strings.resx`.

## Formato de cantidades

- Se muestran con los decimales de su unidad y con separador de miles en `es-MX`: "12", "1,250"
  o "1.250" (kg).
- Siempre van seguidas del nombre de la unidad en la columna "Unidad"; no se usan abreviaturas.
- `QuantityConverter` (Desktop) solo formatea; el cálculo vive en `Quantity`.

## Navegación ✏️

- `Navigator.NavigateAsync(string entryId, object? argument = null)`.
  - Si el destino implementa `INavigationArgumentReceiver.Receive(object argument)`, se le
    entrega el argumento antes de `OnActivatedAsync`, aunque ya sea la pantalla actual.
- `DashboardCard` agrega `virtual object? NavigationArgument => null`, y `HomeViewModel` lo
  entrega al navegar.
- `InventoryModule` sustituye las dos páginas `AddComingSoonPage` por páginas reales:
  - `inventory.stock` → `StockViewModel`.
  - `inventory.movements` → `MovementsViewModel`.

## Productos ✏️

- **Listado**: nueva columna "Existencia". Muestra la cantidad formateada si el producto controla
  inventario y "—" si no (FR-005). Tiene la acción de fila "Ver movimientos", que navega a
  `inventory.movements` con `MovementsProductFilter(productId, name)` (FR-019). La acción solo se
  muestra si el producto controla inventario.
- **Editor**, nueva sección "Inventario":
  - Casilla "Controla inventario".
  - "Existencia mínima": texto opcional, visible y habilitado solo si la casilla está marcada.
  - "Existencia actual": solo lectura, con la cantidad y la unidad; "—" si no controla
    inventario.
  - Si `HasMovements` es verdadero:
    - La unidad de medida y la casilla quedan deshabilitadas, con la nota "Tiene movimientos de
      inventario; no se puede cambiar la unidad ni dejar de controlar el inventario." (FR-006).
    - El servidor valida de todos modos.
  - No ofrece registrar movimientos (FR-015a).

## Existencias (`inventory.stock`) ➕

- **Barra superior**:
  - Búsqueda con la misma espera y el mismo comportamiento que Productos (nombre, SKU o código de
    barras).
  - Filtro de estado (Todos, Normal, Existencia baja, Sin existencia).
  - Casilla "Incluir inactivos".
  - Botón "Registrar movimiento", habilitado con una fila seleccionada.
- **Columnas**: Nombre, SKU, Existencia, Unidad, Mínimo ("—" si no hay) y Estado.
  - El estado es una etiqueta de color: Normal (neutro), Baja (advertencia) o Sin existencia
    (error).
  - Las filas inactivas se ven atenuadas, con la etiqueta "Inactivo" (`InactiveOpacityConverter`
    existente).
- **Paginación**: la de Productos (100 por página, primera, anterior, siguiente y última).
- **Estado vacío**:
  - Sin productos con inventario: "Ningún producto controla inventario. Actívelo en Catálogos >
    Productos."
  - Sin resultados para el filtro: "No hay productos que coincidan."
- **Argumento** `StockFilter`: fija el filtro, limpia la búsqueda, desmarca inactivos y vuelve a
  la página 1.
- **Doble clic o Enter en una fila**: abre el formulario de movimiento con el producto elegido.

## Movimientos (`inventory.movements`) ➕

- **Filtros**:
  - Producto: selector con búsqueda y botón para quitar el filtro.
  - Tipo: Todos o uno de los 4.
  - Desde y Hasta: `CalendarDatePicker`, ambos opcionales.
  - Botón "Registrar movimiento", siempre habilitado.
- **Columnas**: Fecha y hora (local, `dd/MM/yyyy HH:mm`), Producto (nombre y SKU), Tipo,
  Cantidad (con signo + o − según el tipo), Existencia resultante, Unidad, Motivo, Referencia y
  Usuario.
- **Paginación**: 100 por página.
- **Sin acciones de fila**: no se puede editar ni borrar (FR-010, criterio de aceptación 5).
- **Argumento** `MovementsProductFilter`: fija el producto, limpia tipo y fechas, y vuelve a la
  página 1.

## Formulario "Registrar movimiento" ➕

Usa el `FormHost` y el patrón de `FormViewModel` de 002, con guardado y cancelación y aviso de
cambios sin guardar.

| Campo | Control | Reglas en la interfaz |
|---|---|---|
| Producto | Fijo (desde Existencias) o selector con búsqueda (desde Movimientos) que solo lista activos con inventario | Obligatorio |
| Existencia actual | Solo lectura, con la unidad | Se actualiza al elegir producto |
| Tipo | Lista de los 4 tipos. "Inventario inicial" solo aparece si el producto no tiene movimientos | Obligatorio; por omisión "Entrada" (o "Inventario inicial" si no hay movimientos) |
| Cantidad | Texto | Una ayuda indica "Enteros" o "Hasta 3 decimales" según la unidad |
| Existencia resultante | Solo lectura, vista previa (existencia ± cantidad) | Informativa; en rojo si sería negativa |
| Motivo | Texto multilínea, hasta 250 caracteres | Se marca obligatorio si el tipo es un ajuste |
| Referencia | Texto, hasta 50 caracteres | Opcional; ayuda: "Folio de factura o remisión" |

- Los errores de campo del caso de uso se muestran junto a su control.
- Tras guardar:
  - Se cierra el formulario.
  - La pantalla de origen recarga su página; Existencias conserva la selección.
  - Se muestra la notificación "Movimiento registrado. Existencia: {n} {unidad}."
- La vista previa de la existencia resultante se calcula con `Quantity` en el ViewModel solo
  para mostrarla. La regla autoritativa es la del caso de uso (Principio III).

## Inicio ✏️

- `LowStockCard` y `OutOfStockCard` dejan de heredar de `ComingSoonCard`:
  - Cargan `GetStockAlerts` y muestran el conteo.
  - `NavigateTo = inventory.stock`, con `NavigationArgument` igual a `StockFilter.Low` o
    `StockFilter.Out` (FR-021, criterio de aceptación 6).
- Se recalculan cada vez que se muestra Inicio (comportamiento existente de 002), así que
  reflejan los movimientos sin reiniciar (H5, escenario 5).
- Si el conteo es 0 muestran "0" y siguen siendo navegables. El estado vacío "disponible más
  adelante" desaparece.
