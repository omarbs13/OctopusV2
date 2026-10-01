# Descuentos y promociones: guía para soporte

Funcionalidad 015 (versión 0.10.0). Cubre los descuentos por línea, el descuento a la venta completa, los
cupones, la autorización de descuentos sobre el límite y el reporte de descuentos. Pertenece al módulo
**Descuentos y promociones** de la licencia (012). La especificación completa está en
[specs/015-discounts-promotions](../specs/015-discounts-promotions/spec.md).

## Conceptos

| Concepto | Qué es |
|---|---|
| **Descuento de línea** | Porcentaje o monto fijo sobre el importe de una línea (cantidad × precio). Uno por línea. |
| **Descuento de venta** | Porcentaje o monto fijo sobre el **subtotal** (líneas ya descontadas). Uno por venta. |
| **Cupón** (`Coupons`) | Código del Administrador que da un descuento de venta, con vigencia y límite de usos. Excluye al descuento de venta manual. |
| **Límite de descuento** | Porcentaje máximo que se aplica sin autorización. Preferencia local `discounts` (10 % por defecto). |
| **Aprobación** (`DiscountApprovals`) | Autorización de un Administrador para un descuento sobre el límite, ligada a la venta en curso. Inmutable. |
| **Descuento registrado** (`SaleDiscounts`) | Un renglón por descuento de la venta cobrada: tipo, modalidad, valor, monto, cupón, quién lo aplicó y quién lo autorizó. Inmutable. |

## Atajos y pantallas

| Acción | Dónde |
|---|---|
| Descuento de la línea seleccionada | **F7** o botón "Descuento (F7)" en el Punto de venta |
| Descuento a la venta | **Shift+F7** o botón "Descuento a la venta" |
| Aplicar un cupón | Capturar el código en el campo de productos, o botón "Aplicar cupón" |
| Cupones, límite y reporte | Menú **Descuentos** (solo Administrador) |

Sin la licencia del módulo los botones y el menú no aparecen, un código de cupón se trata como producto
no encontrado y la venta básica sigue igual.

## Cálculo

Todo es entero: importes en centavos y porcentajes en **puntos base** (10 000 = 100 %; 12.5 % = 1 250).

1. Importe de línea = cantidad × precio (mitad hacia arriba, como siempre).
2. Descuento de línea: porcentaje `(importe × pb + 5 000) / 10 000`, o el monto. No puede exceder el importe
   ni redondear a $0.00 (por ejemplo 1 % de $0.10): ambos se rechazan al aplicarlo.
3. Subtotal = Σ (importe − descuento de línea).
4. Descuento de venta sobre el subtotal. Un monto manual mayor que el subtotal se rechaza al aplicarlo; si
   después el subtotal baja por debajo del monto, el descuento se **retira** con un aviso. Un cupón de
   monto se limita al subtotal.
5. Total = subtotal − descuento de venta. Nunca es negativo; con un 100 % autorizado puede ser $0.00 y
   se cobra **sin pagos**.

Ejemplo verificado: 3 × $33.33 = $99.99; 15 % = 14.9985 → **$15.00**; final **$84.99**.

### Reparto del descuento de venta

Al cobrar, el descuento de venta (manual o cupón) se reparte entre las líneas en proporción a su
importe ya descontado, por **resto mayor** y con empate por orden de captura (`Proportional.Allocate`).
La suma repartida es exactamente el descuento.

Cada línea guarda:

| Columna | Significado |
|---|---|
| `OriginalAmountCents` | Cantidad × precio |
| `LineDiscountCents` | Descuento propio de la línea |
| `OrderDiscountCents` | Parte repartida del descuento de venta |
| `AmountCents` | **Neto pagado** = original − línea − venta |

Como `AmountCents` es lo pagado, las devoluciones (013) y los reportes que suman importes funcionan sin
cambios y devuelven neto de descuentos. En las ventas anteriores a 0.10.0 la migración copió
`AmountCents` en `OriginalAmountCents`.

El ticket y "Consultar ventas" muestran como importe final de la línea `OriginalAmountCents −
LineDiscountCents`; el descuento de venta aparece una sola vez, antes del TOTAL, para que
SUBTOTAL − descuento = TOTAL.

## Límite y autorización

- Un descuento **supera** el límite si `descuento × 10 000 > límite × base` (multiplicación cruzada, sin
  redondear). Un descuento **igual** al límite no necesita autorización.
- Al aplicar un descuento que lo supera:
  - Un **Cajero** captura usuario y contraseña de un Administrador (diálogo de 007). La concesión de 2
    minutos se consume enseguida con `ApproveDiscount`.
  - Un **Administrador** no captura contraseña, pero también pasa por `ApproveDiscount`.
  - En ambos casos queda una fila en `DiscountApprovals` con el porcentaje aprobado y la entrada
    `DISCOUNT_AUTHORIZED` en la bitácora.
- La aprobación se guarda en la venta conservada (`ApprovalId`), así que sobrevive al cierre de sesión.
- Al cobrar, `ConfirmSale` revalida cada descuento con el **límite vigente**. Cada uno que lo supera
  necesita la aprobación de su `ApprovalId`, que debe ser del mismo borrador, usuario y alcance y cubrir
  el porcentaje actual. Si falta (por ejemplo, porque bajó el límite o bajó la cantidad con un monto
  fijo), el cobro responde "requiere autorización", el Punto de venta la pide y reintenta una vez.
- Los intentos fallidos los registra `ADMIN_AUTHORIZATION_DENIED`, con el detalle del descuento (monto,
  alcance y venta en curso) y **sin** la contraseña. Cuentan para el bloqueo por intentos (007).

## Cupones

- Código de 3 a 30 caracteres `A-Z 0-9 -`, guardado en mayúsculas; se compara sin distinguir mayúsculas
  ni espacios de los extremos. Es único y **no puede coincidir** con el código de barras o el SKU de un
  producto no borrado. Si un código es de producto y de cupón, el campo de productos lo toma como producto.
- Estado con la **fecha local** del equipo, en este orden: inactivo > agotado > vencido > por iniciar >
  vigente. Los días de inicio y fin son válidos completos.
- El uso se cuenta **al cobrar**, en la misma transacción (`BEGIN IMMEDIATE`): dos ventas no pueden usar
  el último uso. La venta conservada revalida el cupón al retomarse y al cobrar.
- La **cancelación completa** de la venta devuelve el uso (`COUPON_USE_RELEASED`); la devolución parcial
  no. Sin licencia del módulo no se devuelve.
- Con usos registrados solo se cambian la vigencia, el límite (no menor que los usos) o el estado. Nunca
  se borran.

## Bitácora

| Acción | Cuándo |
|---|---|
| `DISCOUNT_AUTHORIZED` | Al aprobar un descuento sobre el límite (autorizador en `AuthorizedBy`) |
| `DISCOUNT_APPLIED_AUTHORIZED` | Al cobrar una venta con un descuento autorizado: folio, monto, aplicador y autorizador |
| `COUPON_CREATED` / `COUPON_UPDATED` / `COUPON_DEACTIVATED` | Alta, edición (y reactivación) y desactivación de cupones |
| `COUPON_USE_RELEASED` | Cancelación completa de una venta con cupón |
| `DISCOUNT_LIMIT_CHANGED` | Cambio del límite, con el valor anterior y el nuevo |

## Reportes

- **Reportes > Ventas**: tarjeta "Total descontado" (Σ `Sales.DiscountCents` de las ventas completadas).
  Se muestra aunque el módulo Descuentos no tenga licencia.
- **Descuentos > Reporte**: total descontado, cantidad y detalle por descuento (folio, fecha, cajero,
  tipo, valor, monto, autorizador, cupón), con filtros de período, cajero y tipo, y exportación PDF o
  Excel. Solo cuenta ventas completadas; el total sale de la misma consulta que la tabla.

Consulta para verificar que cada venta cuadra (debe devolver 0 filas):

```sql
SELECT s.Id, s.FolioNumber, s.TotalCents, s.DiscountCents
FROM Sales s
WHERE s.TotalCents <> (SELECT SUM(l.AmountCents) FROM SaleLines l WHERE l.SaleId = s.Id)
   OR s.DiscountCents <> IFNULL((SELECT SUM(d.AmountCents) FROM SaleDiscounts d WHERE d.SaleId = s.Id), 0)
   OR s.DiscountCents <> (SELECT SUM(l.OriginalAmountCents - l.AmountCents) FROM SaleLines l WHERE l.SaleId = s.Id);
```

## Licencia

El módulo **Descuentos y promociones** tiene el identificador opaco
`01a0f957-082e-72cb-9c5a-9cc8fbee6772` (`ModuleCatalog`). **La herramienta del proveedor que emite
licencias debe incluirlo** para venderlo; la evaluación lo incluye automáticamente.

Sin el módulo, las ventas ya cobradas conservan y muestran sus descuentos (ticket, reimpresión,
"Consultar ventas", total descontado del reporte de ventas). Las devoluciones y cancelaciones siguen
usando los importes descontados. Una venta conservada con descuentos se retoma sin ellos, con un aviso.

## Diagnóstico rápido

- Log `Venta no registrada: descuento sin aprobación que lo cubra. … LimitePb=…`: el límite o el descuento
  cambiaron desde que se aplicó; el cajero debe autorizar de nuevo.
- Log `Venta no registrada: el cupón ya no es válido. … Estado=…`: el cupón venció, se agotó o se
  desactivó mientras la venta estaba en curso.
- Log `Descuento autorizado. ApprovalId=… AutorizadoPor=…` y `Venta con descuentos. … DescuentoCents=…`:
  rastro de cada descuento autorizado y de cada venta con descuentos.
- Un cliente reporta un descuento "que desapareció": revise si era un monto fijo de venta y el subtotal
  bajó (se retira con aviso), o si la venta conservada se retomó sin licencia.
