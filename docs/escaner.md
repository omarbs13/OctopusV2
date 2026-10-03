# Lector de códigos de barras: guía para soporte

Guía para soporte técnico. Describe qué lectores funcionan, cómo se registran los códigos en el catálogo
y cómo diagnosticar un lector en el mostrador (versión 0.15.0). La especificación completa está en
[specs/021-barcode-scanner](../specs/021-barcode-scanner/spec.md). El comportamiento en la venta está en
[ventas.md](ventas.md#lector-de-códigos-de-barras-0150).

## Requisitos del lector

- **Modo teclado** (HID): el sistema lo reconoce como un teclado. Sirve cualquier lector USB o inalámbrico
  con receptor USB; no hace falta instalar controladores ni configurar nada en la aplicación. Los lectores
  en modo serie (COM) o con controlador propio no están soportados.
- **Sufijo Enter**: el lector debe enviar Enter al final de cada lectura (es lo habitual de fábrica). Si
  envía Tab o nada, el Punto de venta no agrega el producto; la pantalla de prueba lo indica.
- **Distribución de teclado**: el lector "escribe" con la distribución configurada en el sistema
  operativo. Si el lector está programado para una distribución distinta (por ejemplo, inglés EE. UU.
  contra español), los dígitos suelen llegar bien pero los guiones, las letras o los símbolos cambian
  (`-` llega como `'` o `?`). Síntoma: los códigos numéricos se encuentran y los alfanuméricos dan "Código
  no válido". Se ve en la pantalla de prueba: el texto leído no coincide con el impreso. Solución:
  programar el lector con la misma distribución que el sistema (hoja de códigos de configuración del
  fabricante) o cambiar la distribución del sistema.

## Formatos admitidos

| Formato | Cómo se reconoce |
|---|---|
| EAN-13 | 13 dígitos con dígito verificador correcto |
| EAN-8 | 8 dígitos con dígito verificador correcto |
| CODE128 / CODE39 | Cualquier otro texto que cumpla la regla del catálogo. Un lector en modo teclado no informa la simbología, por eso se muestran juntos |

El dígito verificador **no** se exige al registrar un producto (los códigos internos de 13 dígitos se
siguen aceptando y encontrando). Solo decide el aviso cuando una lectura no tiene coincidencias.

## Reglas del catálogo

El campo "Código de barras" del producto es opcional y, si se llena:

- admite de **1 a 48** caracteres: letras `A-Z`, dígitos `0-9`, espacios interiores y los símbolos
  `- . $ / + %` (el juego de CODE39, que también se representa en CODE128);
- se guarda en **mayúsculas**: `abc-1` y `ABC-1` son el mismo código y no pueden estar en dos productos;
- se quitan los espacios de los extremos y los **asteriscos de CODE39** cuando el código empieza y termina
  con `*` (`*ABC123*` se guarda `ABC123`);
- es único entre los productos no borrados.

Los códigos de versiones anteriores (8 a 14 dígitos) cumplen esta regla y no cambian.

## Probar el escáner

Menú **Configuración → Probar escáner** (desde 0.17.0; antes estaba en Ayuda y en **Acerca de**). Está
disponible para todos los roles: un Cajero ve el grupo Configuración solo con esta opción. **No modifica la
venta en curso** ni el borrador.

Escanee un código y la pantalla muestra:

| Dato | Qué revisar |
|---|---|
| Texto leído | Lo que llegó, con los espacios como `·` y los caracteres invisibles como `[TAB]` o `[U+XXXX]`. Debe coincidir con lo impreso |
| Formato | EAN-13, EAN-8, CODE128 / CODE39 o "Formato no reconocido" |
| Caracteres | Largo recibido, sin el terminador |
| Terminó con | Enter es lo correcto. Con Tab o "Sin Enter" (300 ms sin teclas) aparece "Configure el lector para enviar Enter al final de cada lectura" |
| Velocidad | "Escáner" si llegó en ráfaga (pausas de 50 ms o menos); "Escritura manual" si no. Un lector inalámbrico lento o un equipo muy ocupado puede dar "Escritura manual": la lectura funciona igual, pero en la venta también busca por nombre |
| Producto | Nombre y SKU del producto con ese código, "(inactivo)" o "Sin producto con este código" |

Se conservan las últimas 10 lecturas (botón **Borrar**); se pierden al cerrar la sesión.

## "Código no válido" o "Código no encontrado"

En el Punto de venta, una lectura sin producto ni cupón muestra uno de dos avisos:

- **Código no válido**: el texto no puede ser un código del catálogo: trae caracteres no admitidos (por
  ejemplo `_` o `'`, típico de una distribución de teclado equivocada), más de 48 caracteres, o tiene 8
  o 13 dígitos con dígito verificador incorrecto (lectura dañada). Revisar con la pantalla de prueba.
- **Código no encontrado**: el código tiene forma válida pero ningún producto lo tiene. Registrar el código
  en el producto o buscarlo con **F2** por nombre o SKU.

En el log quedan como `Código sin coincidencias en el Punto de venta` con el texto, el formato y si fue
escaneo (nivel información).
