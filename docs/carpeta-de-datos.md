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
- `pos.db`: un respaldo consistente de la base.

El archivo se arma en una carpeta temporal y solo al final se mueve al destino, así que nunca
queda un zip incompleto.
