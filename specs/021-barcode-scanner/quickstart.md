# Quickstart: validar la integración del escáner

**Feature**: `021-barcode-scanner` | **Plan**: [plan.md](plan.md)

## Requisitos

- Rama `021-barcode-scanner`, base de datos de desarrollo con un turno abierto.
- Un lector USB en modo teclado con sufijo Enter. Sin lector se puede simular con
  `xdotool type --delay 5 "PROD-0042" && xdotool key Return` (Linux) para una ráfaga, o escribiendo a
  mano para la escritura lenta.
- Etiquetas de prueba: EAN-13 `7501055300846`, EAN-8 `96385074`, CODE128 `ABC-12345`, CODE39
  `PROD-0042` (y la variante `*PROD-0042*` si el lector transmite los asteriscos).

## Compilación y pruebas

```bash
dotnet build -v q
dotnet test tests/Pos.Domain.Tests --verbosity quiet          # BarcodeTests
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet  # unicidad, búsqueda, migración v0.15.0
```

Esperado: 0 errores, 0 advertencias, todas las pruebas en verde.

Revisar el SQL de la migración (debe quedar vacío, sin reconstrucción de `Products`):

```bash
dotnet ef migrations script SuppliersAndPurchases ScannerBarcodeFormats \
  -p src/Pos.Infrastructure -s src/Pos.Infrastructure
```

## Escenarios

| # | Pasos | Resultado esperado | Spec |
|---|---|---|---|
| 1 | Crear 4 productos con los códigos de prueba; en otro intentar `abc-12345` | Los 4 se guardan (en mayúsculas); el quinto: "código de barras duplicado" | H1-5, FR-003/004 |
| 2 | Código `ABC_123` o de 49 caracteres en el formulario | Error en el campo Código de barras | H1-5 |
| 3 | Producto existente de 8–14 dígitos (base migrada) | Se abre y guarda sin cambios; se encuentra al escanear | FR-005 |
| 4 | Punto de venta, foco en captura: escanear los 4 códigos | Se agregan los 4 productos | H1-1..4, SC-001 |
| 5 | Clic en una línea (foco en la lista) o en un botón; escanear | Se agrega; no se pulsa el botón | H1-6, FR-006 |
| 6 | F4 (cantidad), teclear `3` y Enter | Cambia la cantidad; no se busca producto | H1-7, FR-007 |
| 7 | Escanear dos veces el mismo código | Una línea con cantidad 2 | H2-1/2, SC-002 |
| 8 | Escanear `7501055300847` (dígito incorrecto, sin producto) | "Código no válido: 7501055300847"; venta igual | H2-4 |
| 9 | Escanear `ZZZ-999` (sin producto); pulsar F2 | "Código no encontrado: ZZZ-999"; F2 deja el foco listo para escribir | H2-3/5, SC-005 |
| 10 | Producto A con SKU `PROD-0042` y B con código `PROD-0042`; escanear | Selector con A y B; Esc no cambia la venta | H2-6, FR-013 |
| 11 | Escanear un producto inactivo | Mensaje de 005, no se agrega | H2-7 |
| 12 | Ráfaga de 20 lecturas (`for i in $(seq 20); do xdotool type --delay 5 PROD-0042; xdotool key Return; done`) | Una línea con cantidad 20 | H2-8, SC-004 |
| 13 | Abrir cobro (F12) con foco en efectivo; escanear | Aviso "Lectura ignorada…"; el efectivo no cambia y el cobro no se confirma | FR-008 |
| 14 | Con el campo vacío pulsar `*`; luego escanear `*PROD-0042*` | `*` abre la cantidad; la lectura agrega el producto | research §9 |
| 15 | Venta con 2 líneas → Ayuda > Probar escáner; escanear EAN-13 existente, `ZZZ-999` y una lectura con un carácter alterado | Texto, formato, largo y producto correctos; "Formato no reconocido" con el carácter visible | H3-1/2 |
| 16 | En la prueba, enviar `ABC` sin Enter (`xdotool type --delay 5 ABC`) | "Sin Enter" con la recomendación | H3-3, FR-017 |
| 17 | 12 lecturas en la prueba; "Borrar" | Se ven las 10 últimas, la más reciente arriba; Borrar vacía | H3-5, FR-018 |
| 18 | Volver al Punto de venta | Las mismas 2 líneas, cantidades y total | H3-4, SC-007 |
| 19 | Iniciar sesión como Cajero → Acerca de | Se ve "Probar escáner" y funciona | H3-6 |

## Rendimiento

Con la base de 10 000 productos de 005, escanear un código del final del catálogo: la línea y el total
se actualizan en menos de 0.5 s (SC-003). Los tiempos aparecen en el log de `OperationRunner`.
