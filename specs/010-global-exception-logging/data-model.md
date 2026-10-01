# Modelo de datos: Registro global de excepciones

No hay cambios en la base de datos ni migraciones. Las entidades viven en archivos y en memoria.

## Entrada de registro (línea de texto en el archivo diario)

| Campo | Descripción | Regla |
|---|---|---|
| Fecha y hora | `yyyy-MM-dd HH:mm:ss.fff` con zona horaria | Siempre presente |
| Nivel | INFO, WARNING, ERROR, FATAL | Según contrato de niveles |
| Mensaje | Texto en español | Sin datos sensibles |
| Propiedades | Pares clave=valor en JSON | Redactadas si son sensibles |
| Excepción | Tipo, mensaje y traza de pila | Solo ERROR/FATAL con excepción |
| Repeticiones | Conteo de repeticiones agrupadas | Solo en la entrada resumen de un episodio |

## Contexto de diagnóstico (memoria, instantánea inmutable)

| Campo | Origen | Notas |
|---|---|---|
| `UserId` y nombre | Sesión actual | Vacío si no hay sesión |
| `Screen` | Pantalla actual de la navegación | Id de la opción, por ejemplo `sales.pos` |
| `Operation` | Operación en curso (si la hay) | La asigna `OperationRunner` |
| `SaleFolio` / `SaleLines` | Pantalla de venta | Solo si hay venta sin guardar |
| Identificadores | Contexto de la operación | `ProductId`, `SaleId`, `ShiftId` (GUID) |

## Archivo diario de registro

- Nombre: `pos-YYYYMMDD.log` en `IAppPaths.LogsDirectory`.
- Ciclo de vida: se crea al primer evento del día; se elimina cuando su fecha tiene más de 30 días.

## Episodio de error (memoria)

| Campo | Descripción |
|---|---|
| Clave | Tipo de excepción + primera línea de la traza |
| Primera vez | Hora de la primera aparición |
| Repeticiones | Contador dentro de la ventana de 5 s |
| Mensaje mostrado | Si el operador ya vio el mensaje del episodio |

## Paquete de diagnóstico (archivo .zip)

- `info.json`: versión, sistema operativo, carpeta de datos y hora de exportación.
- `logs/`: todos los `*.log` de los últimos 30 días.
- `pos.db`: copia consistente de la base, solo si el operador lo eligió.
