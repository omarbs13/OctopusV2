# Plan de implementación: Licencia local con período de evaluación

**Rama**: `011-local-trial-license` | **Fecha**: 2026-09-30 | **Especificación**: [spec.md](spec.md)

**Entrada**: especificación de `/specs/011-local-trial-license/spec.md`

## Resumen

Se agrega un módulo de licencia 100 % local, sin cambios de esquema de base de datos:

1. **Dominio** (`Pos.Domain/Licensing`): reglas puras que, dada la licencia local y la fecha de hoy, calculan el estado (evaluación con N días, activa, vencida) y los avisos (≤ 5 días, ≤ 1 día).
2. **Aplicación**: puertos (`IMachineIdProvider`, `ILicenseStore`, `ILicenseVerifier`, `IInstallationAgeReader`), un estado de licencia en memoria (`ILicenseState`) que se recalcula con el reloj en cada consulta, tres casos de uso (consultar estado, importar licencia, exportar solicitud) y el error `LicenseExpired`.
3. **Bloqueo gradual en un solo punto**: `AccessControl.CheckAsync` rechaza `Sell`, `ViewReports` y `ManageUsers` con `LicenseExpired` cuando el modo es lectura; `OpenShiftHandler` verifica explícitamente (porque `OperateShift` también cubre cerrar turno, que debe seguir permitido). Consultas de Productos, Inventario y Ventas registradas no cambian.
4. **Infraestructura**: ID de máquina estable desde el sistema operativo, archivo `license.lic` cifrado y autenticado en la carpeta de datos, verificación de la firma del proveedor (ECDSA P-256, sin conexión).
5. **Interfaz**: tarjeta "Período de evaluación" en Inicio, aviso rojo en el login, sección "Administración de licencia" en Acerca de, y bloqueo amable en la navegación.

Sin paquetes nuevos: se usa `System.Security.Cryptography`.

## Contexto técnico

**Lenguaje/Versión**: C# / .NET 10, nullable habilitado

**Dependencias principales**: las existentes (Avalonia, CommunityToolkit.Mvvm, FluentValidation, Serilog). **Sin dependencias nuevas** (Principio VII).

**Almacenamiento**: archivo `license.lic` en la raíz de `IAppPaths.DataDirectory`; sin cambios de esquema ni migraciones

**Pruebas**: xUnit (política mínima, Principio VI)

**Plataforma**: Windows y Linux; ID de máquina con adaptador por sistema operativo, rutas por `IAppPaths`

**Tipo de proyecto**: aplicación de escritorio por capas (Domain / Application / Infrastructure / Desktop)

**Metas de rendimiento**: consultar el estado de licencia no accede a disco (< 1 ms); importar una licencia y desbloquear en < 5 s sin reiniciar

**Restricciones**: sin red (FR-017); una falla leyendo el archivo nunca cierra la aplicación; el estado se recalcula al cambiar el día sin reiniciar

**Escala**: un equipo por instalación

## Verificación de la constitución

| Principio | Resultado | Notas |
|---|---|---|
| I. La venta nunca se detiene | Cumple con matiz | Es el objetivo comercial de la funcionalidad bloquear ventas tras el vencimiento (decisión del responsable). Sin red; un archivo ilegible deja modo lectura, nunca cierra la app; un turno abierto se puede cerrar (FR-010) y una venta ya confirmada no se pierde |
| II. Capas | Cumple | Reglas en Domain; puertos en Application; criptografía y sistema operativo en Infrastructure; UI en Desktop. Sin referencias nuevas entre proyectos |
| III. Lógica en el núcleo | Cumple | El cálculo de días y el bloqueo viven en Domain/Application; los ViewModels solo presentan |
| IV. Integridad de datos | Cumple | Sin cambios de esquema. Fechas en UTC dentro del archivo; el "día" para el usuario se calcula en hora local. Importes: no aplica |
| V. Multiplataforma | Cumple | ID de máquina con adaptadores Windows/Linux y reserva común; sin rutas fijas |
| VI. Calidad verificable | Cumple | Pruebas mínimas listadas abajo; sin pruebas de UI |
| VII. Simplicidad | Cumple | Sin paquetes nuevos; sin herramienta de emisión en el repositorio; sin validación remota |
| VIII. Soporte y diagnóstico | Cumple | Cambios de estado y rechazos se registran sin datos sensibles; el `.lic` se excluye de la exportación |
| IX. Seguridad local | Cumple | Importar licencia es operación sensible: queda en la bitácora de auditoría; no hay secretos en el repositorio (solo la clave **pública** del proveedor) |

**Desviación a vigilar**: el Principio I dice que la venta "nunca se detiene". La licencia la detiene por vencimiento comercial. No es un fallo técnico; se justifica por escrito aquí y el vencimiento se avisa con 5 y 1 días de antelación. Si el responsable prefiere enmendar la constitución, es un cambio aparte.

Reevaluado tras el diseño de la fase 1: sin violaciones nuevas.

## Pruebas (política mínima)

- **Dominio**: cálculo de días restantes (30, 1, 0 y vencido), reloj retrasado que no devuelve días, vigencia con fecha de fin y sin vencimiento.
- **Infraestructura**: archivo copiado a otra máquina se rechaza; archivo alterado se rechaza; archivo borrado se regenera con el mismo ID; licencia firmada válida / con otro ID / con firma alterada / más antigua que la vigente.
- **Aplicación**: `AccessControl` rechaza `Sell`, `ViewReports` y `ManageUsers` vencida y permite `ViewProducts`, `ViewInventory`, `ViewAllSales`; `OpenShift` se rechaza y `CloseShift` se permite.
- **Diagnóstico**: el zip exportado no contiene `license.lic`.
- Pruebas de arquitectura existentes (obligatorias) cubren las capas nuevas.

## Estructura del proyecto

### Documentación

```text
specs/011-local-trial-license/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── license-contracts.md
└── tasks.md             # lo crea /speckit-tasks
```

### Código fuente

```text
src/
├── Pos.Domain/
│   ├── Licensing/                         # nuevo
│   │   ├── LicenseRecord.cs               # datos del .lic ya validado
│   │   ├── LicenseStatus.cs               # estado calculado + avisos
│   │   └── LicenseEvaluator.cs            # reglas de días y modo
│   └── Users/Permission.cs                # + ManageLicense (solo Administrador)
├── Pos.Application/
│   ├── Abstractions/
│   │   ├── Error.cs                       # + LicenseExpired, InvalidLicense
│   │   └── IAppPaths.cs                   # + LicenseFile
│   ├── Licensing/                         # nuevo
│   │   ├── ILicenseState.cs               # estado actual (singleton) + LicenseState
│   │   ├── ILicenseStore.cs, IMachineIdProvider.cs, ILicenseVerifier.cs, IInstallationAgeReader.cs
│   │   ├── VendorContact.cs
│   │   ├── GetLicenseStatus/              # consulta para tarjeta, login y Acerca de
│   │   ├── ImportLicense/                 # importar sin reiniciar + auditoría
│   │   └── ExportLicenseRequest/          # archivo de solicitud con el ID
│   ├── Users/Access/AccessControl.cs      # bloqueo central de Sell/ViewReports/ManageUsers
│   └── CashShifts/OpenShift/OpenShiftHandler.cs  # verificación explícita
├── Pos.Infrastructure/
│   └── Licensing/                         # nuevo
│       ├── MachineIdProvider.cs           # Linux, Windows y reserva por MAC
│       ├── LicenseFileStore.cs            # license.lic cifrado (AES-GCM) y autenticado
│       ├── EcdsaLicenseVerifier.cs        # firma del proveedor
│       ├── InstallationAgeReader.cs       # fecha del primer usuario como evidencia de uso
│       └── LicenseClockSync.cs            # actualiza "última fecha vista"
├── Pos.Desktop/
│   ├── Licensing/                         # nuevo
│   │   ├── LicenseCard.cs                 # tarjeta de Inicio
│   │   └── LicenseModule.cs
│   ├── About/AboutView/ViewModel          # sección "Administración de licencia"
│   ├── Auth/LoginViewModel.cs, LoginView  # aviso rojo con ≤ 1 día
│   ├── Navigation/Navigator.cs            # bloqueo amable con el mensaje de contacto
│   ├── Resources/Strings.resx             # textos en español
│   └── Composition/LogRetentionScheduler.cs o equivalente  # re-evaluación al cambiar el día
docs/carpeta-de-datos.md                   # documenta license.lic
tests/                                     # pruebas mínimas listadas arriba
```

**Decisión de estructura**: se mantiene la estructura por funcionalidad ya usada (`Licensing/` en cada capa). Sin proyectos nuevos.

## Seguimiento de complejidad

Sin violaciones que justificar, salvo la desviación del Principio I descrita arriba.
