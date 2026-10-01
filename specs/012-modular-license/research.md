# Investigación: Licencia modular por módulos

Decisiones tomadas para resolver las incógnitas del plan. Parte de la licencia 011 ya implementada (`src/*/Licensing`).

## 1. Identidad de los módulos y mapeo

- **Decisión**: `enum LicensedModule { Inventory, AdvancedReports, CreditAndCustomers, CashShifts, Returns }` y `ModuleCatalog` estático en Domain con un `Guid` fijo por módulo. Los valores se generan una sola vez al implementar (GUID v4 aleatorios) y se entregan al proveedor para su herramienta de emisión. Un GUID que no está en el catálogo se ignora.
- **Justificación**: los identificadores del texto de la descripción no son GUID válidos (spec, Supuestos), así que los definitivos se fijan en el catálogo. Mantenerlos en código cumple FR-007 (ni visibles ni configurables).
- **Alternativas**: identificadores en configuración o base de datos (rechazada: editables, viola FR-007); usar el nombre del módulo en el archivo (rechazada: el cliente deduciría qué activa cada entrada).

## 2. Qué funcionalidad pertenece a cada módulo (código existente)

| Módulo | Permisos existentes | Estado en el código |
|---|---|---|
| Inventario | `ViewInventory`, `RegisterMovements` | Existe (existencias, movimientos, alertas, reporte de inventario) |
| Reportes avanzados | `ViewReports` | Existe (reporte de ventas, arqueo, alertas de Inicio) |
| Turnos y arqueo | `OperateShift`, `WithdrawCash`, `ManageShifts` | Existe (008); incluye "Mi turno" |
| Crédito y clientes | — | **No existe aún** |
| Devoluciones | — | **No existe aún** |

- **Decisión**: `ModuleAccess.Required(Permission)` devuelve el módulo de cada permiso de la tabla; `null` para el resto (`Sell`, `OpenDrawerWithoutSale`, `ViewProducts`, `ManageProducts`, `ViewOwnSales`, `ViewAllSales`, `CancelSales`, `ManageUsers`, `ViewAuditLog`, `ManageSettings`, `ExportDiagnostics`, `ManageLicense`). Crédito y Devoluciones quedan definidos en el catálogo y licenciables, pero sin nada que bloquear hasta que existan; las funcionalidades futuras deben declarar su módulo en `ModuleAccess`.
- **Justificación**: un único mapa evita que menú, `AccessControl` y casos de uso diverjan. `CancelSales` no se asigna a Devoluciones: cancelar una venta es una función básica existente y FR-021 exige que lo básico no se bloquee.
- **Alternativas**: atributo por caso de uso (más disperso, fácil de olvidar); gating solo en la UI (rechazada: FR-013 exige rechazo de operaciones, incluido el acceso directo).

## 3. Copia protegida de la fecha de inicio (FR-018, FR-019, SC-007)

- **Decisión**: tabla `LicenseSeals` con una fila: carga binaria cifrada con AES-256-GCM (clave HKDF del ID de máquina con un secreto de aplicación y un contexto distinto al del archivo) que contiene `FirstRunUtc` y `LastSeenUtc`. Al arrancar se concilia con el archivo: la fecha de inicio vigente es la **más antigua** de las disponibles y la última vista, la **más reciente**.
- **Por qué también `LastSeenUtc`**: la aclaración 1 pide la fecha de inicio en la copia; sin la última fecha vista, borrar el archivo y atrasar el reloj devolvería días (el archivo regenerado nacería con `LastSeen = ahora`), incumpliendo SC-007 y FR-019. El costo es un campo en la misma carga cifrada.
- **Casos**:
  - Archivo presente y copia ausente o ilegible → se reescribe la copia desde el archivo.
  - Archivo ausente, inválido, alterado o de otra máquina y copia válida → se regenera el archivo sin módulos comprados con la fecha de la copia; se muestra el mensaje y la evaluación sigue.
  - Ambos ausentes (base nueva) → primer arranque (FR-001), igual que en 011 se toma como inicio el menor entre ahora y la creación del primer usuario real (`IInstallationAgeReader`).
- **Alternativas**: valor en `BusinessProfile` (mezcla datos de negocio con licencia); archivo oculto adicional (no sobrevive a copiar la carpeta de datos en el mismo sentido que la base, y la aclaración pide la base de datos); sin cifrar (editable, rechazada).
- **Límite aceptado**: borrar el archivo **y** la base a la vez reinicia la prueba (la spec lo declara primer arranque). Las **compras se pierden** al regenerar el archivo sin módulos; se recuperan reimportando la licencia extendida, por eso el mensaje lo indica.

## 4. Archivo de licencia v2

- **Decisión**: se conserva el contenedor de 011 (marca `POSL`, versión, huella del ID de máquina, nonce, etiqueta, AES-256-GCM) con versión `2` y contenido `{ machineId, firstRunUtc, lastSeenUtc, trialDays, modules[] }`. La versión `1` solo se lee para migrar (§6). Un archivo de otra máquina no se puede descifrar (la clave deriva del ID), así que al arrancar se trata como inutilizable igual que uno editado: se regenera como en §3.
- **Justificación**: reutiliza código probado y cumple FR-004/FR-005. El escenario 3 de la historia 2 ("se rechaza por ID de máquina distinto") se cumple porque ese archivo no habilita nada; el único camino a módulos es importar una licencia válida para esta máquina.
- **`trialDays` en el archivo**: se guarda por FR-004 (valor 30). Es tan manipulable como el resto del contenido solo con el secreto de aplicación; se acepta el mismo nivel de protección que en 011.

## 5. Licencia extendida formato 2

- **Decisión**: JSON firmado con ECDSA P-256 (clave pública ya embebida): `{"format":2,"machineId","issuedUtc","modules":[guid,…]}`. Contenido canónico: campos en orden fijo y GUID en minúscula formato `D` ordenados ascendentemente (el emisor debe ordenar; el verificador reordena antes de verificar para ser tolerante). El formato `1` (con `validUntil`) se rechaza como no legible.
- **Importación**: une los GUID conocidos con los ya habilitados (idempotente), no modifica `FirstRunUtc` ni `LastSeenUtc` salvo avanzar la última vista, guarda el archivo y notifica a `ILicenseState`. Se elimina la regla de 011 "licencia más antigua que la vigente": al sumar módulos el orden no importa.
- **Alternativas**: un archivo por módulo (más fricción para el cliente); reutilizar formato 1 con campo extra (cambia la firma ya publicada y mezcla semánticas).

## 6. Migración de instalaciones con licencia 011 (FR-022)

- **Decisión**: al leer un `license.lic` versión 1:
  - Con concesión activa (sin vencimiento, o con `ValidUntil` aún no pasado según el reloj y la última vista) → se escribe v2 con **los 5 GUID**, conservando `FirstRunUtc`.
  - Sin concesión o con concesión vencida (incluye evaluación 011 en curso o vencida) → se escribe v2 con evaluación nueva: `FirstRunUtc = ahora` y sin módulos comprados, y se siembra la copia protegida.
- **Justificación**: es exactamente la aclaración 3. Que una concesión con fecha de fin pase a ser perpetua es una generosidad aceptada y acotada (las licencias 011 no se vendían por módulo); la alternativa de conservar el vencimiento exigiría mantener `ValidUntil` en el modelo.
- **Nota**: una instalación 011 con evaluación vencida recibe 30 días nuevos al actualizar; así lo define la aclaración 3.

## 7. Bloqueo y operaciones entre módulos (FR-012, FR-013, FR-021)

- **Decisión**:
  - `AccessControl.CheckAsync` consulta `ModuleAccess.Required(permiso)` y, si el módulo no está activo, devuelve `ModuleNotLicensed` **antes** de revisar roles y sin tocar datos; registra el rechazo sin datos sensibles. `HasAsync` (decide qué mostrar) también devuelve falso.
  - **Turnos**: `ConfirmSale` omite `RequireOwnOpenShiftAsync` si Turnos está inactivo y registra la venta con `CashShiftId = null` (la columna ya es nula en `Sale`). `CancelSale` omite la regla "solo en el turno de la venta" para el mismo caso.
  - **Inventario**: `ConfirmSale` no consulta existencias, no valida stock ni genera movimientos si Inventario está inactivo; `CancelSale` no repone existencias.
  - Un turno abierto al vencer la evaluación se conserva; no se pueden iniciar nuevas acciones del módulo, pero los datos persisten (cierre de turno incluido en el bloqueo, consistente con FR-013).
- **Riesgo documentado**: inventario desfasado mientras Inventario esté bloqueado; al reactivar no se reconcilia automáticamente (fuera de alcance, aclaración 4).
- **Alternativas**: bloquear la venta si falta Turnos (rechazada: viola FR-021); mover el chequeo a cada handler (rechazada: se pierde el punto único; solo las dos omisiones entre módulos se hacen en handlers).

## 8. Menú y activación inmediata (FR-012, FR-017)

- **Decisión**: `ILicenseState` expone `Changed` (se dispara al importar). `MenuViewModel` oculta las entradas cuyo `Permission` pertenece a un módulo inactivo (reusa `ModuleAccess`, sin campo nuevo en `NavigationEntry`), omite los grupos que se quedan sin hijos y se reconstruye con `Changed`. `Navigator` rechaza el acceso directo con el mensaje estándar. Las tarjetas de Inicio y alertas que consultan Reportes/Inventario deben tolerar `ModuleNotLicensed` ocultándose sin mostrar error.
- **Justificación**: la activación sin reinicio ya era necesaria en 011; aquí se amplía a "menú se actualiza".
- **Alternativas**: reiniciar la app tras importar (viola FR-017).

## 9. Avisos (FR-015, FR-016, SC-006)

- **Decisión**: `LicenseWarning.Near` solo cuando quedan **exactamente 5** días y `Urgent` cuando queda **exactamente 1**; ningún otro valor muestra aviso. Textos fijos de la spec. Se muestran en la tarjeta de Inicio al abrir el sistema; se retira el aviso del login de 011.
- **Justificación**: SC-006 dice "exactamente con 5 días y con 1 día". Se acepta que quien no abra la aplicación esos días no vea el aviso. Ampliar a `≤ 5` es un cambio de una línea en el evaluador si el responsable lo prefiere.
- **Conteo de días**: por fecha de calendario local, como en 011: el día 1 tiene 30 restantes, el día 30 tiene 1, el día 31 tiene 0 (modo modular).

## 10. Restos de 011 que se retiran

`LicenseKind.Expired/Invalid`, `LicenseStatus.IsReadOnly`, `LicenseGrant.ValidUntil`, `ILicenseState.IsBlocked`, el error `LicenseExpired`, el motivo de rechazo `Older`, la verificación explícita de `OpenShiftHandler`, el manejo de `LicenseExpired` en el Punto de venta, la pantalla segura "Inicio" por modo lectura en `Navigator` y el aviso de modo lectura en el login. Los textos de recursos se reemplazan por los de esta funcionalidad.
