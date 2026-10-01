# Contrato: interfaz (Pos.Desktop)

Todos los textos van en español en `Resources/Strings.resx` (prefijos `Customer_`, `Credit_`,
`Payment_`, `Nav_Customers`). Los ViewModels solo invocan los casos de uso de
[application-ports.md](application-ports.md) y no calculan saldos, disponibles, excedentes ni días
vencidos (Principio III). La autorización de Administrador usa el diálogo existente de 007.

## Navegación

| Grupo / página | Id | Orden | Permiso | Licencia |
|---|---|---|---|---|
| Grupo "Clientes" | `customers` | 6 (entre Ventas 5 y Reportes 7) | — | `CreditAndCustomers` |
| Clientes (lista y ficha) | `customers.list` | 0 | `ManageCustomers` | `CreditAndCustomers` |
| Reportes > Créditos | `reports.receivables` | 30 | `ViewReceivables` | `CreditAndCustomers` |

Se registran desde `CustomersModule.AddCustomersModule()` en `Composition/HostBuilder.cs`.

## Clientes: lista

- Campo de búsqueda, que busca mientras se escribe con retardo, por nombre, teléfono o RUC (FR-003,
  SC-001).
- Casilla "Mostrar inactivos".
- Columnas: nombre, teléfono, RUC, modalidad, límite, saldo y estado. Los inactivos se muestran
  atenuados.
- Botones: "Nuevo cliente" y doble clic o Enter para abrir la ficha.

## Clientes: alta y edición (formulario de 002)

- **Campos**: nombre*, teléfono*, email y RUC.
- **Sección "Crédito"**: modalidad ("Crédito disponible" / "Solo efectivo") y límite.
  - Con un Cajero la sección es de **solo lectura** y muestra "Solo efectivo, límite $0,00". El
    Administrador puede cambiarla (FR-020).
- **Errores** por campo:
  - nombre vacío;
  - teléfono vacío;
  - email inválido;
  - límite negativo;
  - "El RUC ya está registrado para otro cliente".
- **Conflicto de versión**: mensaje estándar de recarga.

## Clientes: ficha `[cliente]`

- **Encabezado**:
  - nombre, teléfono, email, RUC y modalidad;
  - límite, saldo pendiente, disponible y días vencido (si los hay).
- **Acciones**:
  - "Editar";
  - "Desactivar" / "Activar" (solo Administrador). Desactivar con saldo muestra `CustomerHasBalance`.
- **Pestaña "Ventas a crédito"**:
  - columnas: folio, fecha, monto, saldo, estado ("Pendiente de pago" / "Pagada" / "Cancelada") y
    días vencido;
  - abrir una fila lleva al detalle de venta existente.
- **Pestaña "Abonos"**:
  - columnas: folio, fecha, monto, forma de pago, referencia, saldo anterior y nuevo, y estado;
  - "Anulado" en rojo con el motivo en la información emergente.
  - Botones: "Registrar abono", "Reimprimir recibo" y "Anular" (este último visible con
    `RegisterCustomerPayments`; al usarlo siempre pide la autorización).

## Registrar abono (diálogo)

- **Campos**: monto*, forma de pago (Efectivo / Tarjeta / Transferencia) y referencia.
- **Datos que se muestran**: saldo actual y el texto "Se aplicará a las ventas más antiguas primero".
- **Validación**:
  - si el monto es > saldo, ≤ 0 o vacío, se muestra `PaymentExceedsBalance` con el máximo;
  - sin turno abierto se muestra `ShiftRequired` y el botón queda deshabilitado (FR-012).
- **Al abrir el diálogo** se genera el `RequestId`.
- **"Registrar"** se deshabilita mientras se procesa. Un reintento devuelve el mismo abono
  (doble clic).
- **Al confirmar**:
  1. imprime el recibo;
  2. si la impresión falla, muestra "Abono registrado. No se pudo imprimir el recibo; puede reimprimirlo
     desde el historial";
  3. actualiza la ficha.

## Anular abono (diálogo)

- **Datos y campos**: resumen del abono y motivo* (≤ 250).
- **Al confirmar** se abre el diálogo de autorización de Administrador, siempre, también si quien opera
  es Administrador.
- **Errores**:
  - `InvalidState` por una devolución posterior: "Este abono se aplicó a una venta que después se canceló
    o devolvió; no se puede anular";
  - `ShiftRequired`;
  - `InsufficientCash`, sin montos.

## Punto de venta y cobro

- **Botón "Cliente…"** en la barra del punto de venta (con `SellOnCredit` y licencia):
  - abre un buscador (`FindCustomersForSale`) y muestra el cliente elegido como etiqueta con "×" para
    quitarlo;
  - el cliente **no** se guarda en el borrador, para no cambiar `SaleDrafts`. Si la venta se recupera
    después de un cierre inesperado, el cliente se vuelve a elegir.
- **Cobro (`CheckoutView`)**:
  - "Venta a crédito" es una opción de pago visible solo si hay un cliente seleccionado.
  - Al elegirla se reemplazan los pagos capturados; se aplica la regla de no admitir pagos mixtos
    (FR-005).
  - El panel muestra los valores de `GetCustomerCreditStatus`: saldo, límite y disponible.
- **Avisos**:
  - con `HasOverdue`: "El cliente tiene ventas vencidas" (no bloquea);
  - con `WouldExceedByCents > 0`: "Excede el límite por $X; requiere autorización".
- **Confirmar**:
  - si `ConfirmSale` devuelve `CreditLimitExceeded`, se abre la autorización de Administrador (permiso
    `ApproveCreditOverLimit`) y se reintenta con la concesión;
  - si se cancela o la contraseña es incorrecta, no se registra nada (Historia 2, escenario 3).
- **Ticket**: el de crédito muestra "Cliente: {nombre}" y "A crédito: $X", sin cambio ni recibido.
- **Cliente "solo efectivo" o inactivo**: no aparece en el buscador. Si cambió mientras se cobraba,
  se muestra `CustomerNotEligibleForCredit`.

## Detalle de venta y devoluciones (013)

- **Detalle de venta a crédito**: bloque "Crédito" con cliente, saldo de esa venta y estado, con un
  enlace a la ficha del cliente.
- **Formulario "Devolver o cancelar"**:
  - en ventas a crédito oculta la opción "Nota de crédito";
  - muestra la vista previa de `CreditSettlement`: "Reduce el saldo en $X", "Se aplica a otras ventas
    del cliente $Y" y "Reintegro en efectivo $Z".

## Turnos y corte (008)

- El detalle del turno, "Mi turno" y el ticket de corte agregan un bloque "Crédito" con:
  - ventas a crédito;
  - abonos en efectivo;
  - abonos con tarjeta o transferencia;
  - anulaciones.
- El efectivo esperado ya los incluye y solo se muestra a quien hoy lo ve (regla de 008).

## Reportes > Créditos

- **Filtros**: estado (Todos / Al día / Vencido / Al límite) y texto. Botón "Actualizar".
- **Columnas**: cliente, saldo pendiente, límite, último abono, días vencido y etiquetas de estado.
- **Pie**: "Saldo total pendiente" y "Clientes con saldo", calculados sobre las filas visibles.
- **Configuración**: "Plazo de pago (días)", editable por el Administrador; guarda con
  `SaveReceivablesSettings`.
- Abrir una fila lleva a la ficha del cliente.
