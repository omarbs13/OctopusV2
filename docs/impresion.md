# Impresión de ticket y cajón de dinero: guía para soporte

Guía para soporte técnico y desarrolladores. Describe cómo se imprime el ticket, cómo se abre el
cajón y dónde mirar cuando algo falla. La especificación completa está en
[specs/006-ticket-printing](../specs/006-ticket-printing/spec.md).

## Principio básico

La venta **siempre** se registra primero. La impresión y el cajón se ejecutan después, en segundo
plano y fuera de la transacción de la venta. Una falla de la impresora o del cajón muestra un aviso
(con **Reintentar** y **Continuar sin imprimir**) y nunca cambia ni duplica la venta. Reintentar
vuelve a imprimir la misma venta; no crea otra.

## Pantallas

| Pantalla | Acceso | Para qué |
|---|---|---|
| **Datos del negocio** | **Configuración → Datos del negocio** | Nombre comercial, dirección, teléfono, RFC, logotipo y mensaje de pie del ticket. Se guardan en la base de datos. |
| **Impresora** | **Configuración → Impresora** | Impresora (o impresora virtual), ancho de papel (58 u 80 mm), impresión automática, cajón automático e **Impresión de prueba**. Es local a cada computadora. |
| **Reimprimir** | Detalle de una venta | Reimprime con la leyenda `REIMPRESIÓN`; las ventas canceladas llevan además `CANCELADA`. |
| **Corte X / Corte Z** | **Caja → Corte X**, cierre de turno (**Caja → Corte Z**) e **Histórico de cortes** | El Corte X se imprime desde su vista ("Imprimir"); el Corte Z, automáticamente al cerrar el turno. Desde el histórico se reimprimen con la leyenda `REIMPRESIÓN`. Ver abajo. |
| **Abrir cajón** | Punto de venta | Abre el cajón sin venta; pide un motivo obligatorio que queda en la bitácora. Un Cajero necesita la autorización de un administrador (solo Administrador puede abrirlo directamente). |

## Encabezado del negocio

Desde 0.17.0 **todos** los tickets (venta, nota de crédito, abono, movimiento de caja, Corte X y Corte Z)
empiezan con el mismo encabezado, el mismo que usan los reportes PDF y XLSX:

```text
        [logotipo]                    (si existe y la impresora puede imprimirlo)
        Nombre comercial              (negrita)
        Dirección (ajustada al ancho)
        Tel. 555 123 4567
        RFC: XAXX010101000            (solo si existe)
```

Las líneas van centradas y ajustadas a 32 o 48 columnas. Un dato vacío se omite sin dejar línea en
blanco. Sin datos del negocio capturados, el ticket empieza directamente en su título, como antes.

## Tickets de Corte X y Corte Z

Los dos usan las cifras guardadas en el corte, sin recalcular: una reimpresión sale idéntica a la
original aunque el turno haya tenido más ventas.

```text
        {encabezado del negocio}
              CORTE X                 | CORTE Z
   LECTURA PARCIAL - NO ES CIERRE     | (sin leyenda)
   DE CAJA
         [REIMPRESIÓN si aplica]
Corte                       X-000004
Turno                       T-000123
Caja                          Caja 1
Usuario: {dueño del turno}
Generado por: {usuario}
Autorizó: {administrador}            (solo si aplica)
Apertura: dd/MM/yyyy HH:mm
Fecha corte: dd/MM/yyyy HH:mm
--------------------------------
{fondo, ventas, total vendido, formas de pago, devoluciones, crédito, ingresos, retiros}
Efectivo esperado            $X
Efectivo contado / diferencia / comentario   (solo Z)
```

- Imprimir un corte lo puede quien lo generó (el Cajero autorizado imprime su Corte X) o un
  Administrador.
- Cada **reimpresión** queda en la bitácora como `SHIFT_CUT_REPRINTED` (entidad `ShiftCut`, con tipo,
  folio y turno). La primera impresión no se registra aparte: la generación ya está en la bitácora.
- Si la impresora no está disponible, el corte ya quedó guardado: se ofrece **Reintentar** y se puede
  reimprimir después desde **Caja → Histórico de cortes**.
- El corte impreso desde **Ventas → Turnos** lleva el título `CORTE Z Z-000001` cuando el turno tiene
  Corte Z; los turnos cerrados antes de 0.12.0 conservan `CORTE DE CAJA`.

## Dónde se guarda cada cosa

| Qué | Dónde |
|---|---|
| Datos del negocio | Tabla `BusinessProfile` (una fila) en la base de datos; entra en los respaldos |
| Configuración de impresión | `<carpeta de datos>/preferences/printing.json` (por computadora; no entra en la base) |
| Tickets de la impresora virtual | `<carpeta de datos>/tickets/<aaaaMMdd-HHmmss>-<folio>.txt` |
| Aperturas de cajón sin venta | Bitácora de auditoría: acción `DRAWER_OPENED`, entidad `CashDrawer`, con motivo y resultado (`OK` o `FALLO`); `AuthorizedBy` si lo autorizó un administrador |
| Fallas de impresión y cajón | Log de Serilog en la carpeta `logs/` (operación, folio e impresora; sin datos sensibles) |

La ubicación de la carpeta de datos está en [carpeta-de-datos.md](carpeta-de-datos.md). Sin
`printing.json`, o si está dañado, se usan los valores predeterminados: sin impresora, 80 mm,
impresión automática desactivada y cajón automático activado.

## Impresora virtual (sin hardware)

1. **Configuración → Impresora**, elegir **Impresora virtual (archivo de texto)** y el ancho (58 mm
   = 32 columnas, 80 mm = 48 columnas). Guardar.
2. Pulsar **Impresión de prueba**: el aviso muestra la ruta del archivo en `tickets/`.
3. Con la impresora virtual el cajón no se abre: la apertura se simula y queda un registro
   informativo en el log.

Si la carpeta `tickets/` no tiene permisos o el disco está lleno, se trata como cualquier falla de
impresión (aviso con reintento).

## Impresora física

El ticket se envía en modo crudo (RAW) con comandos ESC/POS y tabla de caracteres CP858 (acentos y
"ñ"). La impresora debe estar instalada en el sistema operativo.

### Linux (CUPS)

- Listar: `lpstat -e`. La aplicación muestra esas impresoras.
- Enviar: `lp -d <impresora> -o raw` con los bytes por entrada estándar.
- Si `lp` o `lpstat` no están instalados, o CUPS no responde en 20 segundos, se trata como
  "sin impresoras" / impresora no disponible.
- Comprobar a mano: `echo prueba | lp -d <impresora> -o raw`.

### Windows (spooler)

- Usa `winspool.drv` (`EnumPrinters`, `OpenPrinter`, `StartDocPrinter` tipo `RAW`, `WritePrinter`).
- Instalar el controlador de la impresora y comprobar que aparece en **Impresoras y escáneres**.

## Cajón de dinero

- El cajón se conecta a la impresora (puerto RJ11) y se abre con el pulso ESC/POS `ESC p 0 25 250`
  enviado por la misma impresora.
- **Al cobrar**: se abre solo si algún pago es en efectivo y **Abrir el cajón automáticamente** está
  activo. No se audita. Si falla, aviso sin afectar la venta.
- **Sin venta**: botón **Abrir cajón** del Punto de venta; cualquier operador con sesión puede,
  con motivo obligatorio (hasta 200 caracteres). Siempre se audita, también si falla
  (`Resultado: FALLO`).

## Diagnóstico

1. Revisar el log del día en `logs/` buscando "No se pudo imprimir el ticket", "No se pudo abrir el
   cajón" o "CUPS no disponible" / "winspool falló".
2. Probar con la impresora virtual: si el archivo sale bien, el problema es la impresora o el
   sistema operativo, no el armado del ticket.
3. Consultar la bitácora para aperturas de cajón: acción `DRAWER_OPENED`.

## Verificación en hardware real

Una vez por sistema operativo, con una impresora térmica ESC/POS y cajón:

- La impresora aparece en la lista.
- Impresión de prueba en 58 mm o 80 mm: acentos y "ñ" correctos, logotipo, corte de papel.
- Cobro en efectivo con el cajón automático: abre. Cobro solo con tarjeta: no abre.
- **Abrir cajón** con motivo: abre y queda en la bitácora.

El código de Windows solo se compila en la integración continua de Linux; esta verificación manual
es la única prueba de ese adaptador.
