# Alertas de existencia: guía para soporte

Guía para soporte técnico de las alertas inteligentes de bajo stock (funcionalidad 022, versión 0.16.0):
qué umbrales hay, cuándo y a quién se avisa, y dónde buscar en el log. La especificación completa está en
[specs/022-low-stock-alerts](../specs/022-low-stock-alerts/spec.md).

## Umbrales del producto

Se capturan en el formulario de producto, sección "Inventario", y solo si "Controla inventario" está
marcado (si se desmarca, ambos se guardan vacíos):

| Campo | Columna | Nivel que dispara |
|---|---|---|
| Existencia mínima | `Products.MinimumStock` | **En alerta**: la existencia es igual o menor |
| Punto de reorden | `Products.ReorderPoint` | **Urgente**: la existencia es igual o menor |

- Ambos son opcionales, en milésimas y con los decimales de la unidad (una pieza no admite `2.5`).
- El punto de reorden debe ser **estrictamente menor** que la existencia mínima ("El punto de reorden debe
  ser menor que la existencia mínima."). 0 es válido: el producto es urgente solo al agotarse.
- Se puede tener solo uno de los dos. Sin ninguno, el producto nunca está en alerta.
- Los cambios de ambos campos quedan en la auditoría del producto ("Existencia mínima", "Punto de reorden").

## Niveles

El nivel se evalúa en este orden (`StockAlertRule`):

1. **Urgente** si hay punto de reorden y la existencia no lo supera.
2. **En alerta** si hay mínimo y la existencia no lo supera.
3. Sin alerta en otro caso.

El nivel es **independiente** del estado "Sin existencia": un producto en 0 (o negativo) sigue contando en
la tarjeta "Sin existencia" y además es urgente o está en alerta si tiene umbrales. Solo cuentan productos
**activos**, no borrados y que controlan inventario.

## Cuándo se avisa

- La revisión corre **al iniciar sesión** (también al cambiar de usuario) y luego **cada hora** mientras la
  sesión está abierta, en segundo plano. Nunca se solapan dos revisiones.
- Se muestra como máximo una notificación por nivel, abajo a la derecha: "Existencia urgente" (rojo) y
  "Existencia en alerta" (naranja), con el **total actual** de productos de ese nivel. No toman el foco ni
  bloquean la venta; permanecen hasta pulsarse o descartarse.
- **Una vez al día por producto, nivel y usuario** (día calendario local del equipo):
  - Al mostrar una notificación se registran todos los productos de ese nivel en
    `StockAlertAcknowledgements` (mostrar = notificado; descartar no escribe nada más).
  - Una alerta que baja a urgente el mismo día **sí** vuelve a avisar (escalamiento); una urgente que sube a
    alerta **no**. Un producto que entra al nivel más tarde en el día dispara un aviso nuevo.
  - Al día siguiente se avisa de nuevo el nivel vigente. Cada usuario recibe sus propios avisos.
- Pulsar la notificación abre **Reportes > Inventario** con el período "Hoy" y el filtro "Urgente" o "En
  alerta"; el botón "×" (nombre accesible "Descartar") solo la cierra.
- Con la sesión bloqueada, las notificaciones quedan debajo del bloqueo y siguen visibles al desbloquear.

## Tarjeta de Inicio

"Alertas de existencia" reemplaza a la antigua "Existencia baja": muestra **Urgentes** (rojo si hay alguno)
y **En alerta** (naranja si hay alguno), en color neutro cuando valen 0. Cada cifra abre el reporte de
inventario filtrado. Se recalcula cada vez que se muestra Inicio y es de solo lectura (no registra nada).
"Sin existencia" no cambia.

Las cifras de la tarjeta, de las notificaciones y del reporte filtrado de hoy coinciden
(`StockAlertConsistencyTests`).

## Permiso y licencia

Las notificaciones, la tarjeta y el filtro requieren el permiso `ViewInventory` y la licencia del módulo
Inventario. Sin ellos no se revisa nada ni se muestra la tarjeta.

## Registros y purga

`StockAlertAcknowledgements` (usuario, producto, nivel, `LocalDate` en `yyyy-MM-dd`, `CreatedAt` en UTC) es un
registro técnico: sin auditoría ni borrado lógico. En cada revisión, dentro de la misma transacción, se
borran las filas con más de **7 días**. Borrar la tabla a mano solo provoca que se vuelva a avisar hoy.

## Diagnóstico

Cada revisión deja en el log la operación `RevisarAlertasDeExistencia` con el usuario, los conteos de
urgentes y en alerta, si notificó cada nivel y la duración en milisegundos. Una falla (por ejemplo, la base
bloqueada) se registra en el log y **no** se muestra al operador; la transacción se revierte y se reintenta
en la siguiente revisión.
