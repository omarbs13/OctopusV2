# Quickstart: validar Turnos de caja

Guía de validación de extremo a extremo. Las reglas y firmas están en
[contracts/application-ports.md](contracts/application-ports.md), las pantallas en
[contracts/ui.md](contracts/ui.md) y el esquema en [data-model.md](data-model.md).

## Requisitos

- SDK de .NET 10 (`global.json`) y `dotnet tool restore` (para `dotnet ef`).
- Un administrador y un cajero creados (007). Para empezar desde cero, basta con mover `pos.db` de
  la carpeta de datos ([docs/carpeta-de-datos.md](../../docs/carpeta-de-datos.md)) y pasar por el
  asistente.
- Impresora configurada, o la impresora virtual (006), para ver el corte y los comprobantes.
- Para la migración: `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.6.0.db` (se genera en esta
  funcionalidad, research §15).

## 1. Compilar y probar

```bash
dotnet build -v q                     # 0 errores, 0 advertencias
dotnet test --verbosity quiet         # suite completa (en CI; localmente, el proyecto modificado)
```

Pruebas que deben existir y pasar (research §16):

- `Pos.Domain.Tests/CashShifts/`: `CashShiftMathTests` (SC-003) y `CashShiftTests`.
- `Pos.Domain.Tests/Users/RolePermissionsTests`, actualizada con los permisos nuevos.
- `Pos.Infrastructure.Tests/CashShifts/CashShiftUseCaseTests`: venta ligada al turno (SC-001),
  cancelación, movimientos, retiros con autorización y cierre, con los casos de uso reales sobre
  SQLite (se ubicaron aquí y no en `Pos.Application.Tests` para no duplicar repositorios en memoria).
- `Pos.Application.Tests/Security/RestrictedOperationsTests`, con los casos de uso nuevos.
- `Pos.Infrastructure.Tests/CashShifts/CashShiftPersistenceTests`: totales por consulta, apertura
  simultánea (SC-002) e inmutabilidad.
- `Pos.Infrastructure.Tests/SampleDatabases/`: `SampleDatabaseUpgradeTests` (incluye `v0.6.0.db`)
  y `CashShiftsMigrationTests`.

## 2. Revisar la migración

```bash
dotnet ef migrations script UsersAndRoles CashShifts --project src/Pos.Infrastructure -o migracion.sql
```

Verificar en el SQL:

1. `CREATE TABLE "CashShifts"` y `CREATE TABLE "CashMovements"`. Esta última con su llave foránea
   hacia `CashShifts`.
2. `CREATE UNIQUE INDEX "IX_CashShifts_OpenPerRegister" … WHERE "Status" = 'OPEN'`.
3. `ALTER TABLE "Sales" ADD "CashShiftId" TEXT NULL`, **sin** `ef_temp_Sales` ni `DROP TABLE`.
4. `CREATE INDEX "IX_Sales_CashShiftId_Status"`.

## 3. Escenarios manuales

| # | Pasos | Resultado esperado | Cubre |
|---|---|---|---|
| 1 | Cajero inicia sesión y entra al Punto de venta | Panel "Abrir turno"; no se puede capturar productos | H1-1, FR-002 |
| 2 | Captura fondo `0` y confirma | Pide "¿Abrir el turno sin fondo inicial?"; al aceptar, abre | H1-3 |
| 3 | Cierra el turno del paso 2 (conteo 0), abre otro con fondo `500.00` | Turno abierto a su nombre; barra "Turno T-… · desde … · 0 ventas · $0.00" sin fondo | H1-2, FR-022 |
| 4 | Vende $120.00 en efectivo con $200.00 recibidos (cambio $80.00) | La venta tiene el turno y el cajero en su detalle; la barra muestra 1 venta y $120.00 | H2-1 |
| 5 | Vende $300.00 con $100.00 de tarjeta y $200.00 en efectivo | Total vendido $420.00 | FR-019a |
| 6 | Registra un ingreso de $50.00 "Cambio" e imprime el comprobante | Comprobante "INGRESO DE EFECTIVO T-…-01"; entrada `CASH_DEPOSIT` en la bitácora | H4-1, H4-5 |
| 7 | Intenta un retiro de $10,000.00 | Pide autorización de administrador; tras autorizar: "El retiro excede el efectivo disponible en caja", **sin** montos | H4-2, H4-3 |
| 8 | Retiro de $600.00 "Resguardo", autorizado | Se registra; la bitácora tiene `CASH_WITHDRAWAL` con autorizador | H4-3, FR-027 |
| 9 | Retiro de $150.00 (esperado: 500 + 120 + 200 + 50 − 600 − 150 = $120.00). Administrador intenta cancelar la venta del paso 5 ($200.00 en efectivo) | Rechazo sin montos: "No hay efectivo suficiente en caja para devolver esta venta…" | FR-008, clarificación 1 |
| 9b | Ingreso de $100.00 y reintentar la cancelación | Se cancela; el esperado baja a $20.00 (se verá en el cierre) | H2-3, SC-003 |
| 10 | Con productos en el carrito, "Cerrar turno" | "Termine o cancele la venta en curso…" | H3-1 |
| 11 | Carrito vacío, "Cerrar turno", conteo sin ver el esperado | El paso 1 no muestra cifras; el paso 2 muestra esperado, contado, diferencia, tarjeta y transferencia | H3-2, H3-3, SC-005 |
| 12 | Conteo distinto del esperado, sin comentario | "Confirmar cierre" deshabilitado; con comentario, cierra e imprime el corte con los campos de FR-019 | H3-4, H3-5 |
| 13 | Intentar cancelar una venta del turno cerrado desde "Ventas realizadas" | "La venta pertenece a un turno cerrado" | H2-4 |
| 14 | Cajero A abre un turno y deja una venta en el carrito; cambia de usuario a Cajero B | B ve "Hay un turno abierto de A…" y no puede vender ni abrir | H1-4, FR-005 |
| 15 | Administrador entra, "Cerrar ese turno" | Aviso de venta conservada de A → confirmar → cierre; bitácora con `HELD_SALE_DISCARDED` y `SHIFT_CLOSED_BY_ADMIN` | H1-5, FR-013 |
| 16 | Con un turno abierto, cerrar la aplicación y volver a iniciar sesión con el mismo usuario | Continúa en su turno, con las mismas ventas y total | H1-6, SC-007 |
| 17 | Administrador abre "Turnos", filtra por usuario y estado Cerrado, abre un detalle y reimprime | Ventas, movimientos y arqueo; el corte reimpreso dice "REIMPRESIÓN" con las mismas cifras | H5-1, H5-2, SC-006 |
| 18 | Cajero: menú sin "Turnos"; Inicio muestra la tarjeta del turno | Tarjeta con usuario, hora y total vendido; sin turno, "No hay turno abierto" | H5-3, H6 |

## 4. Verificación de integridad (opcional)

Con la aplicación cerrada:

```bash
sqlite3 pos.db "SELECT COUNT(*) FROM CashShifts WHERE Status = 'OPEN';"               # 0 o 1
sqlite3 pos.db "SELECT COUNT(*) FROM Sales WHERE CashShiftId IS NULL AND CreatedAt > (SELECT MIN(OpenedAt) FROM CashShifts);"   # 0
```
