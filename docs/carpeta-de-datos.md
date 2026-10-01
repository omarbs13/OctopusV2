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
├── license.lic                  Licencia local (cifrada): ID de máquina, primer arranque y licencia vigente
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
    └── pos-AAAAMMDD.log         Log de texto (un archivo por día, se conservan 30 días)
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

Los logs son archivos de texto legibles, uno por día (`pos-AAAAMMDD.log`). Al iniciar la aplicación y
cada vez que cambia el día se eliminan los de más de **30 días**, según la fecha del nombre.
Cada línea tiene este formato:

```text
2026-09-30 14:32:05.123 -06:00 [FATAL] Error inesperado en la operación CobrarVenta {"Operation":"CobrarVenta","UserId":"…","Screen":"sales.pos","SaleLines":3,…}
System.InvalidOperationException: …
   at …
```

| Nivel | Cuándo |
|---|---|
| `INFO` | Operaciones críticas: venta registrada, turno abierto, usuario conectado |
| `WARNING` | Situaciones esperadas pero anómalas: impresora que no responde, existencia insuficiente |
| `ERROR` | Fallas controladas: validación fallida, exportación fallida, base inaccesible |
| `FATAL` | Excepciones no controladas (siempre incluyen tipo y traza de pila) |

Cada entrada incluye el contexto para reproducir el problema: `UserId` y `UserName` del usuario
conectado, `Screen` (pantalla actual), `SaleLines` y `SaleDraftId` si hay una venta sin guardar, y los
identificadores de la operación (`ProductId`, `SaleId`, `ShiftId`...), además de `AppVersion`,
`MachineName` y `OperatingSystem`. Los valores de propiedades sensibles (contraseñas, tarjetas, tokens)
se reemplazan por `***`.

Si el mismo error se repite varias veces en 5 segundos, solo se escribe completo el primero y después
una entrada resumen: `Error repetido N veces en 5 s: <tipo>`. El operador ve un único mensaje por vez:
"Ocurrió un error inesperado. Los detalles se registraron para soporte técnico."

Para buscar errores: `grep -E "\[(ERROR|FATAL)\]" pos-20260930.log`.

## Exportar diagnóstico

En **Acerca de → Exportar diagnóstico…** se genera un único `.zip` con:

- `info.json`: versión, sistema operativo, carpeta de datos y fecha de exportación.
- `logs/`: los logs de los últimos 30 días. Si no hay ninguno, la pantalla lo avisa.
- `pos.db` (**opcional**, casilla "Incluir respaldo de la base de datos", desactivada por omisión):
  un respaldo consistente de la base, **sin las imágenes de productos**: se borran de la copia y se
  compacta antes de comprimirla, porque pesan mucho y no aportan al diagnóstico.

Solo el Administrador puede exportar el diagnóstico.

El archivo se arma en una carpeta temporal y solo al final se mueve al destino, así que nunca
queda un zip incompleto.

## Licencia local (`license.lic`)

El sistema se puede evaluar 30 días en una máquina; después pasa a **modo lectura**: el Punto de
venta, la apertura de turnos, los reportes y la administración de usuarios se bloquean, y Productos,
Inventario y Ventas registradas siguen disponibles. Un turno ya abierto se puede cerrar.

- `license.lic` está cifrado y ligado al ID de la máquina. **No lo edites, no lo copies a otra
  máquina y no lo envíes a soporte**: no se incluye en el diagnóstico y copiarlo a otro equipo lo
  invalida (el sistema queda en modo lectura).
- Si se borra, se regenera con el mismo ID de máquina. El inicio del período se toma de la fecha del
  primer usuario creado, así que borrar el archivo no reinicia los 30 días.
- Para activar o extender: **Acerca de → Administración de licencia** (solo Administrador).
  1. **Exportar solicitud…** genera un archivo `.posreq` con el ID de máquina; se envía al proveedor.
  2. El proveedor devuelve un archivo `.poslic` firmado.
  3. **Importar licencia…** lo aplica de inmediato, sin reiniciar. Se rechaza si pertenece a otra
     máquina, si está alterado o si es anterior a la licencia vigente.
- Si el reloj del sistema se retrasa, los días restantes no aumentan.
- Importar una licencia queda registrado en la bitácora de auditoría.
