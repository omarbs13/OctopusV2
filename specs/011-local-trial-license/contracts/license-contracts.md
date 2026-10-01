# Contratos: licencia local

Contratos de interfaz con el proveedor (archivos) y con el resto de la aplicación (casos de uso y errores).

## 1. Archivo de solicitud (`*.posreq`, texto JSON)

Lo exporta un administrador desde Acerca de → Administración de licencia.

```json
{ "format": 1, "machineId": "<64 hex>", "appVersion": "1.4.0", "createdUtc": "2026-09-30T15:00:00Z" }
```

No incluye datos del negocio ni de usuarios.

## 2. Archivo de licencia importable (`*.poslic`, texto JSON)

Lo emite y firma el proveedor.

```json
{
  "format": 1,
  "machineId": "<64 hex>",
  "issuedUtc": "2026-09-30T15:00:00Z",
  "validUntil": "2027-09-30",
  "signature": "<base64 ECDSA P-256 sobre el JSON canónico sin 'signature'>"
}
```

- `validUntil` es una fecha o `null` (sin vencimiento).
- Contenido canónico: los campos `format`, `machineId`, `issuedUtc`, `validUntil` en ese orden, UTF-8, sin espacios.
- La app solo incluye la clave pública del proveedor.

## 3. Casos de uso (Application)

| Caso de uso | Permiso | Resultado |
|---|---|---|
| `GetLicenseStatusHandler` | ninguno (sin sesión también: lo usa el login) | `LicenseStatusDto` (`Kind`, `DaysRemaining`, `IsReadOnly`, `Warning`, `ContactPhone`, `ContactEmail`, `InvalidReason`) |
| `ImportLicenseHandler(path)` | `ManageLicense` | éxito con el nuevo `LicenseStatusDto`, o `InvalidLicense(reason)` |
| `ExportLicenseRequestHandler(path)` | `ManageLicense` | éxito con la ruta, o `ExportFailed` |

`InvalidLicense.Reason`: `Unreadable`, `BadSignature`, `OtherMachine`, `Older`.

## 4. Errores nuevos (`Error.cs`)

- `LicenseExpired(string ContactPhone, string ContactEmail)`: devuelto por `AccessControl` y `OpenShiftHandler`. La UI muestra "Período de evaluación vencido. Contacte a {teléfono / email}."
- `InvalidLicense(InvalidLicenseReason Reason)`: la importación se rechazó; la licencia vigente no cambió.

`LicenseExpired` se considera un flujo esperado en `UseCases.IsExpected` (no se registra como ERROR; se registra como WARNING en el punto del bloqueo).

## 5. Permisos

- Nuevo `Permission.ManageLicense`: solo Administrador; no autorizable por concesión.
- Bloqueados en modo lectura: `Sell`, `ViewReports`, `ManageUsers` y la apertura de turno.
- Siempre permitidos: `ViewProducts`, `ViewInventory`, `ViewOwnSales`, `ViewAllSales`, `OperateShift` (excepto abrir turno), `ManageLicense`, `ExportDiagnostics`.

## 6. Textos (Strings.resx, español)

- Tarjeta: "Período de evaluación", "{n} días restantes", contacto.
- Vencido: "Período de evaluación vencido. Contacte a {contacto}."
- Inicio en modo lectura: "Sistema en modo lectura. Contacte para activación."
- Aviso a 5 días (Inicio) y a 1 día (login, en rojo).
- Rechazos de importación: "La licencia pertenece a otra máquina.", "El archivo de licencia no es válido.", "La licencia es anterior a la vigente.".
