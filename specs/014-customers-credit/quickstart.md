# Quickstart: validar Gestión de clientes y crédito

**Funcionalidad**: `014-customers-credit` | **Plan**: [plan.md](plan.md)

Esta guía prueba la funcionalidad de punta a punta. Las reglas están en
[data-model.md](data-model.md), y los contratos en [contracts/application-ports.md](contracts/application-ports.md)
y [contracts/ui.md](contracts/ui.md).

## Requisitos previos

- .NET 10 SDK.
- Una licencia con el módulo **Crédito y clientes** activo; además Turnos y Devoluciones para los
  escenarios 5 a 7.
- Un usuario Administrador (`admin`) y un Cajero (`cajero`).
- Una impresora configurada, o la impresora de archivo de 006.

## Compilar y probar

```bash
dotnet build -v q                                                      # 0 errores, 0 advertencias
dotnet test tests/Pos.Domain.Tests --verbosity quiet                   # reglas de crédito, FIFO, liquidación
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet           # casos de uso sobre SQLite, migración 0.9.0
dotnet test tests/Pos.ArchitectureTests --verbosity quiet              # capas
```

Al arrancar, la aplicación migra la base a 0.9.0 (crea `Customers`, `Receivables`, `ReceivableEntries`
y `CustomerPayments`) y respalda la base antes de migrar.

## Escenarios manuales

Todos los montos están en pesos. Plazo de pago: 30 días (predeterminado).

| # | Pasos | Resultado esperado |
|---|---|---|
| 1 | Como `cajero`, Clientes > Nuevo: "Ana", teléfono 555-0101 | Se crea "Solo efectivo", límite 0; la sección Crédito es de solo lectura (H1-7) |
| 2 | Como `admin`, editar "Ana": Crédito disponible, límite 1 000. Crear "Luis" con RUC 123 y luego otro cliente con RUC 123 | "Ana" queda con crédito; el segundo RUC se rechaza (H1-6) |
| 3 | Buscar "555-01" y luego "123" | Solo aparecen los clientes que coinciden (H1-2) |
| 4 | Como `cajero`, con un turno abierto: venta de 200 a crédito a Ana; después otra de 500 | Ambas quedan "Pendiente de pago" y el saldo de Ana es 700. El esperado del turno no cambia. El ticket muestra el cliente y "A crédito" (H2-1, H2-5) |
| 5 | Venta a crédito de 400 a Ana (700 + 400 > 1 000) | Aviso "excede por 100". Con la contraseña incorrecta no se registra; con la de `admin` sí, y la bitácora muestra `CREDIT_LIMIT_OVERRIDE` (H2-2, H2-3) |
| 6 | Clientes > Ana > Abonos > Registrar: 300 en efectivo; doble clic en "Registrar" | Un solo abono `AB-000001`; el saldo baja de 1 100 a 800; la venta de 200 pasa a "Pagada" (FIFO) y se imprime el recibo. El esperado del turno sube 300 (H3-1, H3-2, H3-5) |
| 7 | Intentar abonar 900 y luego 0 | Se rechazan con el máximo de 800 (H3-3) |
| 8 | Anular `AB-000001` con motivo y la contraseña de `admin` | El abono queda "Anulado" y visible; el saldo vuelve a 1 100; la venta de 200 vuelve a "Pendiente de pago"; el esperado baja 300 (H3-7) |
| 9 | Abonar 600 (paga la de 200 y 400 de la de 500); cancelar la venta de 500 (013) | Se reduce el saldo de esa venta en 100 y los 400 abonados de más se aplican a la venta de 400. No queda reintegro, porque aún había deuda (FR-016) |
| 10 | Vender 100 a crédito a Ana e intentar desactivarla; abonar 100 con tarjeta y desactivarla | Primero se rechaza indicando el saldo; después se desactiva y ya no aparece en el punto de venta (H1-4, H1-5) |
| 11 | Reportes > Créditos: filtrar Vencido / Al día / Al límite | Las filas y los totales coinciden con la suma de saldos de las fichas (H4, SC-006). Para probar "vencido", bajar el plazo a 1 día y usar una base de prueba con una venta a crédito antigua |
| 12 | Cerrar el turno | El corte muestra el bloque "Crédito" (incluido el abono con tarjeta por separado) y el esperado coincide con el cálculo manual (SC-007) |
| 13 | Sin turno abierto, intentar registrar un abono con tarjeta a otro cliente con saldo | Se rechaza con "Se requiere un turno abierto" (H3-4) |

## Verificaciones de integridad

- Para cada cuenta, `Receivables.BalanceCents` = `OriginalCents` + Σ `ReceivableEntries.AmountCents`.
  Lo cubre una prueba automática.
- No existe ningún `Receivable` con saldo negativo ni mayor que `OriginalCents`.
- Al forzar una falla después de crear la venta y antes de confirmar la transacción (prueba de
  Infrastructure), no queda ni venta, ni cuenta, ni movimiento de inventario (H2-6, SC-008).
