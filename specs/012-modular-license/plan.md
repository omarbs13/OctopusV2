# Plan de implementación: Licencia modular por módulos

**Rama**: `012-modular-license` | **Fecha**: 2026-09-30 | **Especificación**: [spec.md](spec.md)

**Entrada**: especificación de `/specs/012-modular-license/spec.md`

## Resumen

Se evoluciona la licencia local de 011 (evaluación + modo lectura global) a un modelo **modular**: 30 días con todos los módulos y, después, solo los módulos comprados. Se reutiliza lo existente (ID de máquina, archivo cifrado `license.lic`, verificación ECDSA, importación en Acerca de) y se reemplaza el bloqueo global por un bloqueo por módulo.

1. **Dominio** (`Pos.Domain/Licensing`): enum `LicensedModule` (5 módulos), `ModuleCatalog` (mapeo fijo GUID ↔ módulo, interno), `ModuleAccess` (qué `Permission` pertenece a qué módulo) y el evaluador de evaluación (30 días, avisos exactos a 5 y 1 día, reloj atrasado).
2. **Aplicación**: `ILicenseState` pasa de "solo lectura sí/no" a `IsModuleActive(módulo)`; `AccessControl` rechaza con el error `ModuleNotLicensed` ("Este módulo no está activo en tu licencia.") antes de revisar roles; `ConfirmSale`/`CancelSale` omiten sin error las partes de Inventario y Turnos cuando están bloqueados (FR-021, aclaración 4); el importador **suma** módulos; el arranque recupera la fecha de inicio desde una copia protegida en la base (FR-018) y migra el archivo 011 (FR-022).
3. **Infraestructura**: `license.lic` pasa a versión 2 (lista de GUID, días de evaluación) con lectura de la versión 1 solo para migrar; verificador de licencia extendida formato 2 (lista de módulos firmada); tabla `LicenseSeals` con la copia cifrada de inicio y última fecha vista (una migración EF Core).
4. **Interfaz**: el menú oculta opciones de módulos inactivos y se reconstruye al importar sin reiniciar; Inicio muestra los avisos de 5 y 1 día; se retiran el modo lectura, el aviso rojo del login y el "bloqueo amable" de 011.

Sin paquetes nuevos: `System.Security.Cryptography` y EF Core ya presentes.

## Contexto técnico

**Lenguaje/Versión**: C# / .NET 10, nullable habilitado

**Dependencias principales**: las existentes (Avalonia, CommunityToolkit.Mvvm, FluentValidation, EF Core SQLite, Serilog). **Sin dependencias nuevas** (Principio VII).

**Almacenamiento**: archivo `license.lic` (formato v2) en `IAppPaths.DataDirectory` + tabla nueva `LicenseSeals` en SQLite (una fila, carga cifrada). **Una migración EF Core** (Principio IV).

**Pruebas**: xUnit, política mínima (Principio VI); persistencia contra SQLite real

**Plataforma**: Windows y Linux; sin rutas fijas; cifrado con APIs de .NET

**Tipo de proyecto**: aplicación de escritorio por capas (Domain / Application / Infrastructure / Desktop)

**Metas de rendimiento**: `IsModuleActive` sin acceso a disco (< 1 ms); importar y reflejar en el menú en < 5 s sin reiniciar (SC-004 pide < 1 min en total)

**Restricciones**: sin red (FR-020); una falla de licencia nunca cierra la aplicación ni bloquea la venta básica; el estado se recalcula con el reloj en cada consulta (cruzar la medianoche aplica el cambio sin reiniciar)

**Escala**: un equipo por instalación

Sin `NEEDS CLARIFICATION` pendientes; las decisiones de interpretación están en [research.md](research.md).

## Verificación de la constitución

| Principio | Resultado | Notas |
|---|---|---|
| I. La venta nunca se detiene | **Cumple (mejora respecto a 011)** | Venta básica, productos, ajustes y usuarios nunca se bloquean (FR-021). Con Turnos o Inventario bloqueados la venta se completa omitiendo esa parte; no hay modo lectura global. Sin red; un archivo ilegible se regenera y nunca cierra la app |
| II. Capas | Cumple | Reglas y mapeo en Domain; puertos (`ILicenseSealStore`) en Application; cifrado, archivo y EF en Infrastructure; UI en Desktop. Sin referencias nuevas entre proyectos |
| III. Lógica en el núcleo | Cumple | Evaluación, mapeo permiso→módulo y bloqueo viven en Domain/Application; los ViewModels solo presentan y ocultan opciones según `ILicenseState` |
| IV. Integridad de datos | Cumple con una migración | Tabla `LicenseSeals` aditiva (no destruye datos); respaldo previo y migración automática ya existentes; fechas en UTC; se agrega una base de ejemplo/prueba de migración. `LicenseSeals` es dato técnico de la instalación, no entidad de negocio: sin campos de auditoría, borrado lógico ni versión (justificado en [data-model.md](data-model.md)); el `Id` es GUID v7 |
| V. Multiplataforma | Cumple | Sin cambios de plataforma; mismo cifrado en ambos sistemas |
| VI. Calidad verificable | Cumple | Pruebas mínimas abajo; incluye consistencia de inventario (venta con Inventario bloqueado) y migración, que son obligatorias |
| VII. Simplicidad | Cumple | Sin paquetes, sin servicio de licencias remoto, sin emisor en el repo. Se agrega `LastSeen` a la copia protegida solo porque SC-007 lo exige (ver research §3) |
| VIII. Soporte y diagnóstico | Cumple | Se registran importación, rechazos, recuperación desde la copia y migración 011→012, sin GUID de módulos ni contenido del archivo; `license.lic` sigue fuera de la exportación |
| IX. Seguridad local | Cumple | Importar sigue siendo operación auditada (`ManageLicense`, solo Administrador); solo la clave **pública** del proveedor en el repositorio; el mapeo GUID→módulo no es secreto criptográfico, la protección es el cifrado y la firma |

**Riesgo conocido (no es una violación)**: con Inventario bloqueado las ventas no descuentan existencias, así que el inventario queda desfasado hasta reactivar el módulo. Es el comportamiento decidido en la aclaración 4; se documenta en el mensaje de soporte y en [quickstart.md](quickstart.md).

Reevaluado tras el diseño de la fase 1: sin violaciones nuevas.

## Pruebas (política mínima)

- **Dominio**: evaluación (día 30 con todos, día 31 modular, reloj atrasado no devuelve días, aviso solo con 5 y con 1 día); catálogo (GUID únicos, desconocido se ignora); mapa permiso→módulo (Inventario, Reportes, Turnos; `Sell` y `ManageUsers` sin módulo).
- **Aplicación**: `AccessControl` rechaza un permiso de módulo inactivo con `ModuleNotLicensed` y permite `Sell`; `ConfirmSale` con Turnos bloqueado vende sin turno y con Inventario bloqueado no genera movimientos ni valida existencias (**consistencia de inventario**); `CancelSale` omite la reposición con Inventario bloqueado; el importador suma módulos sin tocar la fecha de inicio y es idempotente; el arranque: archivo borrado recupera la fecha de la copia protegida (SC-007), archivo editado se trata como borrado, migración 011 (licencia activa → todos los módulos; sin ella → evaluación nueva).
- **Infraestructura**: `license.lic` v2 ida y vuelta, alterado y de otra máquina; lectura de v1; verificador formato 2 (válido / otra máquina / firma alterada / GUID desconocido ignorado / formato 1 rechazado); `LicenseSealStore` con SQLite real (guardar, leer, carga alterada → ausente).
- **Arranque y migraciones (obligatoria)**: la prueba de migración existente cubre la nueva migración y las bases de ejemplo.
- Pruebas de arquitectura existentes (obligatorias) cubren las capas.
- Se actualizan o eliminan las pruebas de 011 que asumían modo lectura.

## Estructura del proyecto

### Documentación

```text
specs/012-modular-license/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── license-contracts.md
├── checklists/
│   └── requirements.md
└── tasks.md             # lo crea /speckit-tasks
```

### Código fuente

```text
src/Pos.Domain/Licensing/
├── LicensedModule.cs          # nuevo: enum de los 5 módulos
├── ModuleCatalog.cs           # nuevo: mapeo fijo GUID ↔ módulo (interno)
├── ModuleAccess.cs            # nuevo: Permission → LicensedModule?
├── LicenseRecord.cs           # cambia: TrialDays + módulos habilitados; sin LicenseGrant/ValidUntil
├── LicenseStatus.cs           # cambia: fase Evaluación/Modular, días, aviso; sin Expired/Invalid/IsReadOnly
└── LicenseEvaluator.cs        # cambia: evaluación 30 días, avisos exactos 5 y 1

src/Pos.Application/
├── Licensing/
│   ├── ILicenseState.cs       # cambia: IsModuleActive, EnabledModules, evento Changed; sin IsBlocked
│   ├── ILicenseStore.cs       # cambia: resultado Loaded/Missing/Unusable + lectura de v1 para migrar
│   ├── ILicenseSealStore.cs   # nuevo: copia protegida de inicio y última fecha vista
│   ├── ILicenseVerifier.cs    # cambia: devuelve módulos firmados
│   ├── LicenseBootstrapper.cs # cambia: recuperación desde la copia, migración 011, archivo inválido = borrado
│   ├── ImportLicense/         # cambia: suma módulos, no toca fecha de inicio
│   └── GetLicenseStatus/      # cambia: DTO con fase, días, aviso y módulos activos
├── Abstractions/Error.cs      # LicenseExpired → ModuleNotLicensed; se ajusta InvalidLicense
├── Users/Access/AccessControl.cs   # bloqueo por módulo antes de roles
├── Sales/ConfirmSale/         # omite turno e inventario si el módulo está inactivo
├── Sales/CancelSale/          # omite reposición de inventario y regla de turno si inactivos
└── CashShifts/OpenShift/      # se retira la verificación explícita de 011 (la cubre AccessControl)

src/Pos.Infrastructure/
├── Licensing/
│   ├── LicenseFileStore.cs    # v2 (lee v1 solo para migrar)
│   ├── EcdsaLicenseVerifier.cs# formato 2: lista de módulos
│   ├── LicenseCanonical.cs    # canónico formato 2
│   ├── LicenseSealStore.cs    # nuevo: copia cifrada en SQLite
│   └── InstallationAgeReader.cs  # sin cambios (evidencia de uso previo)
└── Persistence/
    ├── Configurations/LicenseSealConfiguration.cs   # nuevo
    └── Migrations/<fecha>_ModularLicense.cs         # nueva migración

src/Pos.Desktop/
├── Navigation/MenuViewModel.cs, Navigator.cs  # ocultar/bloquear por módulo; reconstruir con Changed
├── Home/ (tarjeta de licencia)                # avisos 5 y 1 día
├── About/AboutViewModel.cs                    # resumen con módulos activos
├── Licensing/LicenseMessages.cs               # textos nuevos
├── Shell/RootViewModel.cs                     # se retira el aviso de modo lectura del login
└── Resources/Strings.resx                     # textos en español

tests/ (Pos.Domain.Tests, Pos.Application.Tests, Pos.Infrastructure.Tests)
```

**Decisión de estructura**: se mantiene el módulo `Licensing` de 011 en cada capa, organizado por funcionalidad. El mapeo permiso→módulo vive en Domain para que `AccessControl`, el menú y los casos de uso consulten una sola fuente.

## Seguimiento de complejidad

Sin violaciones que justificar.
