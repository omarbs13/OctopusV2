# Quickstart: validar el licenciamiento coordinado

**Feature**: 025-coordinated-licensing

Guía para comprobar de punta a punta los criterios de aceptación. Formatos y reglas:
[`contracts/license-format.md`](../../contracts/license-format.md),
[`contracts/license-request.md`](../../contracts/license-request.md),
[`contracts/blocked-mode.md`](contracts/blocked-mode.md). Modelo: [data-model.md](data-model.md).

## Requisitos

- .NET 10 SDK, `openssl` (para firmar licencias de prueba a mano).
- Compilación DEBUG (permite reemplazar la clave pública con `POS_LICENSE_DEV_PUBLIC_KEY`).

## 1. Pruebas automáticas

```bash
dotnet build -v q
dotnet test tests/Pos.Domain.Tests --verbosity quiet          # evaluador: vigencia, bloqueo, reloj atrasado, prueba
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet  # catálogo = JSON, verificador v3 + vector del contrato, almacén, migración
dotnet test tests/Pos.Application.Tests --verbosity quiet     # importación (orden y rechazos), bloqueo en casos de uso, venta en curso
dotnet test tests/Pos.ArchitectureTests --verbosity quiet
```

Resultado esperado: todo en verde, sin advertencias. Cubren los criterios 1, 2, 3, 4, 6 y 7 a
nivel de reglas.

## 2. Clave y licencia de prueba (manual)

```bash
openssl ecparam -name prime256v1 -genkey -noout -out dev.key
export POS_LICENSE_DEV_PUBLIC_KEY=$(openssl ec -in dev.key -pubout -outform DER 2>/dev/null | base64 -w0)
```

Para emitir una licencia: escribir el contenido JSON (§3 del contrato) con el ID de máquina que
muestra "Ayuda > Licencia", firmarlo y armar el sobre. La firma de `openssl` sale en DER y el
contrato exige P1363 (`r‖s`, 64 bytes): convertirla con
`openssl asn1parse -inform DER -in sig.der` (tomar los dos INTEGER, rellenar cada uno a 32 bytes)
o con el emisor de pruebas `TestLicenseIssuer` de `tests/Pos.Infrastructure.Tests`.

## 3. Escenarios manuales

Ejecutar `dotnet run --project src/Pos.Desktop` con la variable de entorno anterior.

| # | Pasos | Resultado esperado | Criterio |
|---|-------|--------------------|----------|
| 1 | Base nueva → "Ayuda > Licencia". | "En prueba: 30 días restantes"; 9 módulos activos. | H5 |
| 2 | "Generar solicitud" → abrir el `.octoreq`. | `machineId` = el mostrado en pantalla (y el copiado con "Copiar"); `catalogVersion` = 1. | 8 |
| 3 | Importar licencia con POS e Inventario. | "Licenciado"; menú solo con funciones base + Inventario; sin reiniciar. | 2 |
| 4 | Importar la misma licencia otra vez. | Aceptada (reimportación), nada cambia. | H4 |
| 5 | Importar: (a) un byte de `payload` cambiado; (b) otro `machineId`; (c) `issuedAtUtc` anterior con otro `licenseId`; (d) un `.lic` de formato 2. | Rechazos con su mensaje; la licencia anterior sigue. | 3 |
| 6 | Editar `InstalledLicenses.Content` con un editor de SQLite (agregar un módulo) y reiniciar. | Bloqueo por licencia no válida, con el mensaje "La licencia guardada no es válida…" y sin aviso duplicado; sin módulos; la prueba no vuelve. Lo mismo si se borra la fila. | 4 |
| 7 | Licencia sin POS (solo Inventario). | Bloqueo: solo Licencia, respaldo e inicio de sesión; invocar una venta nueva se rechaza. | 5 |
| 8 | Con una venta con artículos y un turno abierto, importar una licencia con POS vencido ayer (o adelantar el reloj). | Se puede terminar y cobrar la venta y cerrar el turno; no se puede iniciar otra venta ni abrir turno. | FR-030a |
| 9 | Licencia con Devoluciones `activatesOn` = mañana; adelantar el reloj del sistema a mañana con la app abierta (cruzar medianoche). | Devoluciones aparece en el menú sin reiniciar. | 6 |
| 10 | Licencia con Crédito `expiresOn` = hoy; pasar al día siguiente. | Crédito desaparece; sus clientes y saldos siguen en la base y vuelven al renovar. | 6 |
| 11 | Con un módulo vencido, atrasar el reloj 5 días y reiniciar. | El módulo sigue vencido; aviso de reloj atrasado. Con prueba vencida, la prueba no vuelve. | 7 |
| 12 | Licencia con un módulo que vence en 3 días. | Inicio muestra "{Módulo} vence el {fecha}". | H7 |
| 13 | Prueba vencida (fecha de inicio alterada en `LicenseSeals`). | Bloqueo por prueba vencida. | H5 |
| 14 | Bloqueo → "Exportar respaldo". | Se genera una copia completa de la base en el destino elegido. | 5 |

## 4. Interoperabilidad con OctopusAdmin

- Verificar que `contracts/module-catalog.json`, `contracts/license-format.md` y
  `contracts/license-request.md` son idénticos en ambos repositorios (`diff`).
- Una licencia emitida por OctopusAdmin (con su clave de producción y la clave pública ya puesta
  en `EcdsaLicenseVerifier.ProductionPublicKey`) pasa el escenario 3 en una compilación Release.
