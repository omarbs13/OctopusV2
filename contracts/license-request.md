# Contrato: solicitud de licencia (`.octoreq`)

**Contrato compartido entre OctopusAdmin y el POS (OctopusV2).** Este archivo debe ser idéntico en
ambos repositorios.

- **Versión del documento**: 1 (2026-10-03)

## Propósito

El cliente genera la solicitud en el POS ("Ayuda > Licencia > Generar solicitud") y la envía al
proveedor por cualquier medio. OctopusAdmin la lee para emitir una licencia (`contracts/license-format.md`)
para esa máquina. La solicitud **no es secreta ni va firmada**: solo transporta datos para evitar
errores al copiarlos a mano.

## Archivo

Texto UTF-8 (sin BOM) con un objeto JSON, extensión `.octoreq`. Tamaño máximo: 16 KiB.

```json
{
  "requestFormat": 2,
  "machineId": "3f5a0c1e9b7d2468ace013579bdf2468ace013579bdf2468ace013579bdf2468",
  "businessName": "Abarrotes La Esperanza",
  "appVersion": "1.4.0",
  "catalogVersion": 1,
  "createdAtUtc": "2026-10-03T15:04:05Z"
}
```

| Campo | Tipo | Regla |
|-------|------|-------|
| `requestFormat` | entero | `2`. (La versión `1` de la especificación 011 solo tenía `machineId`, `appVersion` y `createdUtc`; OctopusAdmin puede aceptarla.) |
| `machineId` | texto | 64 caracteres hexadecimales en minúscula. Es el valor que debe llevar `machineId` en la licencia. |
| `businessName` | texto | Nombre del negocio configurado en el POS; texto vacío si no hay. Solo informativo. |
| `appVersion` | texto | Versión de la aplicación POS. |
| `catalogVersion` | entero | `catalogVersion` del `module-catalog.json` incluido en el POS. |
| `createdAtUtc` | texto | Fecha y hora UTC de generación, formato `yyyy-MM-ddTHH:mm:ssZ`. |

Los campos adicionales se ignoran.

## ID de máquina

El POS calcula el ID una sola vez por arranque: SHA-256 (hexadecimal en minúscula) de la sal
`Pos.MachineId.v1:` seguida del identificador del sistema operativo (Linux `/etc/machine-id`,
Windows `MachineGuid`) o, como reserva, de la MAC de la primera interfaz física. Es el mismo valor
que se muestra en "Ayuda > Licencia" con el botón "Copiar". OctopusAdmin lo trata como texto
opaco: no lo recalcula.
