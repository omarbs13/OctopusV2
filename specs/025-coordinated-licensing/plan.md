# Implementation Plan: Licenciamiento coordinado con OctopusAdmin

**Branch**: `025-coordinated-licensing` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/025-coordinated-licensing/spec.md`

## Summary

El POS pasa de "licencia que suma módulos a un archivo cifrado" (012, formato 2) a "licencia
firmada por OctopusAdmin que se guarda tal cual y se reverifica en cada arranque" (formato 3),
con 9 módulos de un catálogo compartido, vigencia por módulo (activación y vencimiento
inclusivos, en fecha local) y bloqueo total si el módulo base POS no está activo.

Enfoque técnico (detalle en [research.md](research.md)):

- **Contratos compartidos** en `contracts/` (raíz): catálogo JSON, formato de licencia v3 y
  solicitud `.octoreq`. El catálogo se embebe en `Pos.Infrastructure` y una prueba lo compara con
  las constantes de `ModuleCatalog`.
- **Firma sin canonicalización**: el `.lic` es un sobre con el contenido en Base64; se firman los
  bytes decodificados (ECDSA P-256/SHA-256, P1363).
- **Licencia guardada** en la tabla técnica `InstalledLicenses` (texto exacto), reverificada al
  arrancar. La prueba de 30 días conserva el mecanismo de 012 sin módulos.
- **Evaluador puro** en Domain: estado general (Prueba / Licenciado / Bloqueado + causa), estado de
  los 9 módulos, avisos, y la intersección por reloj atrasado.
- **Bloqueo en Application**: `AccessControl` rechaza todo permiso no exento; la venta en curso
  (borrador durable) y el cierre del turno abierto siguen disponibles (constitución v1.3.0).
- **UI**: nueva página "Ayuda > Licencia"; Inicio con avisos de módulos por vencer; menú
  reconstruido al cambiar el estado (incluido el cambio de día).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS), nullable habilitado, `TreatWarningsAsErrors`.

**Primary Dependencies**: Avalonia + CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting, EF Core
(SQLite), FluentValidation, Serilog; criptografía y JSON de la BCL. Sin dependencias nuevas.

**Storage**: SQLite local (tabla nueva `InstalledLicenses`; `LicenseSeals` existente) + archivo
`license.lic` (registro de la prueba, versión 3 sin módulos).

**Testing**: xUnit; SQLite real para persistencia; NetArchTest.

**Target Platform**: Escritorio Windows y Linux.

**Project Type**: Aplicación de escritorio en capas (`Pos.Domain`, `Pos.Application`,
`Pos.Infrastructure`, `Pos.Desktop`).

**Performance Goals**: Verificar una licencia y evaluar el estado en < 50 ms (una firma ECDSA y
cálculo en memoria); `ILicenseState.Current` se lee en cada verificación de permiso, así que se
cachea por fecha local (se recalcula solo si cambió el día o el registro).

**Constraints**: Totalmente sin conexión (FR-042); solo la clave pública en el repositorio
(FR-041); el control de licencia nunca cierra la aplicación (Principio I).

**Scale/Scope**: 1 licencia por instalación, 9 módulos, ~25 archivos de producción tocados, 1
migración, 1 página nueva.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación | Estado |
|-----------|------------|--------|
| I. La venta nunca se detiene (v1.3.0) | El bloqueo es por licencia, no por fallo. Garantías: venta en curso y cierre de turno (research §10, FR-030a); inicio de sesión, licencia y respaldo siempre disponibles (FR-028). Fallas al cargar o verificar nunca cierran la app (se mantiene el `try/catch` del bootstrapper). | ✅ |
| II. Capas | Evaluador, catálogo y reglas en Domain; puertos (`IInstalledLicenseStore`, `IModuleCatalogInfo`, `ILicenseVerifier`) en Application; JSON embebido, ECDSA y EF en Infrastructure. Domain no lee JSON. | ✅ |
| III. Lógica en el núcleo | Vigencia, antigüedad, bloqueo y reloj atrasado en Domain; los casos de uso aplican el bloqueo (no solo el menú). ViewModels solo presentan `LicenseStatusDto`. | ✅ |
| IV. Integridad de datos | Migración EF Core versionada para `InstalledLicenses`; fechas en UTC (las fechas de módulo son `DateOnly` de calendario local por contrato, no instantes). `InstalledLicenses` es dato técnico como `LicenseSeals`: sin auditoría/borrado lógico/versión (ver Complexity Tracking). Datos de módulos inactivos nunca se borran. Base de ejemplo: la prueba de migración debe pasar con la nueva migración. | ✅ (justificado) |
| V. Multiplataforma | Sin APIs de plataforma nuevas; `MachineIdProvider` ya resuelve Windows/Linux. | ✅ |
| VI. Calidad verificable | Pruebas solo de reglas: evaluador (vigencia inclusiva, base, prueba, reloj atrasado), antigüedad, verificador v3 (+ vector del contrato), consistencia catálogo↔JSON, bloqueo en `AccessControl` y venta en curso, persistencia de `InstalledLicenses` en SQLite real, migración. Sin pruebas de UI, ViewModels ni mapeos de DTO (T086 elimina las pruebas de mapeo de `GetLicenseStatus`). Durante la implementación se ejecutan solo las pruebas de los proyectos modificados. Casos adicionales en T034, T066 y T067: ver Complexity Tracking. | ✅ (justificado) |
| VII. Simplicidad | Sin dependencias nuevas; se elimina código (`LicenseCanonical`, migración 011, suma de módulos). Sin generador de código para el catálogo (una prueba basta). | ✅ |
| VIII. Soporte | Logs estructurados de importación, rechazo y reverificación fallida; ID de máquina visible y copiable; exportación de respaldo explícita. | ✅ |
| IX. Seguridad local | Solo clave pública en el repo. La constante de las claves AES locales (prueba) no es un secreto real; se renombra `KeyContext` y se documenta como detección de ediciones casuales (research §4). Importar licencia y licencia guardada rechazada quedan en la bitácora. | ✅ (justificado) |

**Re-check post-diseño (Fase 1)**: sin cambios; ninguna violación sin justificar.

## Project Structure

### Documentation (this feature)

```text
specs/025-coordinated-licensing/
├── plan.md              # Este archivo
├── research.md          # Fase 0
├── data-model.md        # Fase 1
├── quickstart.md        # Fase 1
├── contracts/
│   ├── README.md        # Índice (apunta a los contratos compartidos de la raíz)
│   └── blocked-mode.md  # Contrato interno: modo bloqueado y pantalla de licencia
└── tasks.md             # Fase 2 (/speckit-tasks)

contracts/               # Raíz: contratos compartidos con OctopusAdmin (idénticos en ambos repos)
├── module-catalog.json
├── license-format.md
└── license-request.md
```

### Source Code (repository root)

```text
src/Pos.Domain/
├── Licensing/
│   ├── LicensedModule.cs          # + Pos, Suppliers, Categories
│   ├── ModuleCatalog.cs           # 9 módulos, key, orden, base
│   ├── ModuleGrant.cs             # nuevo
│   ├── SignedLicense.cs           # nuevo (regla de antigüedad)
│   ├── TrialRecord.cs             # reemplaza LicenseRecord
│   ├── LicenseStatus.cs           # rediseñado (+ ModuleStatus, enums)
│   ├── LicenseEvaluator.cs        # rediseñado
│   ├── ModuleAccess.cs            # RequiredModules
│   └── LicenseLock.cs             # nuevo: permisos exentos
└── Users/Permission.cs            # + ManageCategories, ExportBackup (y RolePermissions)

src/Pos.Application/
├── Licensing/
│   ├── ILicenseVerifier.cs, ILicenseStore.cs, ILicenseSealStore.cs, ILicenseState.cs
│   ├── IInstalledLicenseStore.cs, IModuleCatalogInfo.cs        # nuevos
│   ├── LicenseBootstrapper.cs
│   ├── ImportLicense/, ExportLicenseRequest/, GetLicenseStatus/
├── Backup/ExportBackup/           # nuevo
├── Users/Access/AccessControl.cs  # bloqueo + RequiredModules + CheckToFinishAsync
├── Sales/SaveSaleDraft/, Sales/ConfirmSale/                     # venta en curso en bloqueo
├── Printing/PrintTicket/                                        # ticket original de la venta en curso
├── CashShifts/CountShiftCash/, CashShifts/CloseShift/           # cierre del turno abierto sin módulo
├── CashShifts/OpenShift/, CashShifts/RegisterCashMovement/      # rechazo en bloqueo
├── Printing/OpenCashDrawer/, CashShifts/Search*/, Reports/*MyShift*  # rechazo en bloqueo
├── Categories/*                   # ManageCategories; opciones vacías si inactivo
└── Products/UpdateProduct/        # conserva categoría si Categorías inactivo

src/Pos.Infrastructure/
├── Licensing/
│   ├── EcdsaLicenseVerifier.cs    # formato 3; LicenseCanonical.cs se elimina
│   ├── LicenseFileStore.cs        # versión 3 sin módulos
│   ├── LicenseSealStore.cs        # ausente vs alterado
│   ├── InstalledLicenseEntity.cs, InstalledLicenseStore.cs     # nuevos
│   └── EmbeddedModuleCatalogInfo.cs                            # nuevo
├── Persistence/ (DbSet + configuración + migración InstalledLicenses)
└── Pos.Infrastructure.csproj      # EmbeddedResource contracts/module-catalog.json

src/Pos.Desktop/
├── Licensing/
│   ├── LicenseViewModel.cs, LicenseView.axaml                  # nueva página help.license
│   ├── LicenseCard.cs, LicenseMessages.cs                      # estados nuevos y avisos
│   └── LicenseModule.cs
├── About/                          # quita importar/solicitud, conserva ID de máquina
├── Navigation/                     # menú en bloqueo; RequiredModules
├── Composition/LicenseClockScheduler.cs                        # medianoche + cada hora
└── Resources/Strings.resx          # textos de contracts/blocked-mode.md

tests/
├── Pos.Domain.Tests/Licensing/        # evaluador, antigüedad, ModuleAccess
├── Pos.Application.Tests/Licensing/   # importación, bootstrapper, bloqueo, venta en curso
├── Pos.Infrastructure.Tests/Licensing/ # catálogo↔JSON, verificador v3 + vector, InstalledLicenseStore, migración
└── Pos.ArchitectureTests/
```

**Structure Decision**: se respeta la estructura en capas existente, organizada por
funcionalidad (`Licensing/`, `Backup/ExportBackup/`). Los contratos compartidos con
OctopusAdmin van en `contracts/` de la raíz del repositorio (FR-004, FR-008), no dentro de
`specs/`, porque se copian tal cual al otro repositorio.

## Implementation order (guía para /speckit-tasks)

1. **Catálogo** (H1): contrato JSON (ya creado), `LicensedModule`/`ModuleCatalog`, recurso
   embebido, `IModuleCatalogInfo`, prueba de consistencia.
2. **Domain** (H2, H5, H7, H8): `ModuleGrant`, `SignedLicense`, `TrialRecord`, `LicenseStatus`,
   `LicenseEvaluator`, `ModuleAccess.RequiredModules`, `LicenseLock` + pruebas.
3. **Verificador v3 y almacén** (H2, H4): `EcdsaLicenseVerifier`, `InstalledLicenses` +
   migración, `LicenseFileStore` v3, `LicenseSealStore` ausente/alterado.
4. **Application** (H4, H5, H6): `LicenseState`, `LicenseBootstrapper`, `ImportLicenseHandler`,
   `AccessControl`, venta en curso/turno en bloqueo, `ExportBackup`, solicitud v2.
5. **Proveedores y Categorías** (H1): permisos y casos de uso.
6. **Desktop** (H3, H6, H7, H9): página "Ayuda > Licencia", menú en bloqueo, Inicio, scheduler.
7. **Clave pública de producción** (cuando OctopusAdmin la entregue).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `InstalledLicenses` sin campos de auditoría, borrado lógico ni versión (Principio IV) | Es un dato técnico de una sola fila, igual que `LicenseSeals`: el texto firmado debe guardarse sin modificaciones y se reemplaza completo. | Agregar auditoría/versión no aporta trazabilidad (la importación ya va a la bitácora) y complica el reemplazo atómico. |
| Pruebas con más casos que "válido + límite principal" en T034 (un rechazo por paso del contrato), T066 y T067 (venta en curso y turno abierto en bloqueo) (Principio VI) | Cada paso de verificación es una frontera de seguridad distinta del contrato compartido, y cada caso de venta o turno es una garantía explícita del Principio I. Un fallo en cualquiera cuesta ingresos al proveedor o detiene la caja del cliente. | Probar solo el caso válido y uno límite dejaría sin cubrir rechazos (formato 2, otra máquina, firma DER) y caminos del cobro (crédito o descuento ya capturados) que no se deducen unos de otros. |
| Constante en el código para las claves AES locales del registro de prueba (Principio IX) | La prueba debe detectar ediciones sin conexión y sin proveedor; no existe forma de hacerlo sin un valor conocido por la aplicación. | Prueba firmada por el proveedor: imposible en el primer arranque sin conexión. Se documenta como detección de ediciones casuales, no como secreto; la licencia comercial depende solo de la firma. |
