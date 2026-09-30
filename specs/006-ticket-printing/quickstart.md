# Quickstart: validar impresión de ticket, cajón y datos del negocio

Guía de validación de extremo a extremo. El detalle de tipos y formatos está en
[data-model.md](data-model.md) y [contracts/](contracts/).

## Requisitos

- .NET 10 SDK. Sin impresora física: basta la impresora virtual.
- Una base con al menos un producto con precio (por ejemplo, con los productos de 003 y 005).

## Compilar y probar

```bash
dotnet build -v q
dotnet test --verbosity quiet
```

Resultado esperado: 0 errores y 0 advertencias, con todas las pruebas en verde, incluidas la
migración de las bases de ejemplo y las de arquitectura.

## Escenarios manuales (impresora virtual)

1. **Datos del negocio (US1)**: **Configuración → Datos del negocio**. Deja la dirección vacía y
   guarda: se indica el campo. Captura nombre, dirección, teléfono y pie, guarda, cierra y abre
   la aplicación: los datos siguen ahí. Intenta un logotipo que no sea imagen: se rechaza y se
   conserva el anterior.
2. **Impresora (US2)**: **Configuración → Impresora**. Elige la impresora virtual y 58 mm; pulsa
   la prueba. Debe aparecer un archivo `.txt` en `tickets/` de la carpeta de datos y el mensaje
   con su ruta. Repite con 80 mm y compara el ancho (32 y 48 columnas).
3. **Ticket al cobrar (US3)**: activa la impresión automática. Cobra una venta con un producto de
   nombre muy largo y pagos mixtos (efectivo y tarjeta). Revisa el archivo: encabezado, folio,
   fecha, líneas alineadas, total, cada forma de pago, cambio y pie, sin texto perdido.
4. **Falla de impresión (US3, SC-002)**: elige una impresora física inexistente (o quita los
   permisos de escritura de `tickets/`) y cobra. La venta debe quedar registrada en **Ventas
   realizadas**; se muestra el aviso con **Reintentar** y **Continuar sin imprimir**. Corrige y
   pulsa **Reintentar**: se imprime sin duplicar la venta.
5. **Reimpresión y cancelada (US4)**: en el detalle de una venta pulsa **Reimprimir**: el ticket
   lleva `REIMPRESIÓN`. Cancela la venta y reimprime: lleva `CANCELADA`.
6. **Cajón (US5)**: con la opción automática activa, cobra en efectivo (con la impresora virtual
   se registra la apertura simulada en el log) y cobra solo con tarjeta (no debe abrir).
   Pulsa **Abrir cajón**, deja el motivo vacío: no abre. Captura un motivo: abre y existe una
   entrada `DRAWER_OPENED` en la bitácora con usuario, fecha y motivo.
7. **Sin bloqueo (SC-005)**: cobra tres ventas seguidas; el Punto de venta queda libre de
   inmediato y los tres tickets salen en orden.

## Verificación en hardware real

Una vez por sistema operativo, con una impresora térmica ESC/POS con cajón:

- **Linux**: instala la impresora en CUPS y comprueba que aparece en la lista (`lpstat -e`).
- **Windows**: instala el controlador y comprueba que aparece en la lista.
- En ambos: impresión de prueba en 58 mm o 80 mm, acentos y "ñ" correctos, corte de papel y
  apertura del cajón.

## Base de datos

- La migración `BusinessProfile` solo crea la tabla. Revisa su SQL antes de integrarla
  (`dotnet ef migrations script`) y confirma que no reconstruye otras tablas.
