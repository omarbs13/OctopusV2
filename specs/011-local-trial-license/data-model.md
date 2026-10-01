# Modelo de datos: Licencia local

Sin cambios en la base de datos. Todo vive en el archivo `license.lic` y en memoria.

## LicenseRecord (contenido de `license.lic`, ya descifrado)

| Campo | Tipo | Regla |
|---|---|---|
| `Version` | entero | Versión del formato; hoy 1 |
| `MachineId` | texto (64 hex) | Debe coincidir con el de la máquina actual |
| `FirstRunUtc` | fecha UTC | Primer arranque; con `.lic` regenerado, la menor entre hoy y el primer usuario |
| `LastSeenUtc` | fecha UTC | Nunca retrocede; se actualiza al arrancar y al cambiar el día |
| `Grant` | `LicenseGrant?` | Concesión del proveedor vigente; nulo en evaluación |

## LicenseGrant (viene del archivo importable)

| Campo | Tipo | Regla |
|---|---|---|
| `MachineId` | texto | Debe coincidir con la máquina |
| `IssuedAtUtc` | fecha UTC | No puede ser anterior a la concesión vigente |
| `ValidUntil` | fecha? | Fecha de fin; con `DaysRemaining ≤ 0` ya está vencida; nula = sin vencimiento |
| `Signature` | bytes | ECDSA P-256 del proveedor sobre el contenido canónico |

## LicenseStatus (calculado, no se guarda)

| Campo | Descripción |
|---|---|
| `Kind` | `Trial`, `Licensed`, `Expired`, `Invalid` |
| `DaysRemaining` | entero ≥ 0; nulo si `Licensed` sin vencimiento o `Invalid` |
| `IsReadOnly` | verdadero en `Expired` e `Invalid` |
| `Warning` | `None`, `Near` (≤ 5 días), `Urgent` (≤ 1 día) |
| `InvalidReason` | `OtherMachine`, `Corrupt`, o nulo |

## Transiciones

```text
(sin .lic) ──primer arranque──► Trial(30)
Trial(n) ──pasan días──► Trial(n-1) … Trial(0)=Expired
Expired ──importar licencia válida──► Licensed(fin) o Licensed(sin vencimiento)
Licensed(fin) ──llega la fecha──► Expired
Invalid(OtherMachine|Corrupt) ──importar licencia válida──► Licensed
(.lic borrado) ──regenerar──► Trial con inicio = min(hoy, primer usuario)
```

## Cálculo

- `hoy` = fecha local de max(reloj, `LastSeenUtc`).
- Trial: `DaysRemaining = 30 − días de calendario entre FirstRun (local) y hoy`.
- Licensed con fin: `DaysRemaining = ValidUntil − hoy`.
- `DaysRemaining ≤ 0` ⇒ `Expired`.
