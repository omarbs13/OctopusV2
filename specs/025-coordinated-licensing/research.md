# Research: Licenciamiento coordinado con OctopusAdmin

**Feature**: 025-coordinated-licensing | **Fecha**: 2026-10-03

Punto de partida: el licenciamiento de 011/012 (`src/*/Licensing/`). Hoy la licencia importada
(formato 2) solo se usa para sumar módulos a un archivo local `license.lic` cifrado con AES-GCM;
la firma original no se guarda. El estado se calcula en `LicenseEvaluator` (fase Prueba/Modular)
y se aplica en `AccessControl`, `NavigationRegistry`, `Navigator` y en algunos casos de uso.

---

## §1. Bytes firmados: contenido en Base64 dentro de un sobre

- **Decision**: el `.lic` v3 es un sobre JSON `{format, payload, signature}`. `payload` es el
  Base64 de los bytes UTF-8 del contenido JSON; la firma ECDSA P-256/SHA-256 (P1363) se calcula
  sobre esos bytes decodificados. El POS verifica sin reserializar y luego lee el JSON.
- **Rationale**: elimina la canonicalización, que es la fuente típica de firmas que no coinciden
  entre dos implementaciones (orden de campos, escapes, formato de fechas, espacios). Guardar la
  licencia "tal como se recibió" es trivial y la reverificación en cada arranque usa los mismos
  bytes. Contrato: `contracts/license-format.md`.
- **Alternatives considered**: (a) canonicalización propia como la de formato 2
  (`LicenseCanonical`): frágil y obliga a OctopusAdmin a replicar el código exacto; (b) JWS/JWT:
  agrega una dependencia y reglas (alg, header) que no aportan; (c) firmar el JSON "tal cual" en
  el archivo: cualquier editor o reformateo rompe la firma.

## §2. Firma en formato IEEE P1363

- **Decision**: firma de 64 bytes `r‖s`; no se acepta DER.
- **Rationale**: es el formato por omisión de `ECDsa.SignData/VerifyData` en .NET, que usan
  ambas aplicaciones; el formato 2 ya lo usaba. Un solo formato evita ambigüedad.
- **Alternatives considered**: aceptar ambos formatos (DER y P1363): más código y casos de
  prueba sin necesidad real.

## §3. Dónde se guarda la licencia firmada

- **Decision**: tabla técnica nueva `InstalledLicenses` en la base SQLite, con una sola fila:
  `Content` (el texto completo del `.lic` tal como se importó) e `ImportedAtUtc`. Al arrancar
  se reverifica el contenido (pasos 1 a 6 del contrato); si falla, la licencia no habilita
  ningún módulo, la prueba no se reanuda (`Blocked/LicenseInvalid`, §4), se avisa en Inicio y se
  registra en la bitácora. Una importación válida
  reemplaza la fila en una sola transacción.
- **Rationale**: la especificación pide que editar la base no permita agregar módulos (criterio
  4): la firma del proveedor protege el contenido sin depender de ningún secreto local. Al vivir
  en la base, entra en los respaldos automáticos y en la exportación de respaldo. Como el
  `LicenseSealEntity`, es un dato técnico de la instalación: sin auditoría, borrado lógico ni
  versión (justificado en el Constitution Check).
- **Alternatives considered**: (a) seguir derivando los módulos al `license.lic` cifrado: depende
  del secreto AES embebido y no cumple "guardar tal como se recibió"; (b) archivo `.lic` suelto en
  la carpeta de datos: se pierde con la base restaurada y no queda en el respaldo.

## §4. Registro de la prueba (fecha de inicio y última fecha vista)

- **Decision**: se conserva el mecanismo de 012 (archivo `license.lic` + copia protegida
  `LicenseSeals` en la base, reconciliados al arrancar), pero el archivo deja de llevar módulos:
  solo `MachineId`, `FirstRunUtc`, `LastSeenUtc`, `TrialDays` y `LicenseImportedUtc`. Este
  último se fija al aceptar la primera licencia, nunca vuelve a nulo y termina la prueba para
  siempre (FR-026a). Sin él, alterar o borrar `InstalledLicenses` devolvería la prueba con los
  9 módulos a un cliente que contrató menos. Si un corte deja una licencia guardada válida sin
  `LicenseImportedUtc`, el bootstrapper lo fija en el siguiente arranque. El riesgo residual es borrar a la vez el archivo, el
  sello y la licencia; queda acotado por `IInstallationAgeReader` a lo que reste de los 30 días.
  Se escribe el archivo de prueba v3. Al leer un archivo de prueba v2 se conservan sus fechas y se
  descartan sus módulos; si tenía alguno (licencia de 011/012), se fija `LicenseImportedUtc` y la
  licencia guardada se trata como rechazada (`Blocked/LicenseInvalid` con aviso, FR-021), en vez
  de perderla sin avisar. Regla de alteración
  (FR-025): si la fila de `LicenseSeals` existe pero no se puede descifrar o validar, la prueba
  se considera vencida (`FirstRunUtc` = mínimo representable); si solo el archivo está alterado,
  se recupera desde la copia (comportamiento actual). Si faltan los dos, se usa la evidencia de
  `IInstallationAgeReader` (comportamiento actual).
- **Rationale**: la protección de la prueba es necesariamente local (no hay proveedor en el
  primer arranque). La clave AES derivada del ID de máquina con una constante de la aplicación
  no es un secreto real: cualquiera con el código puede reproducirla. Su objetivo es detectar
  ediciones casuales con un editor de SQLite, no resistir a un atacante con el código fuente. Se
  documenta así y la constante se renombra de `AppSecret` a `KeyContext` para no presentarla como
  secreto (Principio IX). La licencia comercial, que sí protege ingresos, depende solo de la
  firma del proveedor.
- **Alternatives considered**: (a) prueba firmada por OctopusAdmin: imposible sin conexión en el
  primer arranque; (b) eliminar `license.lic` y dejar solo la copia en la base: borrar una fila
  bastaría para reiniciar la prueba si además se borran los usuarios; el archivo duplicado no
  cuesta y ya existe.

## §5. Catálogo compartido embebido y prueba de consistencia

- **Decision**: `contracts/module-catalog.json` (raíz del repositorio) se incluye en
  `Pos.Infrastructure` como `EmbeddedResource` enlazado (`Include="..\..\contracts\module-catalog.json"`,
  `LogicalName="Pos.ModuleCatalog.json"`). `Pos.Domain.Licensing.ModuleCatalog` sigue siendo la
  fuente en tiempo de ejecución para GUID, clave, orden y módulo base (constantes). Un adaptador
  `EmbeddedModuleCatalogInfo : IModuleCatalogInfo` expone la versión del catálogo, nombres y
  descripciones para la solicitud y la pantalla de licencia. Una prueba en
  `Pos.Infrastructure.Tests` lee el recurso embebido y exige igualdad exacta con `ModuleCatalog`
  (cantidad, GUID, clave, orden, módulo base) y que los 6 GUID históricos no cambien.
- **Rationale**: Domain no lee archivos ni JSON (Principio II). El recurso embebido no puede
  faltar ni editarse en la instalación. La prueba hace imposible que el código y el contrato
  diverjan sin que falle la compilación de CI.
- **Alternatives considered**: (a) generar `ModuleCatalog.cs` desde el JSON con un generador de
  código: más maquinaria que una prueba; (b) leer el JSON en tiempo de ejecución como única
  fuente: Domain dependería de un recurso de Infrastructure.

## §6. GUID nuevos

- **Decision**: POS `4c2517f5-096a-460e-8ee7-b09e46572952`, Proveedores
  `1e6111ca-514b-497e-b553-154960c946f3`, Categorías `7b4ae9e2-6e3f-4006-b259-b7d4dcd06933`,
  generados una vez el 2026-10-03 y fijados en el contrato. Los 6 existentes no cambian.
- **Rationale**: FR-003. Se generaron como GUID aleatorios (v4) porque son identificadores de
  contrato, no entidades de base (el Principio IV aplica a entidades de negocio).

## §7. Estado de licencia: un solo evaluador puro en Domain

- **Decision**: `LicenseEvaluator.Evaluate(trial, license?, today, lastSeenDate)` devuelve un
  `LicenseStatus` con: estado general (`Trial`, `Licensed`, `Blocked`), causa de bloqueo
  (`TrialExpired`, `LicenseInvalid`, `BaseNotLicensed`, `BasePending`, `BaseExpired`), días de prueba restantes,
  aviso de prueba (5 y 1 día), estado de cada uno de los 9 módulos (`Active`, `Pending`,
  `Expired`, `NotLicensed`) con sus fechas, módulos que vencen en ≤ 7 días y bandera de reloj
  atrasado. Reglas:
  - Licencia válida presente → manda la licencia (la prueba deja de aplicar, aunque le queden
    días).
  - Sin licencia válida y con licencia guardada rechazada o ya licenciado alguna vez
    (`LicenseImportedUtc`) → `Blocked/LicenseInvalid`. La licencia guardada rechazada basta por sí
    sola, porque un cambio de hardware deja ilegible el sello y con él `LicenseImportedUtc`.
  - Sin licencia válida y nunca licenciado → prueba: todos activos hasta el día 30; después,
    `Blocked/TrialExpired`.
  - Una entrada con `expiresOn < activatesOn` cuenta como `Expired`, nunca como `Pending`.
  - Base inactiva → `Blocked`, aunque haya otros módulos activos.
- **Rationale**: todas las reglas de fecha son puras y se prueban sin infraestructura (política de
  pruebas: reglas de negocio con cálculos). `LicenseState` (Application) solo aporta el reloj y
  la zona horaria.
- **Alternatives considered**: mantener la fase `Modular` y añadir el bloqueo aparte: dos fuentes
  de verdad para "¿está activo X?".

## §8. Reloj atrasado

- **Decision**: con `today` = fecha local actual y `seen` = fecha local de la última fecha vista:
  si `today ≥ seen`, se evalúa con `today`. Si `today < seen`, el conjunto de módulos activos es la
  **intersección** de los evaluados en `today` y en `seen`, y los días de prueba se calculan con
  `seen`. La bandera `ClockBehind` (aviso al operador) se activa solo si `seen − today > 1 día`.
  La última fecha vista nunca retrocede y se guarda al arrancar, al iniciar sesión y en cada
  revisión periódica.
- **Rationale**: FR-038/FR-039: ningún atraso reactiva un módulo vencido (en `seen` ya está
  vencido) ni activa uno que en `today` aún no se activa (lo más restrictivo). El margen de 1 día
  evita avisos por cambios de zona horaria o ajustes menores, sin abrir una puerta: el estado igual
  usa la intersección.
- **Alternatives considered**: usar siempre `max(today, seen)`: reactivaría módulos cuya
  activación es futura respecto del reloj real pero pasada respecto de `seen`, lo contrario de "lo
  más restrictivo".

## §9. Recalcular al cambiar de día

- **Decision**: `LicenseClockScheduler` programa el siguiente disparo en la próxima medianoche
  local + 5 s y además cada hora; cada disparo llama a `LicenseBootstrapper.TouchAsync`, que
  guarda la última fecha vista y llama a `ILicenseState.Refresh`. `Refresh` compara una huella del
  estado completo (estado general + estado de cada módulo + avisos), no solo la fase, y dispara
  `Changed` si cambió; el menú se reconstruye con ese evento (mecanismo de 012). Al iniciar sesión
  la Shell también llama a `TouchAsync`.
- **Rationale**: FR-034. `ILicenseState.Current` ya se recalcula con el reloj en cada lectura, así
  que los casos de uso aplican el cambio de día al instante; el temporizador solo actualiza menú y
  avisos.

## §10. Bloqueo total y garantías del Principio I

- **Decision**:
  1. `AccessControl.CheckAsync/HasAsync`: si `Current.IsBlocked`, se rechaza con el error nuevo
     `SystemNotActivated` todo permiso que no esté en `LicenseLock.ExemptPermissions`:
     `ManageLicense`, `ExportBackup`, y los de terminar una venta o cerrar un turno: `Sell`,
     `ApplyDiscounts`, `ApproveDiscounts`, `SellOnCredit`, `ApproveCreditOverLimit`,
     `OperateShift`, `ManageShifts`. La regla de módulo sigue aplicando encima (p. ej., vender a
     crédito exige además Crédito y clientes activo).
  2. "Venta en curso" = el borrador durable del usuario (`ISaleDraftStore`), que el punto de venta
     guarda tras cada cambio. En bloqueo, `SaveSaleDraftHandler` y `ConfirmSaleHandler` solo
     aceptan el `DraftId` del borrador ya guardado y con líneas; un `DraftId` distinto es una
     venta nueva y se rechaza con `SystemNotActivated`. `DiscardSaleDraft` siempre se permite.
  3. "Cerrar el turno abierto": en bloqueo, `OpenShiftHandler` y `RegisterCashMovementHandler`
     rechazan; `CountShiftCash`, `CloseShift` y las consultas del turno abierto se permiten.
     También se rechazan en bloqueo `OpenCashDrawer` sin venta y las consultas e impresiones de
     turnos y cortes anteriores, aunque usen permisos exentos (SC-005).
  4. **Terminar trabajo ya iniciado**: `AccessControl.CheckToFinishAsync(permission)` exige
     sesión y rol, pero omite la regla de bloqueo y la de módulo. La usan solo `SaveSaleDraft` y
     `ConfirmSale` con las partes ya capturadas del borrador guardado, `PrintTicket` en la
     impresión original de esa venta (`ViewOwnSales` no es exento) y del corte del turno recién
     cerrado, y `GetCurrentShift`, `GetShiftDetail`, `CountShiftCash`/`CloseShift` del turno
     abierto; las páginas de arqueo y cierre se muestran mientras haya turno abierto. Aplica con o
     sin bloqueo. Sin esto, un
     turno abierto no podría cerrarse si Turnos y arqueo vence junto con POS, y una venta a
     crédito o con descuento ya capturada no podría cobrarse si su módulo vence (FR-030a). Lo
     nuevo (otro descuento, cambiar a crédito) sigue la regla de módulo.
  5. Interfaz: en bloqueo el menú solo muestra "Inicio" (página de entrada, con el mensaje de
     activación y los pasos) y "Ayuda > Licencia", y además "Punto de venta" mientras exista
     borrador con líneas y "Turno" mientras haya un turno abierto.
- **Rationale**: el control vive en Application (Principio III y regla "se verifica en casos de
  uso"); la constitución v1.3.0 exige exactamente estas dos garantías. Usar el borrador durable
  cubre también el reinicio con venta abierta.
- **Alternatives considered**: (a) bloquear todo menos licencia y respaldo: viola el Principio I
  v1.3.0; (b) una "ventana de gracia" por tiempo: arbitraria y no garantiza terminar la venta.

## §11. Exportación de respaldo

- **Decision**: caso de uso nuevo `ExportBackupHandler` (en
  `Pos.Application/Backup/ExportBackup`) que usa `IBackupService.CreateTemporaryCopyAsync` y mueve la
  copia al destino elegido. Permiso nuevo `ExportBackup` (solo Administrador), exento de la
  licencia y sin módulo. Botón "Exportar respaldo" en "Ayuda > Licencia" y en "Acerca de".
- **Rationale**: hoy solo existe la base dentro del diagnóstico (`ExportDiagnostics` con
  `IncludeDatabase`), que mezcla soporte con propiedad de datos. El Principio VIII ya pide
  "exportar un respaldo de la base".
- **Alternatives considered**: reutilizar `ExportDiagnostics`: genera un ZIP con logs y depende de
  un permiso de soporte.

## §12. Proveedores y Categorías como módulos

- **Decision**: `ModuleAccess.Required` pasa a `ModuleAccess.RequiredModules(permission)` (puede
  exigir más de uno):
  - `ManageSuppliers`, `ViewPurchaseReport` → Proveedores.
  - `RegisterPurchases`, `VoidPurchases` → Proveedores **e** Inventario (una compra es una entrada
    de mercancía; sin Inventario no tiene sentido).
  - Permiso nuevo `ManageCategories` (mismos roles que `ManageProducts`) → Categorías; los casos de
    uso de Categorías dejan `ManageProducts` y usan `ManageCategories`.
  - Con Categorías inactivo: `ListCategoryOptions` devuelve lista vacía, el formulario de producto
    oculta el campo y `UpdateProduct` conserva la categoría actual del producto (no la borra);
    los filtros por categoría se ocultan. Los datos de categorías se conservan.
- **Rationale**: FR-007 con el mismo mecanismo que los demás módulos; FR-033 (datos conservados).

## §13. Solicitud `.octoreq`

- **Decision**: `ExportLicenseRequestHandler` pasa a `requestFormat` 2 con `businessName` (de
  `IBusinessProfileRepository`, vacío si no hay) y `catalogVersion` (de `IModuleCatalogInfo`).
  Contrato: `contracts/license-request.md`. El diálogo propone la extensión `.octoreq`. Deja de
  exigir `ManageLicense` y solo pide sesión (`AccessControl.CheckSessionAsync`, nuevo): la solicitud no es secreta (FR-014), y así cualquier
  operador puede pedir la activación en bloqueo. Importar sigue exigiendo `ManageLicense`.

## §14. Formato 2 retirado

- **Decision**: el verificador solo acepta `format: 3`; `format: 2` devuelve
  `LicenseImportRejection.UnsupportedFormat`. `LicenseCanonical` y `ExtendedGrant` se eliminan.
  Una instalación con módulos derivados de formato 2 o de la migración 011 en `license.lic` los
  pierde al actualizar (decisión de clarificación: no hay clientes con ellas). Si aun así existe
  alguna, el operador ve el bloqueo por licencia no válida, con los pasos para pedir una licencia formato 3 (§4).
- **Rationale**: decisión del responsable (spec, Clarifications 2026-10-03).

## §15. Clave pública de producción

- **Decision**: `EcdsaLicenseVerifier.ProductionPublicKey` se reemplaza por la clave pública que
  entregue OctopusAdmin (SubjectPublicKeyInfo, Base64). Se mantiene la variable de entorno
  `POS_LICENSE_DEV_PUBLIC_KEY` solo en compilaciones DEBUG. Las pruebas usan pares de claves
  generados en memoria (`TestLicenseIssuer`) y el vector del contrato con su clave de prueba.
- **Dependency**: la clave real de OctopusAdmin; sin ella no se puede liberar a producción (no
  bloquea la implementación ni las pruebas).

## §16. Dependencias externas nuevas

Ninguna. Todo usa la BCL (`System.Security.Cryptography`, `System.Text.Json`) y las dependencias
ya aprobadas.
