# Contratos de diagnóstico

Aplicación de escritorio sin API externa; los contratos son los formatos que ve soporte y los límites entre capas.

## 1. Formato de línea del archivo de registro

```text
2026-09-30 14:32:05.123 -06:00 [FATAL] Error inesperado en la operación CobrarVenta {"Operation":"CobrarVenta","UserId":"…","Screen":"sales.pos","SaleLines":3,"SaleDraftId":"…"}
System.InvalidOperationException: …
   at …
```

- Una entrada por línea; la excepción continúa en las líneas siguientes.
- Niveles permitidos: `INFO`, `WARNING`, `ERROR`, `FATAL`.
- Entrada agrupada: `… [FATAL] Error repetido 7 veces en 5 s: <tipo> {"Repetitions":7,…}`.

## 2. Niveles

| Nivel | Cuándo | Ejemplos |
|---|---|---|
| INFO | Operación crítica de negocio exitosa | Venta registrada, turno abierto, usuario conectado |
| WARNING | Situación esperada pero anómala | Impresora desconectada, existencia negativa |
| ERROR | Falla controlada | Validación fallida, base inaccesible, exportación fallida |
| FATAL | Excepción no controlada | Cualquier excepción que llega a un manejador global |

## 3. Propiedades de contexto

| Propiedad | Siempre | Ejemplo |
|---|---|---|
| `UserId` | Sí (vacío o Sistema sin sesión) | GUID |
| `Screen` | Sí tras iniciar la navegación | `sales.pos` |
| `Operation` | Cuando la hay | `CobrarVenta` |
| `SaleLines` y `SaleDraftId` | Con venta sin guardar | `3` |
| `ProductId`, `SaleId`, `ShiftId` | Cuando aplican (en el cobro, `DraftId`) | GUID |

Se sustituye por `***` el valor de toda propiedad cuyo nombre contenga: password, contraseña, pin, card, tarjeta, token, secret o cvv.

## 4. Mensaje al operador

Texto único, en `Strings.resx`: **"Ocurrió un error inesperado. Los detalles se registraron para soporte técnico."** Sin tipo de excepción, traza ni rutas. Un solo mensaje por episodio.

## 5. Exportación de diagnóstico

- Interfaz de aplicación: `IDiagnosticsExporter.ExportAsync(destinationFile, includeDatabase, cancellationToken)`, que devuelve la cantidad de archivos de log incluidos (cero = sin registros).
- Comando: `ExportDiagnosticsCommand(DestinationFilePath, IncludeDatabase = false)`.
- Salida: `.zip` con `info.json`, `logs/*.log` (30 días) y, si se pidió, `pos.db`.
- Nunca deja un archivo incompleto en el destino; falla controlada → ERROR en el log y mensaje comprensible.
