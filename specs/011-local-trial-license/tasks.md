---

description: "Tareas de implementación: Licencia local con período de evaluación"
---

# Tareas: Licencia local con período de evaluación

**Entrada**: documentos de diseño en `/specs/011-local-trial-license/`

**Prerrequisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/license-contracts.md](contracts/license-contracts.md), [quickstart.md](quickstart.md)

**Pruebas**: solo las que exige la política mínima de la constitución (Principio VI): cálculo de días, archivo `.lic` (copia a otra máquina, alteración, regeneración), verificación de licencias importables, bloqueo en `AccessControl` y `OpenShift`, y exclusión del `.lic` del diagnóstico. Sin pruebas de interfaz ni de ViewModels. Al implementar se ejecutan solo las pruebas del proyecto modificado (`dotnet test --verbosity quiet <proyecto>`); compilar con `dotnet build -v q`.

**Organización**: por historia de usuario. Rutas relativas a la raíz del repositorio. Código en inglés, textos de interfaz y documentación en español.

## Formato: `[ID] [P?] [Historia] Descripción`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[USn]**: historia de usuario a la que pertenece

---

## Fase 1: Preparación

- [X] T001 Ejecutar `dotnet build -v q` desde la raíz y anotar el estado inicial (debe terminar sin errores ni advertencias).
- [X] T002 [P] Crear las carpetas `src/Pos.Domain/Licensing/`, `src/Pos.Application/Licensing/`, `src/Pos.Infrastructure/Licensing/`, `src/Pos.Desktop/Licensing/` y las de pruebas `tests/Pos.Domain.Tests/Licensing/`, `tests/Pos.Application.Tests/Licensing/`, `tests/Pos.Infrastructure.Tests/Licensing/`.

---

## Fase 2: Fundamentos (bloquea todas las historias)

**Propósito**: tipos, puertos y estado compartidos por todas las historias.

- [X] T003 [P] Crear `src/Pos.Domain/Licensing/LicenseRecord.cs` y `LicenseGrant.cs` según [data-model.md](data-model.md): `LicenseRecord(Version, MachineId, FirstRunUtc, LastSeenUtc, Grant?)` y `LicenseGrant(MachineId, IssuedAtUtc, ValidUntil: DateOnly?, Signature)`; inmutables, sin dependencias.
- [X] T004 [P] Crear `src/Pos.Domain/Licensing/LicenseStatus.cs` con `LicenseKind` (`Trial`, `Licensed`, `Expired`, `Invalid`), `LicenseWarning` (`None`, `Near`, `Urgent`), `InvalidLicenseReason` (`OtherMachine`, `Corrupt`) y el registro `LicenseStatus(Kind, DaysRemaining: int?, IsReadOnly, Warning, InvalidReason?)`.
- [X] T005 Crear `src/Pos.Domain/Licensing/LicenseEvaluator.cs`: función pura `Evaluate(LicenseRecord record, DateTime nowUtc, TimeZoneInfo zone)` que usa como fecha efectiva el máximo entre `nowUtc` y `LastSeenUtc`, la pasa a fecha local y calcula `Trial`: `DaysRemaining = 30 − días de calendario entre FirstRunUtc (local) y hoy`; `Licensed` con fin: `ValidUntil − hoy`; sin vencimiento: `DaysRemaining` nulo y `Kind = Licensed`. `DaysRemaining ≤ 0` ⇒ `Expired` e `IsReadOnly`. Aviso `Near` con ≤ 5 días y `Urgent` con ≤ 1 día (solo si quedan días ≥ 1). La constante `TrialDays = 30` es pública.
- [X] T006 [P] Agregar `ManageLicense` a `src/Pos.Domain/Users/Permission.cs` (con su `<summary>`). Verificar en `src/Pos.Domain/Users/RolePermissions.cs` que solo el Administrador lo tiene (ya lo recibe porque el conjunto de administrador incluye todos los valores) y que no es autorizable.
- [X] T007 [P] Agregar a `src/Pos.Application/Abstractions/Error.cs`: `LicenseExpired(string ContactPhone, string ContactEmail)`, `InvalidLicense(InvalidLicenseReason Reason)` con los motivos `Unreadable`, `BadSignature`, `OtherMachine`, `Older` (enumeración `InvalidLicenseImportReason`), cada uno con su `<summary>` en español.
- [X] T008 [P] Agregar `string LicenseFile { get; }` a `src/Pos.Application/Abstractions/IAppPaths.cs`; implementarlo en `src/Pos.Infrastructure/Platform/AppPaths.cs` como `Path.Combine(DataDirectory, "license.lic")` y en `tests/Pos.Desktop.Tests/TestSupport/FakeAppPaths.cs` y en cualquier otra implementación que no compile.
- [X] T009 [P] Crear los puertos en `src/Pos.Application/Licensing/`: `IMachineIdProvider` (`string GetMachineId()`), `ILicenseStore` (`LicenseLoadResult Load()`, `void Save(LicenseRecord)`; `LicenseLoadResult` = `Loaded(record)` / `Missing` / `Invalid(reason)`), `ILicenseVerifier` (`VerifyResult Verify(Stream file, string machineId)` que devuelve la concesión o el motivo de rechazo) e `IInstallationAgeReader` (`Task<DateTime?> GetFirstUserCreatedUtcAsync(CancellationToken)`).
- [X] T010 [P] Crear `src/Pos.Application/Licensing/VendorContact.cs`: registro con `Phone` y `Email` y valor por omisión de marcador de posición (`"[teléfono]"`, `"[email]"`) marcado con comentario `// Pendiente: datos reales del proveedor`.
- [X] T011 Crear `src/Pos.Application/Licensing/ILicenseState.cs`: interfaz (`LicenseStatus Current { get; }`, `void Initialize(...)`, `void Apply(LicenseRecord)`) y su implementación singleton `LicenseState` que guarda el `LicenseRecord` o el estado `Invalid` y recalcula `Current` con `LicenseEvaluator` y `IClock` en cada lectura (sin acceso a disco), segura entre hilos. Incluir `bool IsBlocked(Permission)`: verdadero si `Current.IsReadOnly` y el permiso es `Sell`, `ViewReports` o `ManageUsers`.
- [X] T012 Registrar en `src/Pos.Application/DependencyInjection.cs` (`LicenseState` como singleton, `VendorContact`) y en `src/Pos.Infrastructure/DependencyInjection.cs` los adaptadores de las historias a medida que existan; dejar la estructura lista.

**Punto de control**: `dotnet build -v q` sin errores ni advertencias.

---

## Fase 3: Historia 1 - Identificador único de máquina (P1) 🎯 MVP

**Objetivo**: primer arranque genera el ID y crea `license.lic` protegido; copiarlo a otra máquina o alterarlo lo invalida.

**Prueba independiente**: escenarios 1 y 2 de [quickstart.md](quickstart.md).

- [X] T013 [P] [US1] Crear `src/Pos.Infrastructure/Licensing/MachineIdProvider.cs`: ID = SHA-256 hexadecimal (64 caracteres) de `sal fija + identificador`, donde el identificador es, en orden, `/etc/machine-id` o `/var/lib/dbus/machine-id` (Linux), `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid` (Windows, con `OperatingSystem.IsWindows()`), y como reserva la MAC de la primera interfaz de red física ordenada por nombre. El resultado se calcula una vez y se guarda en memoria.
- [X] T014 [US1] Crear `src/Pos.Infrastructure/Licensing/LicenseFileStore.cs` (implementa `ILicenseStore`): formato binario con encabezado de versión, nonce y contenido cifrado con AES-256-GCM; la clave sale de HKDF-SHA256 con un secreto de la aplicación y el ID de máquina. Contenido JSON con `machineId`, `firstRunUtc`, `lastSeenUtc` y `grant`. Escritura atómica (archivo temporal + reemplazo). Un `.lic` que no se descifra (otra máquina o alterado) devuelve `Invalid(OtherMachine)` si el encabezado es válido y el descifrado falla, y `Invalid(Corrupt)` si el formato es ilegible; un `.lic` descifrado cuyo `machineId` no coincide devuelve `Invalid(OtherMachine)`. Nunca lanza por contenido inválido; registra una advertencia sin el contenido.
- [X] T015 [P] [US1] Crear `src/Pos.Infrastructure/Licensing/InstallationAgeReader.cs` (implementa `IInstallationAgeReader`): lee con el `DbContext` la menor `CreatedAt` de `Users` no del sistema; devuelve nulo si no hay usuarios.
- [X] T016 [US1] Crear `src/Pos.Application/Licensing/LicenseBootstrapper.cs`: al arrancar llama a `ILicenseStore.Load()`; si es `Missing`, crea el registro con `FirstRunUtc = min(ahora, primer usuario)` (research §5) y `LastSeenUtc = ahora`, lo guarda y lo aplica a `ILicenseState`; si es `Loaded`, actualiza `LastSeenUtc = max(LastSeenUtc, ahora)` y lo guarda solo si avanzó; si es `Invalid`, aplica el estado inválido sin sobrescribir el archivo. Una excepción de E/S deja el estado `Invalid(Corrupt)` y se registra; nunca cierra la aplicación.
- [X] T017 [US1] Registrar en `src/Pos.Infrastructure/DependencyInjection.cs` `IMachineIdProvider`, `ILicenseStore` e `IInstallationAgeReader` (esta última con ámbito, porque usa el `DbContext`), y llamar a `LicenseBootstrapper` en el arranque después de las migraciones, en `src/Pos.Desktop/Startup/StartupPresenter.cs` o el paso equivalente de `src/Pos.Application/Startup/DatabaseStartup.cs`, de modo que el estado exista antes de mostrar el login.
- [X] T018 [P] [US1] Pruebas en `tests/Pos.Infrastructure.Tests/Licensing/LicenseFileStoreTests.cs`: un `.lic` válido se lee; copiado a un `MachineIdProvider` falso con otro ID devuelve `OtherMachine`; con un byte alterado devuelve inválido; no existe y se regenera conservando el ID (`Missing` → `Save` → `Loaded` con el mismo ID). Usar carpeta temporal.

**Punto de control**: la primera ejecución crea `license.lic`; el escenario 2 del quickstart se cumple.

---

## Fase 4: Historia 2 - Período de evaluación de 30 días (P1)

**Objetivo**: los días restantes se calculan en cada consulta y Inicio muestra la tarjeta.

**Prueba independiente**: escenarios 1, 8 y 9 de [quickstart.md](quickstart.md).

- [X] T019 [P] [US2] Pruebas en `tests/Pos.Domain.Tests/Licensing/LicenseEvaluatorTests.cs`: inicio hoy ⇒ 30 días; hace 29 días ⇒ 1; hace 30 días ⇒ 0 y `Expired`; reloj retrasado (ahora < `LastSeenUtc`) no aumenta los días; concesión con fecha de fin y sin vencimiento.
- [X] T020 [US2] Crear `src/Pos.Application/Licensing/GetLicenseStatus/GetLicenseStatusHandler.cs` y `LicenseStatusDto.cs` según el contrato 3: devuelve `Kind`, `DaysRemaining`, `IsReadOnly`, `Warning`, `ContactPhone`, `ContactEmail`, `InvalidReason`; sin permiso (se usa también antes de iniciar sesión). Registrar en `src/Pos.Application/DependencyInjection.cs`.
- [X] T021 [US2] Crear `src/Pos.Desktop/Licensing/LicenseCard.cs`: `DashboardCard` de tipo `Metric`, `Order` 0, sin permiso, que llama a `GetLicenseStatusHandler` por `UseCases.RunAsync` y muestra: en `Trial` "Período de evaluación" con "{n} días restantes" y el contacto; en `Licensed` con fin "Licencia vigente: {n} días"; sin vencimiento no muestra la tarjeta (`SetEmpty` oculto o `Order` fuera) — decidir y dejar un solo comportamiento documentado en el código; en `Expired`/`Invalid` el mensaje "Sistema en modo lectura. Contacte para activación." (más el motivo si es `Invalid`).
- [X] T022 [US2] Crear `src/Pos.Desktop/Licensing/LicenseModule.cs` registrando `LicenseCard` con `AddDashboardCard<LicenseCard>()` y llamarlo desde `src/Pos.Desktop/Composition/HostBuilder.cs` junto con los demás módulos.
- [X] T023 [P] [US2] Agregar a `src/Pos.Desktop/Resources/Strings.resx` los textos de [contracts/license-contracts.md](contracts/license-contracts.md) §6 (tarjeta, días restantes, modo lectura, contacto) con claves `License_*`.
- [X] T024 [US2] (implementado como `src/Pos.Desktop/Composition/LicenseClockScheduler.cs`, cada hora) Re-evaluar al cambiar el día sin reiniciar: en `src/Pos.Desktop/Composition/LogRetentionScheduler.cs` (o un `LicenseClockSync` nuevo en `src/Pos.Infrastructure/Licensing/LicenseClockSync.cs` siguiendo el mismo temporizador) actualizar `LastSeenUtc` en `.lic` cuando cambia el día; el estado en memoria ya se recalcula solo (T011). Una falla se registra y no interrumpe.

**Punto de control**: Inicio muestra los días correctos (criterio 6).

---

## Fase 5: Historia 3 - Bloqueo gradual tras 30 días (P1)

**Objetivo**: en modo lectura se bloquean venta, apertura de turno, reportes y usuarios; se permite consultar Productos, Inventario y Ventas; se puede cerrar un turno abierto.

**Prueba independiente**: escenarios 3 y 4 de [quickstart.md](quickstart.md).

- [X] T025 [US3] Modificar `src/Pos.Application/Users/Access/AccessControl.cs`: inyectar `ILicenseState` y `VendorContact`; al inicio de `CheckAsync` (ambas sobrecargas), si `licenseState.IsBlocked(permission)` devolver `AccessDecision.Deny(new LicenseExpired(phone, email))` y registrar un WARNING sin datos sensibles. `HasAsync` no cambia. Actualizar el constructor en `tests/Pos.Application.Tests/TestSupport/AuthFixture.cs` con un `ILicenseState` falso configurable.
- [X] T026 [US3] Modificar `src/Pos.Application/CashShifts/OpenShift/OpenShiftHandler.cs`: tras comprobar el permiso, si `ILicenseState.Current.IsReadOnly` devolver `Result.Failure<CurrentShiftSummary>(new LicenseExpired(...))`. No tocar `CloseShiftHandler`, `CountShiftCashHandler` ni `RegisterCashMovementHandler` (FR-010). Actualizar el registro de DI y las pruebas que construyen el handler.
- [X] T027 [P] [US3] Agregar `LicenseExpired` a `IsExpected` en `src/Pos.Desktop/Common/UseCases.cs` para que no se registre como ERROR.
- [X] T028 [US3] Bloqueo amable en `src/Pos.Desktop/Navigation/Navigator.cs`: inyectar `ILicenseState`, `VendorContact` e `IDialogService` (opcionales, como `DiagnosticContext`); antes de navegar, si la opción exige `Sell`, `ViewReports` o `ManageUsers` y `IsBlocked(permiso)`, mostrar "Período de evaluación vencido. Contacte a {contacto}." y devolver `false` sin cambiar de pantalla. Cuando el sistema está en modo lectura, la pantalla segura de recuperación (`SafeScreenId`) pasa a ser Inicio.
- [X] T029 [US3] Mostrar el mensaje de vencimiento en las pantallas que reciben el error de los casos de uso: manejar `LicenseExpired` en `src/Pos.Desktop/CashShifts/OpenShiftViewModel.cs` (hoy maneja `Forbidden`, líneas ~95) y en `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs`, con una cadena `License_Expired` en `Strings.resx` que incluya el contacto. En Inicio, las tarjetas de reportes que reciban `LicenseExpired` se muestran vacías en lugar de con error (`src/Pos.Desktop/Reports/AlertsCard.cs`).
- [X] T030 [P] [US3] Pruebas en `tests/Pos.Application.Tests/Licensing/LicenseBlockingTests.cs` usando `AuthFixture`: vencida, `Sell`, `ViewReports` y `ManageUsers` devuelven `LicenseExpired`; `ViewProducts`, `ViewInventory`, `ViewAllSales`, `OperateShift` y `ManageLicense` se permiten; `OpenShiftHandler` se rechaza y `CloseShiftHandler` se permite; en evaluación todo se permite.

**Punto de control**: criterio 3 de la especificación.

---

## Fase 6: Historia 4 - Extensión de licencia (P2)

**Objetivo**: un administrador importa una licencia firmada sin reiniciar y puede exportar la solicitud con su ID.

**Prueba independiente**: escenarios 5 y 6 de [quickstart.md](quickstart.md).

- [X] T031 [P] [US4] Crear `src/Pos.Infrastructure/Licensing/EcdsaLicenseVerifier.cs` (implementa `ILicenseVerifier`): lee el JSON del contrato 2, valida `format`, arma el contenido canónico (`format`, `machineId`, `issuedUtc`, `validUntil` en ese orden, UTF-8, sin espacios) y verifica la firma ECDSA P-256 con la clave pública incluida como constante (`LicensePublicKey`); en compilaciones `DEBUG` admite además `POS_LICENSE_DEV_PUBLIC_KEY` (`#if DEBUG`, excluido de `Release`). Devuelve `Unreadable`, `BadSignature` u `OtherMachine` (ID distinto al de la máquina). La clave de producción es un marcador de posición documentado hasta que el proveedor entregue la suya.
- [X] T032 [US4] Crear `src/Pos.Application/Licensing/ImportLicense/ImportLicenseHandler.cs` y `ImportLicenseCommand.cs`: exige `ManageLicense` (`IAccessControl`), abre el archivo, verifica con `ILicenseVerifier`, rechaza con `InvalidLicense(Older)` si `IssuedAtUtc` es anterior a la concesión vigente, y si todo es válido escribe `LicenseRecord` con la concesión (`FirstRunUtc` se conserva; si el `.lic` estaba inválido o ausente se parte de `min(ahora, primer usuario)`), llama a `ILicenseState.Apply` y registra la operación en la bitácora con `IAuditLog` (sin el contenido del archivo). Cualquier fallo conserva el `.lic` actual. Devuelve `Result<LicenseStatusDto>`.
- [X] T033 [P] [US4] Crear `src/Pos.Application/Licensing/ExportLicenseRequest/ExportLicenseRequestHandler.cs` y su comando: exige `ManageLicense`, escribe el archivo `*.posreq` del contrato 1 (`format`, `machineId`, `appVersion`, `createdUtc`) en la ruta indicada de forma atómica (temporal + renombrado) y devuelve `ExportFailed` ante errores de E/S. Registrar ambos handlers en `src/Pos.Application/DependencyInjection.cs` y el verificador en `src/Pos.Infrastructure/DependencyInjection.cs`.
- [X] T034 [US4] Agregar la sección "Administración de licencia" a `src/Pos.Desktop/About/AboutView.axaml` y `AboutViewModel.cs`: visible solo con `Permission.ManageLicense` (`ICurrentPermissions`), muestra el estado vigente, un botón "Importar licencia" (diálogo para abrir `.poslic`; ver `src/Pos.Desktop/Common/FileSelection.cs`/`IDialogService`) y un botón "Exportar solicitud" (diálogo para guardar `.posreq`). Tras importar con éxito refresca el estado y el menú sin reiniciar. Mensajes de rechazo específicos (otra máquina, no válido, anterior a la vigente) en `Strings.resx`.
- [X] T035 [P] [US4] Pruebas en `tests/Pos.Infrastructure.Tests/Licensing/EcdsaLicenseVerifierTests.cs` con un emisor de apoyo en `tests/Pos.Infrastructure.Tests/Licensing/TestLicenseIssuer.cs` (par de claves de prueba generado en la prueba): licencia válida se acepta; con otro ID de máquina da `OtherMachine`; con la firma alterada da `BadSignature`.
- [X] T036 [P] [US4] Pruebas en `tests/Pos.Application.Tests/Licensing/ImportLicenseHandlerTests.cs`: importar válida levanta el modo lectura sin reiniciar (`ILicenseState.Current` cambia); un archivo rechazado deja el estado intacto; una licencia más antigua que la vigente da `Older`; sin `ManageLicense` da `Forbidden`.

**Punto de control**: criterios 4 y 5.

---

## Fase 7: Historia 5 - Avisos anticipados (P3)

**Objetivo**: aviso en Inicio a ≤ 5 días y aviso rojo en el login a ≤ 1 día.

**Prueba independiente**: escenario 7 de [quickstart.md](quickstart.md).

- [X] T037 [US5] Extender `src/Pos.Desktop/Licensing/LicenseCard.cs` para que con `Warning = Near` o `Urgent` el mensaje de la tarjeta incluya la notificación de vencimiento próximo ("Quedan {n} días: contacte para renovar").
- [X] T038 [US5] Aviso rojo en el login: en `src/Pos.Desktop/Shell/RootViewModel.cs` (donde se crea `LoginViewModel`, línea ~172) consultar `GetLicenseStatusHandler` y pasar a `LoginViewModel` (`src/Pos.Desktop/Auth/LoginViewModel.cs`) una propiedad `LicenseWarning` (texto) solo cuando `Warning == Urgent`; mostrarla en rojo en `src/Pos.Desktop/Auth/LoginView.axaml` encima del formulario. Con el sistema ya vencido, mostrar el mensaje de modo lectura en el login con el mismo estilo. Textos en `Strings.resx`.

**Punto de control**: con más de 5 días no aparece ningún aviso adicional.

---

## Fase 8: Pulido y transversales

- [X] T039 [P] Prueba en `tests/Pos.Infrastructure.Tests/Diagnostics/ZipDiagnosticsExporterTests.cs`: con un `license.lic` en la carpeta de datos, el zip exportado no contiene ninguna entrada llamada `license.lic` (FR-014).
- [X] T040 [P] Documentar `license.lic`, los archivos `.posreq`/`.poslic`, el proceso de regeneración y que no se debe borrar ni copiar entre máquinas en `docs/carpeta-de-datos.md` (agregar `license.lic` al árbol de la estructura).
- [X] T041 Revisar que las pruebas de arquitectura existentes (`tests/Pos.ArchitectureTests`) pasan con los nuevos espacios de nombres (Domain sin dependencias; Application sin Infrastructure) y que `CompositionRootTests` (`tests/Pos.Desktop.Tests/Composition/CompositionRootTests.cs`) valida el grafo con los nuevos servicios.
- [X] T042 Ejecutar `dotnet build -v q` desde la raíz (sin errores ni advertencias) y `dotnet test --verbosity quiet` en `tests/Pos.Domain.Tests`, `tests/Pos.Application.Tests`, `tests/Pos.Infrastructure.Tests`, `tests/Pos.Desktop.Tests` y `tests/Pos.ArchitectureTests`.
- [ ] T043 Recorrer los escenarios 1 a 10 de [quickstart.md](quickstart.md) a mano con `POS_DATA_DIR` aislado y anotar cualquier diferencia.

---

## Dependencias y orden de ejecución

- **Fase 1** → **Fase 2** (bloquea todo) → historias.
- **US1** (Fase 3) primero: crea el `.lic` y el estado que usan las demás.
- **US2** depende de US1 (T016/T017 inicializan el estado).
- **US3** depende de T011 (estado) y T007/T025; es independiente de US2 salvo por el mensaje de Inicio de T021.
- **US4** depende de US1 (archivo y estado) y de T006/T007; los avisos de US5 dependen de US2 (tarjeta) y del login.
- **US5** depende de US2.

Orden de historias: US1 → US2 → US3 → US4 → US5.

## Oportunidades de paralelismo

- Fase 2: T003, T004, T006, T007, T008, T009 y T010 en paralelo (archivos distintos).
- US1: T013 y T015 en paralelo; T018 tras T014.
- US2: T019 y T023 en paralelo con T020.
- US3: T027 y T030 en paralelo con T025/T026.
- US4: T031, T033 y T035 en paralelo; T036 tras T032.
- Pulido: T039 y T040 en paralelo.

## Estrategia de implementación

1. **MVP**: Fases 1 a 3 (US1) más US2 y US3: la licencia se crea, cuenta los días y bloquea. Con eso ya hay control comercial.
2. **Incremento 2**: US4 (importar y exportar la solicitud) para poder activar sin intervención técnica.
3. **Incremento 3**: US5 (avisos).
4. Pulido y validación manual al final.

## Notas

- Pendiente del responsable: datos reales de contacto (T010) y clave pública de producción (T031).
- El `.lic` y los archivos de licencia nunca se escriben en los logs ni en el diagnóstico.
- Hacer commit después de cada fase o grupo lógico.
