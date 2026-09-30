# Carpeta de datos, respaldos y logs

Guía para desarrolladores y soporte técnico.

## Ubicación

| Sistema | Ruta |
|---|---|
| Windows | `%LOCALAPPDATA%\Pos` (por ejemplo `C:\Users\<usuario>\AppData\Local\Pos`) |
| Linux | `~/.local/share/Pos` |

La variable de entorno `POS_DATA_DIR` reemplaza esa ruta; es útil para pruebas y para soporte.
La ruta real siempre se ve en la pantalla **Acerca de**, con un botón para copiarla.

## Estructura

```text
Pos/
├── app.lock                     Bloqueo de instancia única (lo libera el sistema si el proceso muere)
├── logo.png                     Logotipo del cliente (opcional) para la pantalla de carga
├── preferences/
│   ├── navigation.json          Estado del menú lateral (contraído y grupos abiertos)
│   └── security.json            Tiempo de inactividad antes de bloquear la sesión (0 = desactivado)
├── data/
│   ├── pos.db                   Base de datos SQLite (modo WAL)
│   ├── pos.db-wal               Diario WAL; forma parte de la base mientras la app está abierta
│   └── pos.db-shm
├── backups/
│   ├── auto/                    Respaldos automáticos: se conservan los 7 más recientes
│   ├── pre-migration/           Respaldo antes de cada migración: se conservan los 5 más recientes
│   └── corrupt/<fecha>Z/        Bases dañadas apartadas al restaurar (no se borran solas)
└── logs/
    └── pos-AAAAMMDD.log         Log estructurado (un archivo por día, se conservan 31)
```

**Nunca copies `pos.db` a mano con la aplicación abierta**: en modo WAL, los cambios recientes
pueden estar en `pos.db-wal`. Para obtener una copia consistente usa **Acerca de → Exportar
diagnóstico**, o cierra la aplicación antes de copiar.

## Logotipo del cliente

La pantalla de carga muestra el logotipo predeterminado del POS. Para usar el del cliente, copia
un archivo **PNG** llamado `logo.png` en la raíz de la carpeta de datos y reinicia la aplicación.
No hace falta recompilar.

- Se muestra a un máximo de 160 × 160 píxeles, conservando la proporción. Se recomienda una
  imagen cuadrada de 320 × 320 con fondo transparente.
- Si el archivo no existe, no es un PNG válido o no se puede leer, se usa el logotipo
  predeterminado y se registra una advertencia en el log.

## Preferencias del menú

`preferences/navigation.json` guarda si el menú lateral quedó contraído y qué grupos estaban
abiertos. Es una preferencia de esta máquina, fuera de la base del negocio:

```json
{ "collapsed": false, "expandedGroups": ["catalogs", "inventory"] }
```

Se puede borrar sin riesgo para restablecer el menú. Si está dañado, la aplicación lo ignora,
muestra el menú expandido y registra una advertencia. La contracción automática en ventanas de
menos de 1000 px de ancho no se guarda.

## Imágenes de productos

Las imágenes de productos se guardan **dentro de la base** (`data/pos.db`, tabla
`ProductImages`), no como archivos sueltos. Por eso:

- Se guardan en la misma transacción que el producto: nunca queda un producto con una imagen a
  medias.
- Los respaldos automáticos y los previos a migración las incluyen, y restaurar un respaldo
  devuelve a cada producto la imagen que tenía en ese momento.
- Al reemplazar o quitar una imagen se actualiza o borra su fila. SQLite reutiliza ese espacio,
  así que no se acumulan archivos huérfanos.

Cada imagen se guarda optimizada en WEBP, con un máximo de 800 px por lado (de 40 a 90 KB) más
una miniatura de 128 px (de 3 a 6 KB). Con unas 5,000 imágenes, la base y **cada** respaldo
automático crecen de 300 a 450 MB. Como se conservan 7 respaldos, prevé ese espacio en disco. El
respaldo diario al arrancar puede tardar unos segundos más en ese escenario.

## Respaldos automáticos

- Se crea uno al arrancar y otro al cerrar, pero solo si el último tiene más de 24 horas.
- El respaldo al cerrar tiene un límite de 15 segundos; si falla o se excede, se registra en el
  log y la aplicación cierra igual.
- Antes de aplicar una migración siempre se crea un respaldo en `backups/pre-migration/`. Si la
  migración falla, la aplicación lo restaura automáticamente y no continúa.
- Los nombres son `pos-AAAAMMDD-HHMMSSZ.db` (hora UTC). Se escriben primero como `.tmp` y se
  renombran al terminar, así que un archivo `.db` siempre es un respaldo completo.

## Base dañada

Si al arrancar la base está dañada, la aplicación ofrece restaurar el respaldo automático más
reciente e indica su fecha. Si el operador acepta, la base dañada se mueve a
`backups/corrupt/<fecha>Z/` para que soporte la analice y se restaura el respaldo. Si no acepta,
o si no hay respaldos, la aplicación no abre.

## Restaurar un respaldo a mano

1. Cierra la aplicación. Revisa que no quede abierta (no debe haber otra ventana del POS).
2. Mueve `data/pos.db`, `data/pos.db-wal` y `data/pos.db-shm` a otra carpeta. No los borres:
   pueden hacer falta para soporte.
3. Copia el respaldo elegido a `data/pos.db`.
4. Abre la aplicación. Si el respaldo es de una versión anterior, se migrará automáticamente
   (con su propio respaldo previo).

## Logs

Los logs usan el formato CLEF (JSON compacto, un evento por línea). Cada error incluye la
operación (`Operation`), el usuario (`UserId`) y los identificadores relevantes (`ProductId`,
`Sku`...), además de `AppVersion`, `MachineName` y `OperatingSystem`. No contienen datos
sensibles.

Para leerlos puedes usar cualquier visor de CLEF (por ejemplo Seq o `clef-tool`), o `jq`:

```bash
jq -c 'select(."@l" == "Error") | {t: ."@t", op: .Operation, msg: ."@mt", ex: ."@x"}' pos-20260929.log
```

## Exportar diagnóstico

En **Acerca de → Exportar diagnóstico…** se genera un único `.zip` con:

- `info.json`: versión, sistema operativo, carpeta de datos y fecha de exportación.
- `logs/`: los logs de los últimos 7 días.
- `pos.db`: un respaldo consistente de la base, **sin las imágenes de productos**: se borran de
  la copia y se compacta antes de comprimirla, porque pesan mucho y no aportan al diagnóstico.

El archivo se arma en una carpeta temporal y solo al final se mueve al destino, así que nunca
queda un zip incompleto.
