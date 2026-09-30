# Contrato: interfaz de usuario

Textos en español. Los ViewModels solo invocan los casos de uso de
[application-ports.md](application-ports.md) y presentan su resultado; no calculan importes
(Principio III). Todos los diálogos se muestran en `ModalHost`, como el cobro (005).

## Punto de venta (`Sales/PointOfSaleView`)

Al activarse, y después de cada error de turno de `ConfirmSale`, consulta `GetCurrentShift`:

| Estado | Qué se ve | Acciones |
|---|---|---|
| Sin turno | Panel "Abrir turno" en lugar del carrito. La captura está deshabilitada | Capturar el fondo inicial → "Abrir turno". Con 0: confirmación "¿Abrir el turno sin fondo inicial?" (Sí/No) |
| Turno de otro usuario | Panel "Hay un turno abierto de {nombre} desde {hora}. Debe cerrarse antes de vender" | Administrador: "Cerrar ese turno" (abre el cierre del turno ajeno). Cajero: ninguna; puede cerrar sesión desde el menú de usuario |
| Turno propio | Carrito normal y una barra superior: "Turno T-000123 · desde 08:15 · 23 ventas · $4,560.00" | "Ingreso", "Retiro" y "Cerrar turno" en la barra. Atajos: ninguno nuevo en esta fase |

La barra de turno **no** muestra fondo, efectivo esperado ni movimientos (FR-022, SC-005). Se
refresca tras cada venta o cancelación.

## Diálogo de movimiento de efectivo

- **Campos**: tipo (fijado por el botón que lo abrió), monto y motivo. El motivo tiene de 1 a 250
  caracteres; "Guardar" se deshabilita mientras falte.
- **Retiro de un cajero**: al guardar, `RegisterCashMovement` devuelve `Forbidden(CanBeAuthorized)`.
  Entonces se abre el diálogo de autorización de 007 (`AdminAuthorizationService.RequestAsync(WithdrawCash)`)
  y se reintenta con la concesión.
- **Rechazo por excedente**: el cajero ve "El retiro excede el efectivo disponible en caja". El
  administrador ve el mismo texto con "Disponible: $X".
- **Éxito**: se muestra "Movimiento T-000123-02 registrado" con los botones "Imprimir comprobante"
  y "Cerrar".

## Cierre de turno (tres pasos)

1. **Conteo**:
   - "Efectivo contado en caja", un solo campo de monto.
   - No hay ninguna cifra esperada en pantalla.
   - Antes de llamar a `CountShiftCash`, el Punto de venta espera el guardado del borrador. Si hay
     `SaleInProgress`, el diálogo se cierra con "Termine o cancele la venta en curso antes de cerrar
     el turno".
   - Si hay `HeldSaleWillBeDiscarded` (solo administrador con turno ajeno), aparece la confirmación
     "{nombre} tiene una venta en curso guardada. Si continúa, se descartará. ¿Continuar?" y se
     reintenta con `DiscardHeldSale = true`.
2. **Cifras**:
   - Efectivo esperado, contado y diferencia con la etiqueta "Sobrante", "Faltante" o "Cuadrado".
   - Tarjeta y transferencia.
   - Campo "Comentario", obligatorio si la diferencia no es cero: "Confirmar cierre" queda
     deshabilitado sin él.
   - "Volver a contar" regresa al paso 1. El nuevo conteo queda en la bitácora.
3. **Confirmación**:
   - `CloseShift`. Si devuelve `ShiftChanged`, se regresa al paso 2 con las cifras nuevas.
   - Al cerrar: "Turno T-000123 cerrado" e impresión automática del corte. Si la impresión falla,
     aparece el mensaje con "Reintentar" y el cierre ya está guardado.
   - El Punto de venta vuelve al estado "Sin turno".

## Pantalla "Turnos" (`CashShifts/ShiftsView`)

- **Menú**: grupo Ventas, orden 20, permiso `ManageShifts`. El cajero no la ve y la navegación
  directa devuelve `Forbidden`.
- **Filtros**:
  - Desde y hasta (fecha local de apertura; hoy por omisión).
  - Usuario ("Todos" y la lista de `ListCashiers` de 007).
  - Estado (Todos, Abierto, Cerrado).
- **Columnas**: Turno, Usuario, Apertura, Cierre, Total vendido, Diferencia.
  - La diferencia se muestra con signo y color: faltante en rojo, sobrante en ámbar.
  - En los turnos abiertos, la columna Diferencia queda vacía.
- **Paginación**: 100 por página, con los mismos controles que "Ventas realizadas".
- **Detalle** (doble clic o Enter), en un panel lateral como `SaleDetailView`:
  - Encabezado: turno, usuario, apertura, cierre, "Cerrado por" si fue otro usuario, y fondo.
  - Pestaña **Ventas**: folio, hora, total, formas de pago y estado. Abrir una venta lleva al
    detalle de venta existente.
  - Pestaña **Movimientos**: folio, hora, tipo, monto, motivo, usuario, autorizó y "Imprimir".
  - Pestaña **Arqueo**: esperado, contado, diferencia, comentario y totales por forma de pago. En
    un turno abierto muestra "Efectivo esperado al momento" y no hay conteo.
  - Botones: "Reimprimir corte" (cerrado) y "Cerrar turno" (abierto; abre el mismo diálogo de
    cierre).

## Tarjeta de Inicio (`CashShifts/CurrentShiftCard`)

- Métrica, orden 100, permiso `OperateShift`.
- **Con turno**: "Turno de {nombre}", "Desde {hora}" y "Total vendido $X". Al hacer clic lleva al
  Punto de venta.
- **Sin turno**: "No hay turno abierto", en estado vacío.

## Módulo

`CashShiftsModule.AddCashShiftsModule()` registra la página "Turnos", los diálogos (apertura,
movimiento, cierre), el detalle y la tarjeta. Se llama desde `HostBuilder` junto a `AddSalesModule`.
Los textos van en `Resources/Strings.resx`, con los prefijos `Shift_`, `CashMovement_` y
`Nav_Shifts`.
