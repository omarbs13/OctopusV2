# Contrato: interfaz de usuario

Textos en español (`Strings.resx`, prefijo `Cut_`). Los ViewModels solo invocan los casos de uso
de [application-ports.md](application-ports.md); no calculan importes (Principio III).

## Menú "Caja" (grupo `cash`, orden 6)

Aparece después de Ventas. Sus tres opciones pertenecen al módulo "Turnos y arqueo"; sin licencia
el grupo desaparece (FR-020).

| Opción | Página | Permiso del menú | Quién la ve |
|---|---|---|---|
| Corte X | `cash.readout` | `OperateShift` | Cajero y Administrador |
| Corte Z | `cash.close` | `OperateShift` | Cajero y Administrador |
| Histórico de cortes | `cash.cuts` | `ManageShifts` | Administrador |

## Caja > Corte X (`CashShifts/ShiftReadoutView`)

- **Sin turno abierto**: "No hay un turno abierto. El Corte X se genera sobre el turno abierto de la
  caja." Botón deshabilitado.
- **Con turno**: "Turno T-000123 de {nombre} · desde {hora}" y el botón "Generar Corte X". Antes de
  generar no se muestra ninguna cifra.
- **Generar**:
  - `Forbidden(CanBeAuthorized: true)` (Cajero): se abre
    `AdminAuthorizationService.RequestAsync(GenerateShiftReadout, "Corte X del turno T-000123")` y se
    reintenta con la concesión. Si se cancela: "Se requiere autorización de un administrador".
  - `ShiftRequired`: se muestra el estado sin turno.
  - Éxito: se muestra el reporte (ver "Vista del corte") con el aviso "Lectura parcial: no es un
    cierre de caja" y el botón "Imprimir".
- Cada clic en "Generar Corte X" crea un corte nuevo con su folio.

## Caja > Corte Z (`CashShifts/ShiftClosingView`)

- **Sin turno**: "No hay un turno abierto."
- **Con turno**: "Turno T-000123 de {nombre} · desde {hora}" y el botón "Hacer Corte Z".
  - Turno ajeno: solo el Administrador ve el botón (como "Cerrar ese turno" en 008).
- El botón abre `CashShiftDialogs.CloseShiftAsync` (los tres pasos de 008 sin cambios de lógica).

## Diálogo de cierre (cambia, `CloseShiftView`)

- Título "Corte Z" en lugar de "Cerrar turno"; botón final "Confirmar Corte Z".
- Al terminar: "Corte Z Z-000001 · Turno T-000123 cerrado". Imprime automáticamente con
  `PrintSource.ShiftCut(cutId)`; si falla, "Reintentar" y el corte ya está guardado.
- El botón "Cerrar turno" del Punto de venta y del detalle de turno pasa a "Corte Z".

## Caja > Histórico de cortes (`CashShifts/ShiftCutsView`)

- **Filtros**: Tipo (Todos, Corte X, Corte Z), Desde y Hasta (fecha local; hoy por omisión),
  Usuario (Todos o un usuario). "Buscar".
- **Columnas**: Tipo, Folio, Fecha y hora, Usuario, Turno, Total vendido, Diferencia (solo Z, con
  "Sobrante" / "Faltante" / "Cuadrado"; "—" en X).
- Orden del más reciente al más antiguo; paginación de 100 con "Anterior" / "Siguiente" y
  "Página N de M", como "Turnos".
- Doble clic o "Ver": abre la vista del corte.
- Sin resultados: "No hay cortes con estos filtros."

## Vista del corte (`CashShifts/ShiftCutDetailView`, diálogo)

Muestra `GetShiftCut`; las mismas cifras que el ticket:

- Encabezado: "Corte X X-000004" o "Corte Z Z-000001", turno, caja, dueño del turno, generado por
  (y "Autorizó: {nombre}" si aplica), apertura y fecha y hora del corte.
- Bloques: fondo inicial, ventas y canceladas, total vendido, formas de pago, devoluciones y
  reintegros, crédito (ventas a crédito, abonos, anulaciones), ingresos, retiros, efectivo esperado.
- Solo Z: contado, diferencia con su etiqueta y comentario.
- Solo X: leyenda "Lectura parcial: no es un cierre de caja".
- Botones: "Imprimir" (recién generado) o "Reimprimir" (desde el histórico, `IsReprint = true`) y
  "Cerrar".

## Ticket impreso (`ShiftTicketBuilder.BuildCut`)

```text
        {encabezado del negocio}
              CORTE X                 | CORTE Z
   LECTURA PARCIAL - NO ES CIERRE     | (sin leyenda)
         [REIMPRESIÓN si aplica]
Corte                       X-000004
Turno                       T-000123
Caja                          Caja 1
Usuario: {dueño del turno}
Generado por: {usuario}
Autorizó: {administrador}            (solo si aplica)
Apertura: dd/MM/yyyy HH:mm
Fecha corte: dd/MM/yyyy HH:mm
--------------------------------
{mismas filas de cifras que el corte de 008}
Efectivo esperado            $X
Contado / Diferencia / Comentario    (solo Z)
```

## "Turnos" (008) — cambios menores

- Detalle de un turno cerrado: muestra "Corte Z: Z-000001" cuando existe.
- El corte impreso desde "Turnos" lleva "CORTE Z" y el folio Z si existe; los turnos anteriores a
  0.12.0 conservan "CORTE DE CAJA".
