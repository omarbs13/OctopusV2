# Contrato interno: modo bloqueado y pantalla de licencia

## 1. Operaciones en bloqueo (`LicenseStatus.IsBlocked`)

| Operación | Disponible | Control |
|-----------|-----------|---------|
| Iniciar y cerrar sesión | Sí | Sin permiso de licencia. |
| Ver "Ayuda > Licencia" (estado, ID de máquina, Copiar) | Sí, cualquier usuario | `GetLicenseStatus` no exige permiso. |
| Generar solicitud | Sí, cualquier usuario | `ExportLicenseRequest` usa `AccessControl.CheckSessionAsync` (solo sesión): la solicitud no es secreta (FR-014). Igual fuera de bloqueo. |
| Importar licencia | Sí, Administrador | `ManageLicense` (exento). |
| Exportar respaldo | Sí, Administrador | `ExportBackup` (exento). El respaldo contiene todos los datos del negocio, así que queda protegido por rol (Principio IX); la garantía del Principio I es que la función siga disponible, y siempre existe al menos un Administrador activo (`CreateFirstAdmin` lo crea en el primer arranque y `UpdateUser` impide quitar el último). |
| Seguir, cobrar o descartar la venta en curso | Sí, solo el borrador guardado con líneas | `SaveSaleDraft`/`ConfirmSale` comparan `DraftId` con el borrador guardado; `CheckToFinishAsync`. |
| Imprimir el ticket de la venta recién cobrada | Sí, solo impresión original (no reimpresión) de una venta propia | `PrintTicket` con `SaleSource` e `IsReprint = false`; `CheckToFinishAsync(ViewOwnSales)`. |
| Consultar el borrador y buscar productos para él | Sí | `GetSaleDraft`, `ReviewSale`, `FindProductsForSale`, `InspectScan`, `GetCreditNoteBalance` (solo lectura, `Sell` exento). |
| Apoyos de descuento y crédito para la venta en curso | Sí, con la regla de módulo | `ResolveCoupon`, `ApproveDiscount`, `GetDiscountSettings` (exigen Descuentos activo) y `FindCustomersForSale`, `GetCustomerCreditStatus` (exigen Crédito y clientes activo). Usan `CheckAsync` con permisos exentos. Lo que producen solo tiene efecto al guardarse en el borrador de la venta en curso, que `SaveSaleDraft`/`ConfirmSale` controlan por `DraftId`; por eso no permiten iniciar una venta nueva. |
| Iniciar una venta nueva | No | `SystemNotActivated`. |
| Contar efectivo y cerrar el turno abierto | Sí, **aunque Turnos y arqueo no esté activo** | `GetCurrentShift`, `GetShiftDetail` del turno abierto, `CountShiftCash` y `CloseShift` usan `CheckToFinishAsync`; la impresión original del corte del turno recién cerrado (`PrintTicket` con `ShiftCutSource`, `IsReprint = false`) también. |
| Abrir turno, entradas y retiros de efectivo | No | `OpenShift`, `RegisterCashMovement` rechazan. |
| Abrir el cajón sin venta | No | `OpenCashDrawer` rechaza en bloqueo (el cajón se abre solo como parte del cobro). |
| Consultas e impresiones de turnos y cortes anteriores | No | `SearchShifts`, `SearchShiftCuts`, `GetShiftCut`, `GetShiftDetail` de turnos cerrados, `ListMyShifts`, `GetMyShiftSummary` y reimpresiones rechazan con `SystemNotActivated`. |
| Cualquier otra operación | No | `AccessControl` → `SystemNotActivated`. |

**Terminar trabajo ya iniciado** (`AccessControl.CheckToFinishAsync(permission)`): exige sesión y
rol, pero no aplica la regla de bloqueo ni la regla de módulo. Se usa solo para la venta del
borrador guardado, el ticket original de esa venta, la consulta, el conteo y el cierre del turno
abierto y la impresión original de su corte (FR-030a, Principio I). Aplica **siempre**, no solo en
bloqueo: también cuando POS sigue activo y vence otro módulo que participa en la venta o en el
turno.

**Partes ya capturadas**: son las que existen en el borrador guardado en `ISaleDraftStore`
(líneas con su descuento, descuento global, cliente y forma de pago a crédito). Al guardar o
confirmar, el caso de uso compara el comando con ese borrador: lo que coincide se autoriza con
`CheckToFinishAsync`; lo que el comando agrega o cambia (un descuento nuevo o distinto, cambiar a
crédito, otro cliente) se autoriza con `CheckAsync` y sigue la regla de módulo. Así, las partes de otros módulos **ya capturadas** en la venta en curso
(descuentos aplicados, venta a crédito con cliente asignado) se respetan al cobrar aunque su
módulo haya vencido. Agregar partes nuevas de un módulo no activo (aplicar otro descuento,
cambiar a crédito) sigue la regla de módulo y se rechaza (caso límite "Operación en curso al
cambiar el día"). FR-036 aplica a las operaciones nuevas, no al cierre de la venta en curso.

## 2. Menú en bloqueo

- Siempre: "Inicio" (página de entrada en bloqueo; muestra el mensaje de activación con los
  pasos de su causa, incluida la licencia no válida, y el aviso de reloj atrasado) y "Ayuda > Licencia".
- "Punto de venta": solo mientras el usuario tenga un borrador con líneas.
- "Turno" (páginas de arqueo y cierre de caja): solo mientras haya un turno abierto; se muestran
  sin la regla de módulo (aunque Turnos y arqueo no esté activo), igual que fuera de bloqueo
  mientras exista un turno abierto con el módulo vencido.
- El menú se reconstruye con `ILicenseState.Changed` (sin reiniciar).

## 3. Mensajes al operador

| Situación | Mensaje |
|-----------|---------|
| Bloqueo por prueba vencida | "El periodo de prueba terminó. Para seguir vendiendo, genera una solicitud en Ayuda > Licencia, envíala a tu proveedor e importa la licencia que te entregue." |
| Bloqueo por licencia no válida (la licencia guardada no supera la verificación, o ya se había importado una y falta) | "La licencia guardada no es válida. Para seguir vendiendo, importa de nuevo la licencia que te entregó tu proveedor en Ayuda > Licencia o genera una solicitud nueva." |
| Bloqueo por POS no contratado | "Tu licencia no incluye el módulo Punto de venta. …(mismos pasos)" |
| Bloqueo por POS vencido | "El módulo Punto de venta venció el {fecha}. …(mismos pasos)" |
| Bloqueo por POS pendiente | "El módulo Punto de venta se activa el {fecha}. …" |
| Operación rechazada en bloqueo | "El sistema no está activado. Abre Ayuda > Licencia para activarlo." |
| Módulo no activo (existente, 012) | "Este módulo no está activo en tu licencia." |
| Venta en curso al bloquear | "La licencia venció. Puedes terminar y cobrar esta venta y cerrar el turno; no se pueden iniciar ventas nuevas." |
| Reloj atrasado | "La fecha del equipo es anterior a la última fecha registrada ({fecha}). Corrige la fecha y la hora del equipo." |
| Módulo por vencer (Inicio) | "{Módulo} vence el {fecha}." (uno por módulo, ≤ 7 días) |
| Prueba: 5 días / 1 día | Textos existentes de 012 (`License_NearExpiry`, `License_NearExpiryOne`). |

### Rechazos de importación

| `LicenseImportRejection` | Mensaje |
|--------------------------|---------|
| `Unreadable` | "El archivo no es una licencia válida o está dañado." |
| `UnsupportedFormat` | "El formato de esta licencia ya no es compatible. Pide a tu proveedor una licencia nueva." |
| `BadSignature` | "La licencia no es auténtica: no fue emitida por tu proveedor o fue modificada." |
| `OtherMachine` | "Esta licencia pertenece a otro equipo." |
| `NotNewer` | "Esta licencia es más antigua que la vigente." |

En todos los casos: "Se conserva la licencia actual."

### Importación aceptada

| Resultado | Mensaje |
|-----------|---------|
| Aceptada y el sistema queda activo | "Licencia importada. Los módulos se aplicaron." |
| Aceptada pero el sistema queda bloqueado (licencia ya caducada por completo, sin POS o con POS pendiente) | "Licencia importada, pero el sistema sigue bloqueado: {causa}." ({causa} = texto de la causa de bloqueo de la tabla anterior) |

## 4. Pantalla "Ayuda > Licencia"

Nueva página `help.license` en el grupo Ayuda (orden 0; "Acerca de" pasa a orden 1). Sin permiso
de navegación (siempre visible, también en bloqueo).

- **Encabezado**: ID de máquina + botón "Copiar"; estado general ("En prueba: N días restantes",
  "Licenciado", "Bloqueado: {causa}"); cliente (si hay licencia).
- **Avisos**: reloj atrasado, prueba por vencer. Una licencia guardada rechazada no lleva un aviso
  aparte: siempre produce el bloqueo `LicenseInvalid`, cuyo mensaje ya lo explica.
- **Tabla de módulos** (9 filas, orden del catálogo): Módulo | Estado (Activo, Pendiente de
  activación, Vencido, No contratado) | Activación | Vencimiento ("Indefinido" si no tiene).
- **Acciones**: "Generar solicitud" (guarda `.octoreq`), "Importar licencia" (elige `.lic`),
  "Exportar respaldo". "Generar solicitud" está disponible para cualquier usuario con sesión;
  "Importar licencia" requiere `ManageLicense` y "Exportar respaldo", `ExportBackup`. Sin el
  permiso, el botón no aparece.
- "Acerca de" deja de importar licencias y de generar solicitudes; conserva el ID de máquina y
  enlaza a "Ayuda > Licencia".
