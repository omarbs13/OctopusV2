# Contrato: interfaz de usuario

**Feature**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md)

Todos los textos van en `Resources/Strings.resx`, en español.

## Editor de producto (✏️ `Products/ProductEditorView`)

En la sección "Inventario", debajo de "Existencia mínima":

- **"Punto de reorden"**: texto opcional, visible y habilitado solo con "Controla inventario"
  marcado (igual que la existencia mínima).
- Ayuda bajo el campo: "Debe ser menor que la existencia mínima. Al llegar a este nivel la alerta
  es urgente."
- Los errores del campo `ReorderPoint` se muestran junto al control.

## Notificaciones (➕ `Shell/NotificationCenter`, ✏️ `Shell/MainView`)

- Pila abajo a la derecha, encima del contenido y debajo del modal de sesión; ancho 360.
- Como máximo una notificación por nivel; la urgente va arriba.
- Ninguna toma el foco: los botones tienen `Focusable="False"` y la pila no intercepta el teclado.
- Permanecen hasta que se pulsan o se descartan.

| Nivel | Fondo | Título | Mensaje |
|---|---|---|---|
| Urgente | color de error (`NotificationSeverityBrushConverter`, `Danger`) | "Existencia urgente" | "{0} producto(s) llegaron a su punto de reorden." |
| Alerta | color de advertencia (`Warning`) | "Existencia en alerta" | "{0} producto(s) están en su existencia mínima o por debajo." |

- **Pulsar el cuerpo**: abre "Reportes > Inventario" (`reports.inventory`) con argumento
  `StockFilter.Urgent` o `StockFilter.Alert` y cierra la notificación.
- **Botón "×"** (nombre accesible "Descartar"): cierra la notificación sin navegar.
- Con la sesión bloqueada, las notificaciones quedan debajo del bloqueo y siguen visibles al
  desbloquear.

## Inicio (➕ `Inventory/StockAlertLevelsCard`, ✏️ `Home/DashboardCard`, `Home/HomeView`)

- Tarjeta **"Alertas de existencia"**, `Order = 20` (lugar de "Existencia baja", que se retira),
  permiso `ViewInventory`, ícono `Icon.Warning`.
- Dos segmentos, cada uno un botón con número grande y etiqueta:

  | Segmento | Etiqueta | Color con valor > 0 | Destino |
  |---|---|---|---|
  | Urgentes | "Urgentes" | error | `reports.inventory` + `StockFilter.Urgent` |
  | En alerta | "En alerta" | advertencia | `reports.inventory` + `StockFilter.Alert` |

- Con valor 0 el segmento usa color neutro y sigue siendo navegable.
- "Sin existencia" y "Alertas" (009) no cambian.
- Contrato de tarjetas (002) ampliado: `DashboardCard.Segments` (vacío por omisión). Si tiene
  segmentos, la plantilla de indicadores los dibuja en lugar de `Value` y la tarjeta exterior no es
  navegable.

## Reportes > Inventario (✏️ `Reports/InventoryReportView(Model)`)

- Filtro de estado: se agregan "En alerta" y "Urgente" a "Todos / Normal / Baja / Sin existencia".
- Tabla: columna nueva **"Punto de reorden"** después de "Existencia mínima" ("—" si no tiene).
- Exportación PDF/XLSX: misma columna.
- Al recibir un `StockFilter` por navegación, el período pasa a "Hoy", se aplica el filtro, se
  limpia la búsqueda, se vuelve a la página 1 y se recarga.
