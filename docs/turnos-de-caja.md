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
- Con el módulo Devoluciones (0.8.0) se pueden cancelar o devolver ventas de **cualquier turno**: el
  reintegro en efectivo sale del turno abierto actual y el turno cerrado no se modifica. Con el módulo
  inactivo solo se cancelan ventas del turno abierto actual. Ver [devoluciones.md](devoluciones.md).
- Un turno cerrado es inmutable: no admite ventas, cancelaciones ni movimientos, y su corte se puede
  reimprimir con las mismas cifras (se lee de la instantánea guardada al cerrar).
- Un turno abierto sobrevive al cierre de la aplicación: el mismo usuario lo continúa al volver a
  iniciar sesión.

## Fórmula del efectivo esperado

```text
esperado = fondo inicial
         + efectivo de las ventas (monto aplicado, ya neto de cambio)
         − efectivo de las ventas canceladas (heredado, sin devolución registrada)
         + ingresos − retiros
         − reintegros en efectivo (0.8.0)
         + abonos de clientes en efectivo − anulaciones de abonos en efectivo (0.9.0)
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
  su monto a ningún rol. La salida es registrar un ingreso y reintentar (con Devoluciones, también
  elegir nota de crédito).
- Desde 0.8.0 el "total vendido" es neto de devoluciones parciales, y el corte agrega los reintegros en
  efectivo, los de tarjeta y transferencia pendientes de reversa y las notas de crédito emitidas
  (0 en los turnos cerrados antes de esa versión).
- Desde 0.9.0 las ventas a crédito cuentan en el "total vendido" pero **no** en el esperado. El detalle
  del turno, "Mi turno" y el corte agregan un bloque **"Crédito"**: ventas a crédito, abonos en
  efectivo, abonos con tarjeta o transferencia y anulaciones de abonos. Un abono cuenta en el turno en
  que se registró y su anulación en el turno en que se anuló. Todo abono y toda anulación exigen un turno
  abierto; anular un abono en efectivo se rechaza, sin revelar montos, si el esperado no alcanza. Los
  turnos cerrados antes de 0.9.0 no muestran el bloque. Ver [clientes-y-credito.md](clientes-y-credito.md).

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

## Corte X y Corte Z (0.12.0)

El menú **Caja** agrupa los cortes. Sus opciones pertenecen al módulo "Turnos y arqueo": sin licencia
el grupo desaparece y, al reactivarlo, los cortes registrados siguen ahí.

| Opción | Quién la ve | Qué hace |
|---|---|---|
| Corte X | Cajero y Administrador | Lectura parcial del turno abierto |
| Corte Z | Cajero y Administrador | Cierre del turno con arqueo (el mismo de arriba) |
| Histórico de cortes | Administrador | Lista, consulta y reimpresión de todos los cortes |

- **Corte X**: copia las cifras del turno abierto en ese momento (ventas, canceladas, formas de pago,
  devoluciones, crédito, ingresos, retiros y efectivo esperado) **sin modificar el turno**: no pide
  conteo, no cambia la versión del turno y no interrumpe la venta en curso. Se pueden hacer los que
  se quieran; cada uno recibe su folio. El Administrador lo genera directamente; el **Cajero necesita
  la autorización de un Administrador** (permiso `GenerateShiftReadout`, autorizable), que queda en el
  corte y en la bitácora. El ticket dice "CORTE X" y "LECTURA PARCIAL - NO ES CIERRE DE CAJA".
- **Corte Z**: todo cierre de turno (propio o ajeno) es un Corte Z. En la misma transacción del cierre
  se crea el corte con el siguiente folio Z y una copia de la instantánea del turno (esperado, contado,
  diferencia y comentario). Después ya no se puede vender en ese turno y el siguiente empieza en cero.
  El ticket del cierre dice "CORTE Z" y su folio.
- Los cortes **no se modifican ni se borran** (la base rechaza cualquier `UPDATE` o `DELETE`).
- No hay "gran total" acumulado: la continuidad entre Cortes Z se verifica con su folio consecutivo.

### Folios

`X-000001` y `Z-000001`, consecutivos **por tipo**, sin huecos ni repeticiones, y nunca se reinician.
El número se calcula como `MAX + 1` dentro de la transacción de escritura (`BEGIN IMMEDIATE`) y el
índice único `(Type, Number)` es la última defensa. Si un corte falla (por ejemplo, el cierre devuelve
"El turno cambió") la transacción se revierte y **no se consume folio**. De dos Corte Z simultáneos
sobre el mismo turno solo uno se completa; el otro recibe "El turno ya está cerrado".

Los turnos cerrados antes de 0.12.0 no tienen Corte Z: siguen en "Turnos" con su "CORTE DE CAJA" y no
aparecen en el histórico.

### Histórico de cortes

Filtros por tipo, fechas (hoy por omisión) y usuario que generó el corte; del más reciente al más
antiguo, en páginas de 100. Doble clic o **Ver** abre el corte con las mismas cifras con que se generó
y **Reimprimir** lo imprime con la leyenda REIMPRESIÓN (queda en la bitácora como `SHIFT_CUT_REPRINTED`).
Si la impresora no estaba disponible al generar un corte, el corte ya quedó registrado y se reimprime
desde aquí.

### Verificación de huecos

Con la aplicación cerrada:

```bash
sqlite3 pos.db 'SELECT "Type", COUNT(*), MIN("Number"), MAX("Number") FROM "ShiftCuts" GROUP BY "Type";'
```

Para cada tipo debe cumplirse `MIN = 1` y `COUNT = MAX`. Cada turno cerrado desde 0.12.0 tiene
exactamente un Corte Z:

```bash
sqlite3 pos.db "SELECT COUNT(*) FROM ShiftCuts WHERE Type = 'Z' GROUP BY ShiftId HAVING COUNT(*) > 1;"   # sin filas
```

## Reimpresión del corte y comprobantes

- "Turnos" → detalle del turno cerrado → **Reimprimir corte** (sale con la leyenda REIMPRESIÓN y, si
  el turno tiene Corte Z, con el título "CORTE Z Z-000001"; el detalle muestra "Corte Z: Z-000001").
  Los Cortes X y Z también se reimprimen desde "Caja > Histórico de cortes".
- Cada ingreso o retiro ofrece **Imprimir comprobante** al registrarse; el administrador puede
  reimprimirlo desde la pestaña Movimientos del detalle.

## Bitácora

| Evento | Cuándo |
|---|---|
| `SHIFT_OPENED` | Apertura del turno (con el fondo) |
| `CASH_DEPOSIT` / `CASH_WITHDRAWAL` | Ingreso o retiro (con el autorizador, si lo hubo) |
| `SHIFT_CASH_COUNTED` | Cada conteo del arqueo |
| `SHIFT_CLOSED` / `SHIFT_CLOSED_BY_ADMIN` | Corte Z: cierre por el dueño o por un administrador (el detalle empieza con "Corte Z Z-000001") |
| `SHIFT_READOUT_GENERATED` | Corte X generado (con el autorizador, si lo hubo) |
| `SHIFT_CUT_REPRINTED` | Reimpresión de un Corte X o Z |
| `HELD_SALE_DISCARDED` | Venta conservada descartada al cerrar un turno ajeno |

## Ventas anteriores a 0.6.0

Las ventas hechas antes de esta versión no pertenecen a ningún turno (`CashShiftId` nulo). Se
conservan y se consultan igual que antes. Con el módulo Devoluciones activo (0.8.0) sí se pueden
cancelar o devolver: el reintegro en efectivo sale del turno abierto actual, que debe existir. Con el
módulo inactivo **no se pueden cancelar**: su efectivo no forma parte de ningún arqueo y cancelarlas
descuadraría el turno abierto sin razón.

## Diagnóstico

Con la aplicación cerrada:

```bash
sqlite3 pos.db "SELECT COUNT(*) FROM CashShifts WHERE Status = 'OPEN';"   # 0 o 1
sqlite3 pos.db "SELECT COUNT(*) FROM Sales WHERE CashShiftId IS NULL AND CreatedAt > (SELECT MIN(OpenedAt) FROM CashShifts);"   # 0
```

Aperturas, movimientos, conteos, cierres, Cortes X y Z (con `CutId`, folio y autorizador) y rechazos
también se registran en los logs (Serilog) con el turno, el usuario y los importes.

## Fuera de alcance

Varias cajas, conteo por denominaciones, gran total acumulado entre Cortes Z, facturación electrónica
(CFDI), depósitos bancarios y cancelación de ventas de turnos cerrados.
