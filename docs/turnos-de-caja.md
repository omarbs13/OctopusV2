# Turnos de caja

Guía de soporte de los turnos de caja (funcionalidad 008, versión 0.6.0): cómo se calcula el efectivo,
cómo funciona el arqueo ciego, qué pasa cuando un administrador cierra el turno de otro usuario y cómo
se reimprime un corte.

## Conceptos

- **Caja**: la instalación del POS en una máquina. En esta fase hay una sola, `CAJA-1` ("Caja 1").
- **Turno**: periodo de trabajo de un usuario en la caja, desde la apertura hasta el cierre. Se muestra
  como `T-000123`; sus movimientos de efectivo, como `T-000123-02`.
- **Fondo inicial**: efectivo con el que se abre el turno para dar cambio. Un fondo de 0 requiere
  confirmación explícita.
- Solo puede haber **un turno abierto por caja**. Lo garantiza el índice único filtrado
  `IX_CashShifts_OpenPerRegister`, aunque se abra la aplicación en dos ventanas o tras un reinicio.

## Reglas de uso

- Para vender hace falta un turno abierto **propio**. Si el turno abierto es de otro usuario, el Punto
  de venta se bloquea: el cajero solo puede cerrar sesión y el administrador ve "Cerrar ese turno".
- Cada venta queda ligada al turno y al cajero (`Sales.CashShiftId`, `Sales.CreatedBy`).
- Solo se cancelan ventas **del turno abierto actual**.
- Un turno cerrado es inmutable: no admite ventas, cancelaciones ni movimientos, y su corte se puede
  reimprimir con las mismas cifras (se lee de la instantánea guardada al cerrar).
- Un turno abierto sobrevive al cierre de la aplicación: el mismo usuario lo continúa al volver a
  iniciar sesión.

## Fórmula del efectivo esperado

```text
esperado = fondo inicial
         + efectivo de las ventas (monto aplicado, ya neto de cambio)
         − efectivo de las ventas canceladas
         + ingresos − retiros
```

Se calcula siempre dentro de la transacción de escritura (`CashShiftMath` en Domain); nunca se
acumula en una columna. La tarjeta y la transferencia **no** cuentan: se reportan aparte. El "total
vendido" es la suma de las ventas completadas del turno, con todas las formas de pago y sin
canceladas; es la misma cifra en Inicio, en "Turnos" y en el corte.

- Un **retiro** mayor que el esperado en ese momento se rechaza. El cajero ve un mensaje genérico sin
  montos; el administrador ve el monto disponible.
- El retiro de un **cajero** requiere autorización de un administrador con el mismo diálogo que la
  cancelación de ventas (007). Queda el autorizador en la bitácora.
- La **cancelación** de una venta en efectivo se rechaza si el esperado quedaría negativo, sin revelar
  su monto a ningún rol. La salida es registrar un ingreso y reintentar.

## Arqueo ciego y cierre

El cierre tiene tres pasos:

1. **Conteo**: el usuario captura el efectivo contado sin ver ninguna cifra esperada.
2. **Cifras**: se muestran esperado, contado, diferencia (sobrante, faltante o cuadrado), tarjeta y
   transferencia. Si la diferencia no es cero, el comentario es obligatorio. "Volver a contar" regresa
   al paso 1; **cada conteo queda en la bitácora** (`SHIFT_CASH_COUNTED`), así que se puede reconstruir
   cuántas veces y cuánto contó una persona.
3. **Cierre**: `CloseShift` recalcula todo; si el esperado cambió desde el paso 2 (por ejemplo, una
   venta desde otra ventana) devuelve "El turno cambió" y vuelve a mostrar las cifras. Al cerrar se
   imprime el corte; una falla de la impresora **no** revierte el cierre (se ofrece "Reintentar").

No se puede cerrar un turno mientras su dueño tenga una venta en curso: hay que terminarla o
cancelarla.

## Cierre de un turno ajeno (administrador)

Un administrador puede cerrar el turno de otro usuario desde el Punto de venta o desde "Turnos".
El conteo lo captura el administrador y queda registrado quién cerró. Si el dueño dejó una venta en
curso guardada, se avisa, se pide confirmación y se **descarta**; el descarte queda en la bitácora
(`HELD_SALE_DISCARDED`). El cierre deja una sola entrada, `SHIFT_CLOSED_BY_ADMIN`, con el nombre del
dueño.

## Reimpresión del corte y comprobantes

- "Turnos" → detalle del turno cerrado → **Reimprimir corte** (sale con la leyenda REIMPRESIÓN).
  También puede imprimirlo quien cerró el turno, al cerrarlo.
- Cada ingreso o retiro ofrece **Imprimir comprobante** al registrarse; el administrador puede
  reimprimirlo desde la pestaña Movimientos del detalle.

## Bitácora

| Evento | Cuándo |
|---|---|
| `SHIFT_OPENED` | Apertura del turno (con el fondo) |
| `CASH_DEPOSIT` / `CASH_WITHDRAWAL` | Ingreso o retiro (con el autorizador, si lo hubo) |
| `SHIFT_CASH_COUNTED` | Cada conteo del arqueo |
| `SHIFT_CLOSED` / `SHIFT_CLOSED_BY_ADMIN` | Cierre por el dueño o por un administrador |
| `HELD_SALE_DISCARDED` | Venta conservada descartada al cerrar un turno ajeno |

## Ventas anteriores a 0.6.0

Las ventas hechas antes de esta versión no pertenecen a ningún turno (`CashShiftId` nulo). Se
conservan y se consultan igual que antes, pero **ya no se pueden cancelar**: su efectivo no forma
parte de ningún arqueo, y cancelarlas descuadraría el turno abierto sin razón.

## Diagnóstico

Con la aplicación cerrada:

```bash
sqlite3 pos.db "SELECT COUNT(*) FROM CashShifts WHERE Status = 'OPEN';"   # 0 o 1
sqlite3 pos.db "SELECT COUNT(*) FROM Sales WHERE CashShiftId IS NULL AND CreatedAt > (SELECT MIN(OpenedAt) FROM CashShifts);"   # 0
```

Aperturas, movimientos, conteos, cierres y rechazos también se registran en los logs (Serilog) con
el turno, el usuario y los importes.

## Fuera de alcance

Varias cajas, conteo por denominaciones, corte X, depósitos bancarios y cancelación de ventas de
turnos cerrados.
