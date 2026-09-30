# Contrato: formato del ticket

Ancho: **32 columnas** en papel de 58 mm y **48 columnas** en papel de 80 mm (fuente A). La
misma disposición se guarda en la impresora virtual como texto. Codificación de la impresora:
CP858.

## Estructura (de arriba abajo)

1. Logotipo centrado, si existe (ajustado al ancho en puntos: 384 o 576).
2. Nombre comercial (centrado, negrita), dirección, teléfono y RFC (si existe), centrados y
   ajustados por palabras.
3. Leyendas, si aplican: `CANCELADA` y/o `REIMPRESIÓN` (centradas, negrita).
4. Folio y fecha y hora locales (`dd/MM/yyyy HH:mm`).
5. `Cajero: <nombre completo>` bajo la fecha (desde 007; se recorta al ancho del papel; no sale
   en el ticket de prueba).
6. Separador de guiones.
7. Una entrada por línea de venta (ver "Renglones").
8. Separador.
9. `TOTAL` alineado a la derecha, en negrita.
10. Un renglón por forma de pago con su monto. El pago en efectivo muestra el recibido y, si
    hubo, `CAMBIO`.
11. Mensaje de pie, centrado y ajustado, si existe.
12. Avance de papel y corte.

## Renglones

- Primera línea: `<cantidad> <descripción>`. La cantidad usa los decimales de la unidad de la
  línea (0 para piezas, 3 para kg, por ejemplo).
- El importe va alineado a la derecha en la **última** línea de la descripción; la columna del
  importe nunca se corta ni se mezcla con el texto.
- Si la descripción no cabe, continúa en las líneas siguientes con sangría bajo la descripción.
- Una palabra más larga que el espacio disponible se parte, sin perder caracteres.

Ejemplo en 32 columnas:

```text
        MI TIENDA
   Calle 1 #23, Col. Centro
      Tel. 555 123 4567
--------------------------------
Folio: V-000123
30/09/2026 14:05
Cajero: Ana López
--------------------------------
2   Refresco cola 600 ml   $36.00
1.250 Queso oaxaca de rancho
      tradicional          $187.50
--------------------------------
TOTAL                     $223.50
Efectivo recibido         $300.00
CAMBIO                     $76.50
--------------------------------
   Gracias por su compra
```

## Reglas

- Los importes salen de los centavos guardados en la venta; el ticket no recalcula.
- Se usa el nombre y el precio guardados en la línea al momento de la venta, no los del catálogo.
- Cada forma de pago (efectivo, tarjeta, transferencia) aparece con su monto; el cambio solo
  cuando el efectivo lo generó.
- El ticket de prueba usa el folio `PRUEBA`, líneas de ejemplo y no consume folio.
- Impresión en orden: cada ticket es un solo trabajo, así que dos tickets no se entremezclan.

## Comandos ESC/POS emitidos

| Comando | Uso |
|---|---|
| `ESC @` | Inicializar |
| `ESC t 19` | Tabla CP858 |
| `ESC a n` | Alineación (0 izquierda, 1 centro, 2 derecha) |
| `ESC E n` | Negrita |
| `GS v 0` | Logotipo raster |
| `LF` × 4 y `GS V 66 0` | Avanzar y cortar |
| `ESC p 0 25 250` | Pulso del cajón (pin 2) |
