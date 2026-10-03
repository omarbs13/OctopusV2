# Contrato: formato de licencia, versión 3

**Contrato compartido entre OctopusAdmin y el POS (OctopusV2).** Este archivo debe ser idéntico en
ambos repositorios. Un cambio aquí es un cambio de contrato: se acuerda, se aplica en los dos
repositorios a la vez y, si no es compatible, sube la versión de formato.

- **Versión del documento**: 2 (2026-10-03). Cambio respecto a la 1: en §5, `format` se comprueba
  antes de exigir `payload` y `signature`, para que un archivo de formato 2 se rechace como "formato
  no compatible" y no como "ilegible". No cambia lo que firma OctopusAdmin.
- **Catálogo de módulos**: `contracts/module-catalog.json`
- **Solicitud de licencia**: `contracts/license-request.md`

## 1. Principio

OctopusAdmin firma la licencia con su clave privada ECDSA P-256, que solo existe en la máquina
del proveedor. El POS solo tiene la clave pública. **Lo que se firma son los bytes exactos del
contenido**: el contenido viaja codificado en Base64 dentro del archivo, así que el POS verifica
la firma sobre los bytes decodificados, sin volver a serializar nada. Ambas aplicaciones no
necesitan acordar una canonicalización de JSON.

## 2. Archivo `.lic` (sobre)

Archivo de texto UTF-8 (sin BOM) con un objeto JSON. Tamaño máximo: 64 KiB.

```json
{
  "format": 3,
  "payload": "<Base64 del contenido>",
  "signature": "<Base64 de la firma>"
}
```

| Campo | Tipo | Regla |
|-------|------|-------|
| `format` | entero | Exactamente `3`. |
| `payload` | texto | Base64 estándar (RFC 4648 §4, alfabeto `+/`, con relleno `=`, sin saltos de línea) de los bytes del contenido (§3). |
| `signature` | texto | Base64 estándar de la firma ECDSA P-256 con SHA-256 sobre los bytes del contenido, en formato **IEEE P1363**: `r ‖ s`, 32 bytes cada uno, 64 bytes en total. No se acepta DER. |

Los campos adicionales del sobre se ignoran. Los nombres de campo distinguen mayúsculas.

### Bytes firmados

```
bytesFirmados = Base64Decode(payload)
firma         = ECDSA_P256_SHA256_Sign(clavePrivada, bytesFirmados)   // formato P1363
```

En .NET: `ECDsa.SignData(bytes, HashAlgorithmName.SHA256)` y `ECDsa.VerifyData(bytes, firma,
HashAlgorithmName.SHA256)` usan P1363 por omisión.

## 3. Contenido (`payload` decodificado)

JSON UTF-8 sin BOM. OctopusAdmin puede serializarlo con cualquier espaciado y orden de campos:
el POS nunca lo reconstruye, solo lo lee después de verificar la firma.

```json
{
  "formatVersion": 3,
  "licenseId": "0192f3a4-5b6c-7d8e-9f01-23456789abcd",
  "issuedAtUtc": "2026-10-03T15:04:05Z",
  "machineId": "3f5a0c1e9b7d2468ace013579bdf2468ace013579bdf2468ace013579bdf2468",
  "customerName": "Abarrotes La Esperanza",
  "modules": [
    { "id": "4c2517f5-096a-460e-8ee7-b09e46572952", "activatesOn": "2026-10-01", "expiresOn": null },
    { "id": "7a99f06e-6c58-43fb-bc34-20bec30f8060", "activatesOn": "2026-10-01", "expiresOn": "2027-09-30" }
  ]
}
```

| Campo | Tipo | Regla |
|-------|------|-------|
| `formatVersion` | entero | Exactamente `3`; debe coincidir con `format` del sobre. |
| `licenseId` | texto | GUID en formato `D` (36 caracteres con guiones). Único por licencia emitida. Se compara sin distinguir mayúsculas. |
| `issuedAtUtc` | texto | Fecha y hora de emisión en UTC, formato exacto `yyyy-MM-ddTHH:mm:ssZ` (sin fracciones de segundo, con `Z`). |
| `machineId` | texto | ID de máquina de la solicitud (`contracts/license-request.md`): 64 caracteres hexadecimales en minúscula. Se compara exacto (ordinal). |
| `customerName` | texto | Nombre del cliente, 1 a 200 caracteres. Solo se muestra. |
| `modules` | arreglo | Puede estar vacío. Cada elemento es una entrada de módulo. |
| `modules[].id` | texto | GUID del catálogo (`module-catalog.json`). Un GUID desconocido se ignora sin error. |
| `modules[].activatesOn` | texto | Fecha de activación, formato exacto `yyyy-MM-dd`. |
| `modules[].expiresOn` | texto o `null` | Fecha de vencimiento `yyyy-MM-dd`, o `null` / ausente = indefinido. |

Los campos adicionales del contenido se ignoran (permite agregar datos informativos sin subir la
versión). Un campo obligatorio ausente o con formato inválido hace ilegible la licencia, aunque la
firma sea válida.

## 4. Reglas de interpretación

1. **Fotografía completa**: cada licencia contiene todos los módulos vigentes del cliente.
   Importarla reemplaza a la anterior; nunca se suma.
2. **Fechas de módulo**: solo día, sin hora, interpretadas en la **fecha local del POS**.
3. **Vigencia inclusiva**: un módulo está activo en la fecha local `d` si
   `activatesOn ≤ d` y (`expiresOn` es nulo o `d ≤ expiresOn`). Si `expiresOn < activatesOn`, el
   módulo nunca está activo.
4. **Entradas repetidas**: si un GUID aparece varias veces, el módulo está activo si alguna de sus
   entradas lo está.
5. **Módulo base**: el módulo con `isBase: true` en el catálogo (POS). Si no está activo, el POS se
   bloquea completo, aunque otros módulos estén activos.
6. **Antigüedad**: el POS acepta una licencia nueva solo si tiene el mismo `licenseId` que la
   vigente (reimportación) o un `issuedAtUtc` estrictamente posterior al de la vigente. Por eso
   OctopusAdmin debe emitir cada licencia nueva con un `licenseId` nuevo y la hora actual.

## 5. Orden de verificación en el POS

| Paso | Comprobación | Si falla |
|------|--------------|----------|
| 1 | El archivo existe, mide ≤ 64 KiB y es un objeto JSON con el campo `format` numérico. | Ilegible |
| 2 | `format` es 3. Se comprueba antes de exigir los demás campos, porque los archivos de formato 2 no tienen `payload`. | Formato no compatible (incluye el formato 2) |
| 3 | `payload` y `signature` existen, son Base64 válidos y la firma mide 64 bytes. | Ilegible |
| 4 | La firma es válida con la clave pública sobre `Base64Decode(payload)`. | No auténtica |
| 5 | El contenido es JSON válido con todos los campos obligatorios y `formatVersion` = 3. | Ilegible |
| 6 | `machineId` es igual al ID de esta máquina. | De otro equipo |
| 7 | Mismo `licenseId` que la vigente, o `issuedAtUtc` posterior al de la vigente. | No es más reciente |

El primer fallo detiene la verificación y la licencia vigente se conserva. En cada arranque el POS
repite los pasos 1 a 6 sobre la licencia guardada (el paso 7 no aplica a sí misma).

## 6. Claves

- Curva: NIST P-256 (`secp256r1` / `prime256v1`). Hash: SHA-256.
- El POS incluye la clave pública como `SubjectPublicKeyInfo` DER en Base64. La clave pública no es
  secreta y puede estar en el repositorio. La clave privada nunca entra en ningún repositorio.

## 7. Vector de prueba

Firmado con una clave **solo de prueba** (no es la clave de producción; su clave privada se
descartó). Sirve para comprobar que ambas implementaciones leen y verifican lo mismo.

Clave pública de prueba (`SubjectPublicKeyInfo`, Base64):

```
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEqf8wn3coSlDN1BsJiKsesTuks+oZnez7v4g/RC1VNuQIeOq0tZJrIWLSqTqOw4clijOE6LG0hfyUOGidtChwqA==
```

Licencia:

```json
{
  "format": 3,
  "payload": "eyJmb3JtYXRWZXJzaW9uIjozLCJsaWNlbnNlSWQiOiIwMTkyZjNhNC01YjZjLTdkOGUtOWYwMS0yMzQ1Njc4OWFiY2QiLCJpc3N1ZWRBdFV0YyI6IjIwMjYtMTAtMDNUMTU6MDQ6MDVaIiwibWFjaGluZUlkIjoiM2Y1YTBjMWU5YjdkMjQ2OGFjZTAxMzU3OWJkZjI0NjhhY2UwMTM1NzliZGYyNDY4YWNlMDEzNTc5YmRmMjQ2OCIsImN1c3RvbWVyTmFtZSI6IkFiYXJyb3RlcyBMYSBFc3BlcmFuemEiLCJtb2R1bGVzIjpbeyJpZCI6IjRjMjUxN2Y1LTA5NmEtNDYwZS04ZWU3LWIwOWU0NjU3Mjk1MiIsImFjdGl2YXRlc09uIjoiMjAyNi0xMC0wMSIsImV4cGlyZXNPbiI6bnVsbH0seyJpZCI6IjdhOTlmMDZlLTZjNTgtNDNmYi1iYzM0LTIwYmVjMzBmODA2MCIsImFjdGl2YXRlc09uIjoiMjAyNi0xMC0wMSIsImV4cGlyZXNPbiI6IjIwMjctMDktMzAifV19",
  "signature": "NquLEWbwhaKY7PNshkkMqLRCl7euu5aIyYv0FlwzgW/i3NvdHm71uXszHs/LhcT2QzFQTyVsd0+nZTpdOZ+dwA=="
}
```

Resultado esperado con la clave de prueba y el ID de máquina
`3f5a0c1e9b7d2468ace013579bdf2468ace013579bdf2468ace013579bdf2468`: firma válida; módulos POS
(indefinido) e Inventario (hasta el 2027-09-30). Cambiar un solo byte de `payload` hace que la
firma falle.

## 8. Historial

| Versión de formato | Estado |
|--------------------|--------|
| 2 | Retirado. El POS lo rechaza como "formato no compatible". |
| 3 | Vigente. |
