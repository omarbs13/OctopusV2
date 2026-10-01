# Contratos: Licencia modular por módulos

La aplicación no expone API externa; los contratos son (1) el formato de licencia extendida que consume, (2) el formato del archivo local, (3) los puertos de Application y (4) el contrato de interfaz.

## 1. Licencia extendida (`.poslic`, formato 2) — emitida por el proveedor

```json
{
  "format": 2,
  "machineId": "<ID de máquina destino>",
  "issuedUtc": "2026-10-05T14:30:00Z",
  "modules": ["<guid>", "<guid>"],
  "signature": "<Base64 ECDSA P-256 / SHA-256>"
}
```

- **Contenido firmado (canónico)**: UTF-8, sin espacios, campos en este orden:
  `{"format":2,"machineId":"…","issuedUtc":"yyyy-MM-ddTHH:mm:ssZ","modules":["…","…"]}`
  con los GUID en minúscula formato `D` y ordenados ascendentemente. El verificador reordena y normaliza antes de verificar.
- Tamaño máximo del archivo: 16 KB. La aplicación solo conoce la clave **pública**.
- Formato `1` (con `validUntil`) → rechazado como no legible.
- GUID desconocidos se ignoran; si ninguno es conocido el archivo es válido pero no activa nada.
- Los valores GUID por módulo los define `ModuleCatalog` (Domain); el proveedor los recibe una vez, fuera del repositorio de la app. El emisor es herramienta externa (fuera de alcance).

**Resultados de verificación** (`LicenseImportRejection`): `Unreadable`, `BadSignature`, `OtherMachine`. El valor `Older` de 011 se elimina.

## 2. Archivo local `license.lic` (versión 2)

| Campo | Tamaño | Contenido |
|---|---|---|
| Marca | 4 | `POSL` |
| Versión | 1 | `2` (la `1` solo se lee para migrar) |
| Huella de máquina | 8 | SHA-256(`"fp:" + machineId`) truncado |
| Nonce | 12 | Aleatorio por escritura |
| Etiqueta | 16 | AES-GCM; el encabezado va como datos asociados |
| Cifrado | n | JSON `{machineId, firstRunUtc, lastSeenUtc, trialDays, modules[]}` |

Clave: HKDF-SHA256 con el ID de máquina y un secreto de aplicación. Escritura atómica (temporal + reemplazo). Cualquier edición, truncado o copia a otra máquina falla al descifrar o validar → "inutilizable" (§3 de research).

**Resultado de lectura** (`LicenseLoadResult`): `Loaded(record)`, `Missing`, `Unusable` (reemplaza a `Invalid`; cubre corrupto y otra máquina) y `LegacyV1(record)` (solo para la migración 011).

## 3. Puertos (Application)

```text
ILicenseStore           Load() → LicenseLoadResult;  Save(LicenseRecord)
ILicenseSealStore       Task<LicenseSeal?> ReadAsync(ct);  Task WriteAsync(LicenseSeal, ct)
                        LicenseSeal(FirstRunUtc, LastSeenUtc);  carga alterada/ilegible → null
ILicenseVerifier        Verify(path, machineId) → Valid(ExtendedGrant(modules, issuedUtc)) | Rejected(reason)
ILicenseState           Current; Record; IsModuleActive(LicensedModule); EnabledModules;
                        Set(record); event Changed
IInstallationAgeReader  sin cambios
IMachineIdProvider      sin cambios
```

### Casos de uso

| Caso de uso | Entrada | Salida | Reglas |
|---|---|---|---|
| `GetLicenseStatusHandler` | — | `LicenseStatusDto(Phase, DaysRemaining, Warning, ActiveModules, ContactPhone, ContactEmail)` | Sin permiso (lo consulta Inicio); sin acceso a disco |
| `ImportLicenseHandler` | `ImportLicenseCommand(FilePath)` | `Result<LicenseStatusDto>` | Exige `ManageLicense`; rechaza con `InvalidLicense(reason)`; suma módulos; no modifica `FirstRunUtc`; guarda archivo y copia protegida; dispara `Changed`; audita `LicenseImported` con el número de módulos |
| `ExportLicenseRequestHandler` | — | — | Sin cambios |

### Errores (`Error`)

| Error | Cuándo | Mensaje al operador |
|---|---|---|
| `ModuleNotLicensed(LicensedModule)` | Permiso de un módulo inactivo (nuevo; reemplaza a `LicenseExpired`) | "Este módulo no está activo en tu licencia." |
| `InvalidLicense(reason)` | Importación rechazada | Mensajes de 011 por motivo (otra máquina, no válido) |

### Bitácora (`AuditActions`)

`LicenseImported` (existente, el detalle pasa a "N módulos activados"). Se agrega `LicenseRecovered` cuando el archivo se regenera desde la copia protegida (detalle: motivo, sin contenido).

## 4. Contrato de interfaz

- **Menú**: no muestra entradas cuyo permiso pertenece a un módulo inactivo; un grupo sin entradas visibles no se muestra; se reconstruye con `ILicenseState.Changed`.
- **Acceso directo / atajo**: rechazado con el mensaje estándar; no se modifica nada.
- **Inicio**: tarjeta de evaluación; aviso "Te quedan 5 días de acceso a todos los módulos." con 5 días y "Mañana vence el período de evaluación." con 1 día; nada más. Tarjetas de Reportes/Inventario se ocultan sin error si su módulo está inactivo.
- **Acerca de > Administración de licencia** (solo `ManageLicense`): estado (evaluación con días o modo modular), nombres de los módulos activos, importar, exportar solicitud. No muestra GUID.
- **Mensaje de archivo regenerado**: indica que el archivo de licencia no era válido o faltaba, que la evaluación continúa y que las compras se reactivan reimportando la licencia extendida; incluye el contacto del proveedor.
