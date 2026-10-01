# Contrato: formato del ticket con descuentos

**Funcionalidad**: `015-discounts-promotions`. Extiende el formato de la spec 006.

`TicketBuilder` usa los importes guardados en la venta y no recalcula nada. Una venta sin descuentos
se imprime **exactamente igual** que hoy.

## Línea con descuento

Debajo de la línea normal (que muestra el importe original), se agrega un renglón con la etiqueta y el
monto negativo. Después va el importe final de la línea, alineado a la derecha:
`OriginalAmountCents − LineDiscountCents`. **No** se usa `AmountCents`, porque ya incluye la parte
repartida del descuento de venta o del cupón; con él, SUBTOTAL − descuento no daría el TOTAL.

```text
Refresco cola 600 ml
  2 x $18.00                  $36.00
  Desc. 10%                   -$3.60
                              $32.40
```

Con monto fijo, la etiqueta es `Desc.` sin valor: `Desc.  -$5.00`.

## Bloque de totales

Solo aparece si `DiscountCents > 0`:

```text
--------------------------------
SUBTOTAL                    $X.XX     (Σ importe de las líneas después de su descuento)
Descuento 5%               -$Y.YY     (o "Cupón VERANO10")
TOTAL                       $Z.ZZ
...pagos...
Usted ahorró:               $W.WW     (= DiscountCents)
```

- En 32 columnas, una etiqueta que no cabe se recorta, como el nombre del cajero.
- La reimpresión y el ticket de una venta cancelada muestran el mismo desglose, aunque el módulo ya no
  tenga licencia (FR-023).
