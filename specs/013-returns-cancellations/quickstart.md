# Quickstart: validar Devoluciones y cancelaciones

Guía de validación de extremo a extremo. Las reglas y firmas están en
[contracts/application-ports.md](contracts/application-ports.md), las pantallas en
[contracts/ui.md](contracts/ui.md) y el esquema en [data-model.md](data-model.md).

## Requisitos

- SDK de .NET 10 (`global.json`) y `dotnet tool restore` (para `dotnet ef`).
- Un administrador y un cajero (007), con un turno abierto por el cajero (008), y productos con
  inventario controlado (004).
- Impresora configurada o la impresora virtual (006) para ver el ticket de la nota de crédito.
- Para la migración: `tests/Pos.Infrastructure.Tests/SampleDatabases/` con las bases hasta `v0.7.0.db`.

## 1. Compilar y probar

```bash
dotnet build -v q                     # 0 errores, 0 advertencias
dotnet test --verbosity quiet         # en CI, suite completa; localmente, el proyecto modificado
```

Pruebas que deben existir y pasar (research §15): `ReturnMathTests` (SC-004),
`SaleReturnTests`, `CreditNoteTests`, `CashShiftMathTests` ampliada, `ReturnsUseCaseTests` sobre SQLite
real (cancelación, parcial, nota, concurrencia, autorización, plazo, atomicidad), consistencia de
inventario y `ReturnsMigrationTests` con el SQL sin reconstrucciones.

## 2. Escenarios manuales

1. **Cancelación con reintegro en efectivo**: vender 2 productos pagados en efectivo, ir a "Consultar
   ventas" → detalle → "Cancelar venta", motivo, *Reintegro*, autorizar con la contraseña del
   administrador. Verificar: venta "Cancelada", existencias restauradas con "Devolución por venta
   cancelada", efectivo esperado del turno reducido (arqueo) y entrada en la bitácora con autorizador.
2. **Cancelación con tarjeta**: debe quedar un reintegro pendiente en "Devoluciones y vales", sin
   cambiar el efectivo. Marcarlo como reversado.
3. **Pago mixto, devolución parcial**: vender con efectivo y tarjeta; devolver una línea y una parte de
   otra. El reparto debe ser proporcional y sumar exactamente el total (vista previa y resultado).
4. **Acumulado**: devolver el resto en una segunda devolución; la venta pasa a "totalmente devuelta" y
   ya no admite más.
5. **Nota de crédito**: cancelar con *Nota de crédito*; se imprime el ticket con folio y saldo. En una
   venta nueva, pagar con la nota (saldo parcial y combinada con efectivo); el saldo baja. Folio
   inexistente o sin saldo: mensaje de error.
6. **Autorización**: como cajero, la confirmación pide un administrador; como administrador, pide su
   propia contraseña. Una contraseña errónea no hace nada y queda en la bitácora.
7. **Turno cerrado**: cerrar el turno, abrir uno nuevo y cancelar una venta del anterior con efectivo: el
   reintegro se descuenta del turno nuevo y el cerrado no cambia.
8. **Plazo**: con plazo de 1 día, una venta de hace 2 días se rechaza; ampliarlo desde la configuración.
9. **Sin efectivo**: con el esperado menor al reintegro, se rechaza sin mostrar montos y se sugiere la
   nota de crédito.
10. **Licencia**: con la evaluación vencida y sin Devoluciones, solo existe la cancelación básica.

## 3. Migración

```bash
dotnet ef migrations list --project src/Pos.Infrastructure --startup-project src/Pos.Desktop
```

Debe terminar con `ReturnsAndCreditNotes`. La prueba de bases de ejemplo migra `v0.1.0.db` a
`v0.7.0.db` a la versión actual y verifica que las ventas ya canceladas conserven su efectivo
heredado y que el SQL de la migración no contenga reconstrucciones (`CREATE TABLE "ef_temp_…"`).
