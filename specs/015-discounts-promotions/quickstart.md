# Quickstart: validar Descuentos y promociones

**Funcionalidad**: `015-discounts-promotions` | Detalle en [data-model.md](data-model.md) y [contracts/](contracts/)

## Requisitos previos

- Instalación con la evaluación vigente o con la licencia del módulo "Descuentos y promociones".
- Un Administrador y un Cajero (spec 007), y un turno abierto del Cajero (spec 008).
- Productos de prueba: A a $50.00, B a $33.33 y C a $100.00, todos con existencia.

```bash
dotnet build -v q
dotnet test --verbosity quiet
dotnet run --project src/Pos.Desktop
```

## Escenarios

| # | Pasos | Resultado esperado |
|---|---|---|
| 1 | Como Administrador, "Descuentos > Configuración" | El límite es 10 %. |
| 2 | Como Cajero, vender 2 × A y aplicar a la línea 10 % (F7) | Se ve $100.00 tachado y $90.00, sin pedir autorización. |
| 3 | En la misma línea, cambiar a un monto de $15.00 | Pide autorización. Con la contraseña incorrecta la línea no cambia; con la correcta aplica $85.00. |
| 4 | Agregar 3 × B y aplicarle 15 % | El descuento es $15.00 y la línea queda en $84.99 (research §2). Pide autorización. |
| 5 | Cobrar en efectivo | El ticket muestra el desglose por línea, SUBTOTAL, TOTAL y "Usted ahorró" ([ticket-format.md](contracts/ticket-format.md)). |
| 6 | Nueva venta: 1 × C y 1 × A (subtotal $150.00); Shift+F7 con $15.00 | El total es $135.00, sin autorización (exactamente 10 %). |
| 7 | En la venta anterior, cambiar el descuento global a $60.00 (con autorización) y después quitar C | El subtotal baja a $50.00, que es menor que $60.00. El descuento global se retira con el aviso de [ui.md](contracts/ui.md). |
| 8 | Como Administrador, crear el cupón `PRUEBA10` (10 %, vigente hoy, 1 uso) | Aparece "Vigente", con 0 usos y 1 restante. |
| 9 | Como Cajero, capturar `prueba10` en el campo de productos | Se aplica el cupón, sin autorización. Si había un descuento global, primero pregunta si lo reemplaza. |
| 10 | Cobrar y, en otra venta, capturar `PRUEBA10` | "El cupón PRUEBA10 ya alcanzó su límite de usos." |
| 11 | Cancelar (013) la venta del paso 10 | El cupón vuelve a tener 1 uso restante. |
| 12 | Crear un cupón con el SKU de A como código | Se rechaza por coincidir con un producto. |
| 13 | Crear un cupón vencido ayer y capturarlo | "El cupón … venció el …". |
| 14 | Devolución parcial (013) de 1 de las 2 unidades de A del paso 2 | Devuelve $45.00, o la mitad del neto pagado. |
| 15 | Aplicar un descuento, cerrar sesión y volver a entrar | La venta conservada mantiene el descuento y la autorización. |
| 16 | "Descuentos > Reporte" de hoy | El total descontado es igual a la suma de "Usted ahorró" de los tickets. Filtrar por tipo y exportar. |
| 17 | "Reportes > Ventas" de hoy | Aparece la tarjeta "Total descontado" con el mismo valor. |
| 18 | Bitácora | Están las autorizaciones (y el intento fallido sin contraseña), el alta del cupón, el cambio de límite y la devolución del uso. |
| 19 | Simular una licencia sin el módulo (fecha de evaluación vencida) | No aparecen las acciones ni el menú de descuentos. Una venta conservada con descuento se recupera sin él y avisa. Los tickets anteriores reimprimen su desglose. |

## Pruebas automáticas mínimas

```bash
dotnet test tests/Pos.Domain.Tests --verbosity quiet
dotnet test tests/Pos.Application.Tests --verbosity quiet
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet   # migración y bases de ejemplo
dotnet test tests/Pos.ArchitectureTests --verbosity quiet
```

La cobertura esperada está en research §14.
