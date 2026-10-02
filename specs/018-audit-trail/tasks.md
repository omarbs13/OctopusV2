---

description: "Lista de tareas de la funcionalidad 018: auditoría detallada de cambios"
---

# Tasks: Auditoría detallada de cambios

**Input**: Documentos de diseño en `specs/018-audit-trail/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: el plan pide pruebas concretas según la política mínima de la constitución v1.2.0 (research §14). Solo se incluyen esas: validación de `AuditFieldChange`, comparador, exclusión de secretos, autor obligatorio, atomicidad, completitud, lector, exportación, rendimiento (explícita), migración y arquitectura. **No** hay pruebas de ViewModels, vistas, mapeos ni del formato del PDF. Persistencia siempre sobre SQLite real.

**Organization**: tareas agrupadas por historia de usuario. Las tres historias son P2, P2 y P3; se ejecutan en el orden de la spec.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes).
- **[Story]**: historia a la que pertenece (US1, US2, US3).
- Comandos del proyecto: `dotnet build -v q` y `dotnet test --verbosity quiet`. Al implementar, ejecutar solo las pruebas del proyecto modificado (constitución VI).

## Path Conventions

- Capas: `src/Pos.Domain/`, `src/Pos.Application/`, `src/Pos.Infrastructure/`, `src/Pos.Desktop/`.
- Pruebas: `tests/Pos.Domain.Tests/`, `tests/Pos.Application.Tests/`, `tests/Pos.Infrastructure.Tests/`, `tests/Pos.ArchitectureTests/`.
- Código en inglés; textos de interfaz, mensajes y documentación en español.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: preparar la versión. No se agrega ninguna dependencia externa.

- [X] T001 Subir `<Version>` de `0.12.0` a `0.13.0` en Directory.Build.props

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: modelo, puerto ampliado, comparador, persistencia y migración que usan las tres historias.

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase.

### Domain

- [X] T002 [P] Crear el value object `AuditFieldChange` (sealed record con `Field`, `Before`, `After`) en src/Pos.Domain/Audit/AuditFieldChange.cs. Reglas de data-model.md: `Field` "texto ≤ 80 … Obligatorio"; `Before` "texto ≤ 2000, nulo … Nulo en las altas"; `After` "texto ≤ 2000, nulo … Nulo en las eliminaciones". "Si `Before` y `After` son iguales, el cambio no es válido" (lanza `DomainException`). "Un valor de más de 2000 caracteres se recorta con "…"". Exponer constantes `FieldMaxLength = 80` y `ValueMaxLength = 2000`.
- [X] T003 Ampliar `AuditEntry` en src/Pos.Domain/Audit/AuditEntry.cs (depende de T002): propiedades `EntityName` ("texto ≤ 200, nulo … Se recorta a 200 caracteres"), `Reason` ("texto ≤ 250, nulo") y `Changes` (`IReadOnlyList<AuditFieldChange>`, "Nula o vacía en los eventos sin cambios de campo"). `AuditEntry.Create` recibe los tres campos: recorta `EntityName` y `Reason` a su longitud máxima, guarda vacíos como nulos y conserva el orden de `Changes`. `Create` **no** recibe el autor (lo asigna la persistencia). Constantes `EntityNameMaxLength = 200` y `ReasonMaxLength = 250`. Sin `UpdatedAt`, `Version` ni `DeletedAt`.

### Application

- [X] T004 [P] Crear `AuditRecord` (sealed record `Action, EntityType, EntityId, EntityName = null, Details = null, Reason = null, Changes = null, AuthorizedBy = null`, según contracts/application-ports.md) en src/Pos.Application/Audit/AuditRecord.cs
- [X] T005 Agregar la sobrecarga `void Add(AuditRecord record)` a `IAuditLog` en src/Pos.Application/Abstractions/IAuditLog.cs, conservando la firma existente `Add(action, entityType, entityId, details, authorizedBy)` (depende de T004)
- [X] T006 [P] Crear `AuditField(string Field, string? Value)` y la clase estática `AuditChanges` en src/Pos.Application/Audit/AuditChanges.cs, con:
  - `Compare(before, after)`: solo los campos distintos, en el orden de la instantánea.
  - `Created(after)`: todos los campos con `Before` nulo, omitiendo los valores nulos.
  - `Removed(before)`: todos los campos con `After` nulo.
  - `HasChanges`.
  - Si `before` y `after` no tienen los mismos campos en el mismo orden, lanza `ArgumentException`.
- [X] T007 [P] Crear `AuditFormat` con los formateadores comunes de las instantáneas en src/Pos.Application/Audit/AuditFormat.cs: dinero desde el value object de centavos con formato de moneda, Sí/No, Activo/Inactivo, cantidades con los decimales de la unidad y "Sin categoría".
- [X] T008 [P] Actualizar el catálogo en src/Pos.Application/Audit/AuditActions.cs:
  - Agregar los eventos `PRODUCT_CREATED` ("Producto creado"), `PRODUCT_UPDATED` ("Producto modificado"), `PRODUCT_DELETED` ("Producto eliminado"), `SALE_DISCOUNTS_APPLIED` ("Venta con descuento") y `AUDIT_EXPORTED` ("Bitácora exportada"), con sus textos en `All`.
  - Agregar las constantes de entidad `ProductEntity = "Product"`, `AuditLogEntity = "AuditLog"` y `CashDrawerEntity = "CashDrawer"`. Hacer que `OpenCashDrawerHandler.AuditEntityType` use `CashDrawerEntity`, con el mismo valor.
  - Conservar `DISCOUNT_APPLIED_AUTHORIZED` en el catálogo.

### Infrastructure

- [X] T009 Implementar `Add(AuditRecord)` en src/Pos.Infrastructure/Audit/AuditLog.cs y hacer que la firma existente delegue en ella con un `AuditRecord` sin cambios (depende de T003, T005)
- [X] T010 En src/Pos.Infrastructure/Persistence/AuditingInterceptor.cs, al agregar una `AuditEntry` con el usuario actual en `Guid.Empty`, asignar `SystemUser.Id` como `CreatedBy` y escribir una advertencia en Serilog con la acción y la entidad. No rechazar el guardado: el Principio I lo impide (research §4, FR-010).
- [X] T011 Configurar en src/Pos.Infrastructure/Persistence/Configurations/AuditEntryConfiguration.cs (depende de T003):
  - Columnas: `EntityName` con `HasMaxLength(200)` y nula, `Reason` con `HasMaxLength(250)` y nula, `Changes` con `OwnsMany(...).ToJson("Changes")`.
  - Índices: reemplazar `IX_AuditEntries_CreatedBy` por `(CreatedBy, CreatedAt)` y agregar `IX_AuditEntries_AuthorizedBy (AuthorizedBy)`, `IX_AuditEntries_Action_CreatedAt (Action, CreatedAt)` e `IX_AuditEntries_EntityType_CreatedAt (EntityType, CreatedAt)`.
  - Conservar `IX_AuditEntries_CreatedAt` e `IX_AuditEntries_Entity`.
- [X] T012 Generar la migración `AuditTrail` con `dotnet ef migrations add AuditTrail` en src/Pos.Infrastructure/Persistence/Migrations/ (depende de T011). Revisar el SQL generado (`dotnet ef migrations script`): solo `ALTER TABLE AuditEntries ADD COLUMN EntityName/Reason/Changes TEXT NULL` e índices; **ninguna** reconstrucción de tabla ni transformación de datos.
- [ ] T013 Generar la base de ejemplo tests/Pos.Infrastructure.Tests/SampleDatabases/v0.13.0.db con `SampleDatabaseGenerator` según docs/migraciones.md, incluyendo al menos una entrada con `Changes`, `EntityName` y `Reason` (depende de T012)

### Pruebas obligatorias de la fase

- [X] T014 [P] Crear `AuditFieldChangeTests` en tests/Pos.Domain.Tests/Audit/AuditFieldChangeTests.cs: un cambio válido conserva campo, antes y después; un cambio con antes y después iguales lanza `DomainException` (depende de T002)
- [X] T015 [P] Crear `AuditChangesTests` en tests/Pos.Application.Tests/Audit/AuditChangesTests.cs: `Compare` devuelve solo los campos distintos en orden; `Created` devuelve todos con `Before` nulo; dos instantáneas iguales → lista vacía (depende de T006)
- [X] T016 [P] Crear `AuditAuthorTests` sobre SQLite real en tests/Pos.Infrastructure.Tests/Audit/AuditAuthorTests.cs: con un `ICurrentUser` de prueba que devuelve `Guid.Empty`, la entrada guardada tiene `CreatedBy = SystemUser.Id`; con un usuario real, conserva ese usuario (depende de T010)
- [X] T017 [P] Crear `AuditTrailMigrationTests` en tests/Pos.Infrastructure.Tests/SampleDatabases/AuditTrailMigrationTests.cs: migrar una base 0.12.0 conserva todas las entradas con `EntityName`, `Reason` y `Changes` nulos, y crea los cuatro índices (depende de T012)
- [ ] T018 Revisar tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests.cs y agregar `v0.13.0.db` a sus bases y a las verificaciones por versión, siguiendo el patrón de `v0.12.0.db` (depende de T013)
- [X] T019 [P] Agregar la regla de arquitectura en tests/Pos.ArchitectureTests/AuditImmutabilityTests.cs (research §11):
  - La prueba revisa el código fuente: localiza la raíz del repositorio subiendo hasta `Pos.slnx` y recorre los `.cs` de `src/Pos.Infrastructure`, excluyendo `Persistence/Migrations/`.
  - Falla si un archivo menciona `AuditEntries` o `AuditEntry` y además `ExecuteUpdate` o `ExecuteDelete`, e indica el archivo.
  - No se usa NetArchTest, porque no ve sobre qué `DbSet` se hace una llamada.

**Checkpoint**: `dotnet build -v q` sin advertencias; pasan Domain.Tests, Application.Tests, ArchitectureTests, `AuditAuthorTests` y las pruebas de migración. Los más de 40 llamadores existentes de `IAuditLog.Add` siguen compilando sin cambios.

---

## Phase 3: User Story 1 - Bitácora centralizada con antes y después (Priority: P2) 🎯 MVP

**Goal**:
- Productos auditados: alta, modificación (incluido marcar como crítico) y eliminación.
- Antes y después por campo en las ediciones que ya se registran.
- Una entrada por venta con descuento.
- Motivo y productos afectados en cancelaciones y devoluciones, y motivo en el cajón.
- Una pantalla que muestra los cambios.

**Independent Test**: quickstart §2.
1. Con un producto: crearlo, editarlo (precio y categoría), guardarlo sin cambios y borrarlo.
2. Cancelar una venta con autorización, hacer una devolución, cobrar con descuento, abrir el cajón y editar un usuario.
3. En "Auditoría", cada evento aparece con autor y fecha, y cada edición muestra solo los campos cambiados con antes → después.

### Tests for User Story 1

- [X] T020 [P] [US1] Crear `UserAuditFieldsTests` en tests/Pos.Application.Tests/Audit/UserAuditFieldsTests.cs: la instantánea de un usuario nunca contiene la contraseña ni el hash (ni por nombre de campo ni por valor) y sí contiene Nombre completo, Usuario, Rol y Estado (depende de T024)
- [X] T021 [P] [US1] Crear `ProductAuditTests` sobre SQLite real en tests/Pos.Infrastructure.Tests/Audit/ProductAuditTests.cs (depende de T026–T029). Casos:
  - (a) Cambiar el precio de $25.00 a $28.50 y la categoría crea **una** entrada `PRODUCT_UPDATED` con exactamente esos dos cambios.
  - (b) Guardar sin cambios no crea entrada.
  - (c) Un conflicto de versión no deja entrada (FR-012).
  - (d) `SetProductCritical` crea un `PRODUCT_UPDATED` con el único cambio "Crítico" (No → Sí).
- [X] T022 [P] [US1] Crear `SaleDiscountAuditTests` sobre SQLite real en tests/Pos.Infrastructure.Tests/Audit/SaleDiscountAuditTests.cs (depende de T035). Casos:
  - Una venta con un descuento de línea sin autorización crea una sola entrada `SALE_DISCOUNTS_APPLIED`, con `EntityName` "Venta {folio}" y un cambio por descuento.
  - Una venta sin descuento no crea entrada (SC-007).

### Instantáneas de auditoría

- [X] T023 [P] [US1] Crear `ProductAuditFields.Snapshot(product, categoryName, unit)` en src/Pos.Application/Products/ProductAuditFields.cs, con estos campos en este orden:
  - SKU, Código de barras, Nombre, Precio y Unidad de medida.
  - Categoría ("nombre o "Sin categoría"").
  - Maneja inventario (Sí/No).
  - Existencia mínima ("con los decimales de la unidad").
  - Crítico (Sí/No).
  - Estado (Activo/Inactivo).
  - Imagen ("Sin imagen" o "Con imagen", sin su contenido).

  Sin costo (FR-003a) ni campos técnicos (`Version`, `UpdatedAt`, `NameSearch`).
- [X] T024 [P] [US1] Crear `UserAuditFields.Snapshot(user)` con Nombre completo, Usuario, Rol y Estado en src/Pos.Application/Users/UserAuditFields.cs. "**Nunca** contraseña ni hash."
- [X] T025 [P] [US1] Crear las instantáneas de categorías, clientes y cupones:
  - `CategoryAuditFields` (Nombre, Estado) en src/Pos.Application/Categories/CategoryAuditFields.cs.
  - `CustomerAuditFields` ("Los campos editables del formulario de cliente, incluidos el límite y la modalidad de crédito") en src/Pos.Application/Customers/CustomerAuditFields.cs.
  - `CouponAuditFields` ("Los campos editables del formulario de cupón") en src/Pos.Application/Discounts/CouponAuditFields.cs.

### Productos (FR-001 a FR-005)

- [X] T026 [US1] Registrar `PRODUCT_CREATED` con `EntityName` = nombre del producto y `Changes = AuditChanges.Created(snapshot)` antes de guardar, en la misma operación, en src/Pos.Application/Products/CreateProduct/CreateProductHandler.cs (depende de T023)
- [X] T027 [US1] En src/Pos.Application/Products/UpdateProduct/UpdateProductHandler.cs (depende de T023):
  - Tomar la instantánea antes y después de modificar, con el nombre de la categoría vigente en cada momento.
  - Si el comando trae `ProductImageChange.Replace` y el producto ya tenía imagen, agregar a mano el cambio ("Imagen", "Con imagen", "Imagen reemplazada") al resultado de `Compare` (research §5).
  - Registrar `PRODUCT_UPDATED` solo si la lista resultante no está vacía.
  - La desactivación es un cambio del campo Estado.
- [X] T028 [US1] Registrar `PRODUCT_DELETED` con `Removed(snapshot)` (últimos valores) en src/Pos.Application/Products/DeleteProduct/DeleteProductHandler.cs (depende de T023)
- [X] T029 [US1] En src/Pos.Application/Reports/SetProductCritical/SetProductCriticalHandler.cs, inyectar `IAuditLog` y, antes de `SaveChangesAsync`, registrar `PRODUCT_UPDATED` con `EntityName` = nombre del producto y un único cambio "Crítico" (Sí/No con `AuditFormat`). Si el valor no cambia, el handler ya termina antes sin registrar (depende de T007, T008)

### Ediciones que ya se registran (FR-006)

- [X] T030 [P] [US1] Usuarios, en src/Pos.Application/Users/CreateUser/CreateUserHandler.cs y src/Pos.Application/Users/UpdateUser/UpdateUserHandler.cs (depende de T024):
  - `CreateUser` registra `Created(snapshot)` con `EntityName` = nombre de usuario.
  - `UpdateUser` registra `Compare(before, after)`, incluido el Estado en la activación y la desactivación, y no registra nada si no hay cambios.
  - `ResetUserPassword` y `ChangeOwnPassword` no cambian.
- [X] T031 [P] [US1] Categorías: agregar `Changes` y `EntityName` en (depende de T025):
  - src/Pos.Application/Categories/CreateCategory/CreateCategoryHandler.cs: `Created`.
  - src/Pos.Application/Categories/UpdateCategory/UpdateCategoryHandler.cs: `Compare`.
  - src/Pos.Application/Categories/SetCategoryActive/SetCategoryActiveHandler.cs: Estado; últimos valores al desactivar.
  - src/Pos.Application/Categories/DeleteCategory/DeleteCategoryHandler.cs: `CATEGORY_DELETED` con `Removed(snapshot)`. No se agregan entradas por producto: el borrado se rechaza si la categoría tiene productos (research §5).
- [X] T032 [P] [US1] Clientes: agregar `Changes` y `EntityName` en src/Pos.Application/Customers/CreateCustomer/CreateCustomerHandler.cs, src/Pos.Application/Customers/UpdateCustomer/UpdateCustomerHandler.cs (incluido el crédito) y src/Pos.Application/Customers/SetCustomerActive/SetCustomerActiveHandler.cs (depende de T025)
- [X] T033 [P] [US1] Cupones: agregar `Changes` y `EntityName` en src/Pos.Application/Discounts/Coupons/SaveCoupon/SaveCouponHandler.cs (alta con `Created`, edición con `Compare`) y src/Pos.Application/Discounts/Coupons/SetCouponActive/SetCouponActiveHandler.cs (depende de T025)
- [X] T034 [P] [US1] Configuración: cada handler toma la configuración antes y después, registra `Compare` con sus campos legibles y no registra nada si no cambió:
  - src/Pos.Application/Returns/SaveReturnsSettings/SaveReturnsSettingsHandler.cs: plazo de devoluciones.
  - src/Pos.Application/Receivables/SaveReceivablesSettings/SaveReceivablesSettingsHandler.cs: plazo de crédito.
  - src/Pos.Application/Discounts/Settings/SaveDiscountSettings/SaveDiscountSettingsHandler.cs: límite de descuento.
  - src/Pos.Application/Reports/SaveReportSettings/SaveReportSettingsHandler.cs: configuración de reportes.

### Ventas, devoluciones y cajón (FR-007, FR-008, FR-009)

- [X] T035 [US1] En src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs (research §7):
  - Dejar de emitir `DISCOUNT_APPLIED_AUTHORIZED`.
  - Emitir **una** entrada `SALE_DISCOUNTS_APPLIED` por cada venta con al menos un descuento, autorizado o no: entidad `Sale` y `EntityName` "Venta {folio}".
  - Un cambio por descuento:
    - **Campo**: "Producto {nombre}" o "Total de la venta".
    - **Antes**: el importe sin descuento.
    - **Después**: el importe con descuento, más `DiscountTexts.Describe`, el cupón y quién autorizó.
  - `AuthorizedBy` = el primer autorizador.
  - `DISCOUNT_AUTHORIZED` no cambia.
- [X] T036 [US1] En tests/Pos.Infrastructure.Tests/Discounts/DiscountTestBase.cs, hacer que `AppliedAuthorizedCountAsync` cuente las entradas `SALE_DISCOUNTS_APPLIED` con `AuthorizedBy` no nulo, en lugar de `DISCOUNT_APPLIED_AUTHORIZED`. Verificar que tests/Pos.Infrastructure.Tests/Discounts/ConfirmSaleApprovalTests.cs (líneas 67 y 82) siga pasando con el mismo significado (depende de T035).
- [X] T037 [P] [US1] En src/Pos.Application/Sales/CancelSale/CancelSaleHandler.cs, registrar `SALE_CANCELLED` con `AuditRecord` (research §8):
  - `EntityName` "Venta {folio}", `Reason` = motivo capturado y `AuthorizedBy`.
  - `Changes`: un cambio por producto (Campo "Producto {nombre}", Antes "{cantidad} × {precio}", Después "Cancelado") y uno final "Importe".
  - `Details`: solo el resumen de pago y reembolso.
- [X] T038 [P] [US1] En src/Pos.Application/Returns/SaleReturnProcessor.cs, registrar `SALE_RETURNED` con:
  - `EntityName` "Venta {folio}" y `Reason` = motivo.
  - `Changes`: un cambio por producto devuelto (Antes "{cantidad} × {precio}", Después "Devuelto: {cantidad}") y uno final "Importe".
  - `Details`: solo el resumen del reembolso.
- [X] T039 [P] [US1] En src/Pos.Application/Printing/OpenCashDrawer/OpenCashDrawerHandler.cs, registrar `DRAWER_OPENED` con `Reason` = motivo capturado y `Details` solo con el resultado

### Lectura y pantalla

- [X] T040 [US1] Ampliar `AuditRow` con `EntityName`, `Reason` y `Changes` (lista vacía si no tiene), con el orden de campos de contracts/application-ports.md, en src/Pos.Application/Audit/AuditDtos.cs. Proyectarlos en src/Pos.Infrastructure/Audit/AuditLogReader.cs, leyendo `Changes` solo para las filas de la página (depende de T011).
- [X] T041 [P] [US1] Agregar a src/Pos.Desktop/Resources/Strings.resx los textos `Audit_Entity`, `Audit_Record`, `Audit_Summary`, `Audit_Reason`, `Audit_Changes`, `Audit_Field`, `Audit_Before`, `Audit_After` y `Audit_NoResults` ("No hay entradas con estos filtros.")
- [X] T042 [US1] Actualizar la pantalla en src/Pos.Desktop/Administration/AuditLogViewModel.cs y src/Pos.Desktop/Administration/AuditLogView.axaml (contracts/ui.md) (depende de T040, T041):
  - Columnas: Fecha y hora · Evento · Entidad · Registro · Usuario · Autorizó · Resumen.
    - Registro: `EntityName`, vacío en las entradas anteriores.
    - Resumen: "Precio, Categoría (+1)", o `Details` recortado si no hay cambios.
  - Panel de detalle de la fila seleccionada: encabezado, Motivo, tabla Campo | Antes | Después (nulos como "—", textos largos ajustados) y Detalles completos.
  - Ningún botón para editar o borrar.
- [X] T043 [US1] Ajustar las pruebas existentes tests/Pos.Infrastructure.Tests/Audit/AuditLogReaderTests.cs y tests/Pos.Infrastructure.Tests/Audit/CashDrawerAuditTests.cs a la nueva firma de `AuditRow` y al `Reason` del cajón, sin agregar casos nuevos (depende de T039, T040)

**Checkpoint**: quickstart §2 se cumple; pasan `UserAuditFieldsTests`, `ProductAuditTests`, `SaleDiscountAuditTests`, las pruebas de descuentos y las pruebas de auditoría existentes.

---

## Phase 4: User Story 2 - Filtros de búsqueda (Priority: P2)

**Goal**: filtrar por entidad además de fechas, usuario involucrado y evento; ver el historial completo de un registro; responder en menos de 1 s con 1,000,000 de entradas.

**Independent Test**: quickstart §3 y §6.
- Combinar filtros: Entidad = Producto; Evento = "Producto modificado" con rango; Usuario = Administrador que autorizó.
- Abrir "Ver historial del registro".
- Un rango invertido se rechaza y los filtros sin resultados muestran el aviso.
- La prueba de rendimiento explícita pasa.

### Tests for User Story 2

- [X] T044 [P] [US2] Ampliar tests/Pos.Infrastructure.Tests/Audit/AuditLogReaderTests.cs con estos casos (depende de T047):
  - Filtro por `AuditEntityGroup.Product` y `Session`.
  - Filtro `Settings`: incluye `REPORT_SETTINGS_CHANGED`, que no aparece en Reportes y exportaciones.
  - Historial `(EntityType, EntityId)` ordenado de la más antigua a la más reciente.
  - Filtro de usuario que encuentra al autorizador.
  - Entradas anteriores sin `Changes` incluidas por su entidad (FR-021).
- [X] T045 [P] [US2] Crear `AuditLogPerformanceTests` con `[Fact(Explicit = true)]` en tests/Pos.Infrastructure.Tests/Audit/AuditLogPerformanceTests.cs (SC-002) (depende de T047):
  - Siembra 1,000,000 de entradas en SQLite en un archivo temporal.
  - Mide la primera página con fechas, usuario, evento, entidad, historial y todos juntos.
  - Exige menos de 1 s y escribe en la salida las búsquedas que pasen de 300 ms.

### Implementation for User Story 2

- [X] T046 [P] [US2] Crear `AuditEntityGroup` en src/Pos.Application/Audit/AuditEntityGroup.cs:
  - Grupos: Producto, Venta, Usuario, Sesión, Categoría, Cliente, Cupón, Caja/Turno, Configuración, y Reportes y exportaciones, cada uno con su texto en español.
  - Criterio de cada grupo: exactamente la tabla de research §9, por `EntityType` y, donde corresponde, por evento.
  - Usuario excluye los eventos de sesión; Sesión incluye `LOGIN_*`, `USER_LOCKED_OUT` y `LOGOUT`.
  - Configuración incluye el evento `REPORT_SETTINGS_CHANGED`, y Reportes y exportaciones lo excluye.
- [X] T047 [US2] Cambiar los tipos de búsqueda y el lector (depende de T046, T040):
  - En src/Pos.Application/Audit/AuditDtos.cs, reemplazar `AuditSearch` por `AuditFilter(FromUtc, ToUtcExclusive, UserId, Action, Entity, Record)`, `AuditRecordRef(EntityType, EntityId)` y `AuditSearch(Filter, Page, PageSize)`.
  - En src/Pos.Infrastructure/Audit/AuditLogReader.cs, aplicar el filtro de entidad (por `EntityType` y por evento, según el grupo) y el de registro.
  - Orden ascendente por `CreatedAt` cuando hay `Record`; descendente en otro caso.
  - Extraer la construcción del `IQueryable` filtrado a un método que también usará `ListAsync` (US3).
- [X] T048 [US2] Actualizar el caso de uso `SearchAuditLog` (depende de T047):
  - Agregar `Entity` y `Record` a `SearchAuditLogQuery` en src/Pos.Application/Audit/SearchAuditLog/SearchAuditLogQuery.cs.
  - Mantener la validación `From < To` en src/Pos.Application/Audit/SearchAuditLog/SearchAuditLogValidator.cs.
  - Construir el `AuditFilter` en src/Pos.Application/Audit/SearchAuditLog/SearchAuditLogHandler.cs.
- [X] T049 [P] [US2] Agregar a src/Pos.Desktop/Resources/Strings.resx `Audit_ViewHistory`, `Audit_HistoryOf` ("Historial de: {0}") y los nombres de los grupos de entidad
- [X] T050 [US2] Actualizar la pantalla en src/Pos.Desktop/Administration/AuditLogViewModel.cs y src/Pos.Desktop/Administration/AuditLogView.axaml (depende de T048, T049):
  - Filtro "Entidad": Todas y los grupos.
  - Fechas iniciales en hoy.
  - Lista de usuarios con los inactivos y "Sistema".
  - Botón "Ver historial del registro" en el panel de detalle: aplica `Record`, limpia las fechas y se desactiva si `EntityId` es `Guid.Empty`.
  - Chip "Historial de: {registro}", con una ✕ que lo quita.
  - Cada cambio de filtro vuelve a la página 1.

**Checkpoint**: quickstart §3 se cumple; `AuditLogReaderTests` pasa; la prueba explícita de rendimiento pasa con menos de 1 s.

---

## Phase 5: User Story 3 - Exportación a PDF o Excel (Priority: P3)

**Goal**: exportar a PDF o XLSX todas las entradas filtradas (rango obligatorio), y registrar la exportación solo si el archivo se guardó.

**Independent Test**: quickstart §4.
- Exportar un rango con más de 100 entradas a PDF y a Excel: ambos archivos lo contienen todo.
- Aparecen dos entradas "Bitácora exportada".
- Exportar sin rango se rechaza.
- Exportar a una carpeta sin permiso muestra el error y no registra nada.

### Tests for User Story 3

- [X] T051 [P] [US3] Crear `ExportAuditLogTests` sobre SQLite real en tests/Pos.Infrastructure.Tests/Audit/ExportAuditLogTests.cs (depende de T055, T056). Casos, con 150 entradas:
  - El XLSX tiene una fila por cambio de campo, o una por entrada si no tiene cambios, para todas las entradas.
  - `ExportAuditLog` no agrega entradas a la bitácora.
  - `ConfirmAuditExport` agrega una `AUDIT_EXPORTED` con formato, rango, filtros y número de entradas.
  - Sin rango, la exportación se rechaza.

### Implementation for User Story 3

- [X] T052 [P] [US3] En src/Pos.Infrastructure/Reports/XlsxReportWriter.cs, omitir la hoja "Gráficas" cuando `ReportDocument.Charts` está vacío. Revisar si algún reporte existente se exporta sin gráficas y anotarlo en docs/reportes.md.
- [X] T053 [US3] Agregar `ListAsync(AuditFilter filter, CancellationToken)` a src/Pos.Application/Audit/IAuditLogReader.cs e implementarlo en src/Pos.Infrastructure/Audit/AuditLogReader.cs con el mismo filtrado y orden que `SearchAsync`, en una sola consulta, sin tope de filas (depende de T047)
- [X] T054 [P] [US3] Crear `AuditExport(ExportedFile File, AuditExportReceipt Receipt)` y `AuditExportReceipt(ExportFormat Format, AuditFilter Filter, int EntryCount)` en src/Pos.Application/Audit/ExportAuditLog/AuditExport.cs
- [X] T055 [US3] Crear el caso de uso `ExportAuditLog` en src/Pos.Application/Audit/ExportAuditLog/ (depende de T053, T054):
  - Tipos: `ExportAuditLogCommand(AuditFilter Filter, ExportFormat Format)`, `ExportAuditLogValidator` (exige `FromUtc` y `ToUtcExclusive`, y `From < To`), `ExportAuditLogHandler` con permiso `ViewAuditLog` y `AuditLogDocumentBuilder`.
  - Reutiliza `ReportDocument`, `IPdfReportWriter` e `IXlsxReportWriter` de src/Pos.Application/Reports/Export/.
  - **PDF**: encabezado del negocio, rango, filtros, fecha de generación y usuario que exporta. Una fila por entrada con fecha, evento, entidad, registro, usuario, autorizó, motivo y cambios ("Campo: antes → después", en varias líneas).
  - **XLSX**: una fila por cambio (FR-024), con fecha y hora como valor de fecha, evento, entidad, registro, autor, autorizador, motivo, campo, antes y después.
  - **No** registra nada en la bitácora.
  - Escribe en Serilog el formato, las entradas, la duración y los rechazos.
- [X] T056 [US3] Crear el caso de uso `ConfirmAuditExport` en src/Pos.Application/Audit/ConfirmAuditExport/: `ConfirmAuditExportCommand(AuditExportReceipt Receipt)` y un handler con permiso `ViewAuditLog`, que registra `AUDIT_EXPORTED` con entidad `AuditLog` y `Details` con formato, rango, filtros y número de entradas, y guarda (depende de T054)
- [X] T057 [US3] Registrar `ExportAuditLogHandler`, `ExportAuditLogValidator` y `ConfirmAuditExportHandler` en src/Pos.Application/DependencyInjection.cs (depende de T055, T056)
- [X] T058 [P] [US3] Agregar a src/Pos.Desktop/Resources/Strings.resx `Audit_ExportPdf`, `Audit_ExportExcel`, `Audit_ExportNeedsRange` ("Elige un rango de fechas para exportar."), `Audit_Exported` ("Bitácora exportada ({0} entradas).") y `Audit_ExportFailed` ("No se pudo guardar el archivo: {0}.")
- [X] T059 [US3] Agregar la exportación a src/Pos.Desktop/Administration/AuditLogViewModel.cs y src/Pos.Desktop/Administration/AuditLogView.axaml (depende de T057, T058):
  - Botones "Exportar PDF" y "Exportar Excel".
  - Sin rango, mostrar `Audit_ExportNeedsRange`.
  - Generar fuera del hilo de la interfaz, con indicador de espera.
  - Diálogo de guardar como en src/Pos.Desktop/Reports/ReportExportCoordinator.cs y src/Pos.Desktop/Common/DialogService.cs, con el nombre `bitacora_AAAAMMDD-AAAAMMDD.pdf` o `.xlsx`.
  - Si se guarda bien, llamar a `ConfirmAuditExport` y mostrar `Audit_Exported`. Si se cancela, nada. Si falla la escritura, mostrar `Audit_ExportFailed` y no registrar nada.

**Checkpoint**: quickstart §4 y §7 se cumplen; `ExportAuditLogTests` pasa.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: documentación, soporte y validación final.

- [X] T060 [P] Crear docs/auditoria.md con:
  - Los eventos, incluidos los nuevos y `SetProductCritical`.
  - Los grupos de entidad.
  - El formato de antes y después, incluidos los productos de cancelaciones y devoluciones.
  - El motivo, el autor "Sistema" y el historial del registro.
  - La exportación en dos pasos.
  - La inmutabilidad y lo que queda fuera: triggers y cadena de hashes.
- [X] T061 [P] Actualizar docs/usuarios-y-permisos.md: consultar y exportar la bitácora es exclusivo del Administrador (`ViewAuditLog`)
- [X] T062 [P] Agregar la sección 0.13.0 a docs/migraciones.md: migración `AuditTrail`, solo `ADD COLUMN` e índices, sin reconstrucciones, base de ejemplo `v0.13.0.db`
- [X] T063 Verificar en src/Pos.Application/ que ningún llamador de `IAuditLog.Add` registra contraseñas, hashes ni secretos (FR-006), y que ningún caso de uso, además de `SetProductCritical`, modifica campos auditados del producto sin registrarlo (SC-001)
- [X] T064 Ejecutar `dotnet build -v q` (0 advertencias) y `dotnet test --verbosity quiet` desde la raíz
- [ ] T065 Validación final:
  - Ejecutar la prueba explícita de rendimiento (quickstart §6) y la exportación de 10,000 entradas (quickstart §7).
  - Validar de punta a punta quickstart §2–§4.
  - Confirmar que "Auditoría" no queda bloqueada por la licencia de módulos (FR-027).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias.
- **Foundational (Phase 2)**: depende de Setup. **Bloquea** todas las historias.
- **US1 (Phase 3)**: depende de Foundational.
- **US2 (Phase 4)**: depende de Foundational y de T040 (firma de `AuditRow` en `AuditLogReader`). Su lógica no depende del resto de US1.
- **US3 (Phase 5)**: depende de T047 (filtro reutilizable de US2) para `ListAsync`. T052 y T054 pueden empezar después de Foundational.
- **Polish (Phase 6)**: después de las historias que se quieran entregar.

### User Story Dependencies

- **US1**: independiente después de Foundational.
- **US2**: comparte `AuditDtos.cs`, `AuditLogReader.cs`, `AuditLogViewModel.cs`, `AuditLogView.axaml` y `Strings.resx` con US1; hacerla después de T040–T042 para no chocar en esos archivos.
- **US3**: reutiliza `AuditFilter` (US2). Se prueba sola con `ExportAuditLogTests`.

### Within Each User Story

- Instantáneas antes que los handlers que las usan.
- Handlers antes de sus pruebas de integración. Estas pruebas verifican, sobre SQLite, el comportamiento que se acaba de implementar, así que se escriben junto con la implementación.
- DTOs y lector antes del ViewModel.
- T035 (descuentos) y T036 (ajuste de pruebas de descuentos) van juntos: sin T036 la suite de descuentos falla.

### Parallel Opportunities

- Foundational: T002, T004, T006, T007 y T008 en paralelo; luego T014, T015, T016, T017 y T019 en paralelo.
- US1:
  - T023, T024 y T025 en paralelo.
  - T030–T034 y T037–T039 en paralelo entre sí (archivos distintos).
  - T020, T021 y T022 en paralelo cuando terminan sus dependencias.
- US2: T044, T045, T046 y T049 en paralelo.
- US3: T052, T054 y T058 en paralelo con el resto.
- Polish: T060, T061 y T062 en paralelo.

---

## Parallel Example: User Story 1

```bash
# Instantáneas en paralelo:
Task: "Crear ProductAuditFields en src/Pos.Application/Products/ProductAuditFields.cs"
Task: "Crear UserAuditFields en src/Pos.Application/Users/UserAuditFields.cs"
Task: "Crear CategoryAuditFields, CustomerAuditFields y CouponAuditFields"

# Ediciones existentes en paralelo (archivos distintos):
Task: "Usuarios: Changes en CreateUserHandler y UpdateUserHandler"
Task: "Categorías: Changes en Create/Update/SetCategoryActive/DeleteCategory"
Task: "Clientes: Changes en Create/Update/SetCustomerActive"
Task: "Cupones: Changes en SaveCoupon y SetCouponActive"
Task: "Configuración: Changes en los cuatro handlers de configuración"
Task: "CancelSale con Reason, EntityName y un cambio por producto"
Task: "SaleReturnProcessor con Reason, EntityName y un cambio por producto"
Task: "OpenCashDrawer con Reason"
```

## Parallel Example: User Story 2

```bash
Task: "Crear AuditEntityGroup en src/Pos.Application/Audit/AuditEntityGroup.cs"
Task: "Agregar textos de historial y grupos en Strings.resx"
# Después de T047:
Task: "Ampliar AuditLogReaderTests"
Task: "Crear AuditLogPerformanceTests (explícita)"
```

## Parallel Example: User Story 3

```bash
Task: "Omitir la hoja Gráficas vacía en XlsxReportWriter"
Task: "Crear AuditExport y AuditExportReceipt"
Task: "Agregar textos de exportación en Strings.resx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup.
2. Phase 2: Foundational (modelo, puerto, comparador, autor, migración y pruebas obligatorias).
3. Phase 3: US1.
4. **STOP and VALIDATE**: quickstart §2; pasan las pruebas de Domain, Application e Infrastructure de auditoría y de descuentos.
5. Con esto, la bitácora ya registra productos, antes y después, descuentos, motivos y productos afectados, y la pantalla los muestra.

### Incremental Delivery

1. Setup + Foundational → la base migra a 0.13.0 sin perder entradas.
2. US1 → evidencia completa con antes y después (MVP).
3. US2 → filtros por entidad e historial, con la prueba de rendimiento.
4. US3 → exportación a PDF y Excel.
5. Polish → documentación y validación final antes de publicar 0.13.0.

### Parallel Team Strategy

1. El equipo completa Setup + Foundational.
2. Después:
   - Desarrollador A: US1 (handlers e instantáneas).
   - Desarrollador B: la parte de US2 en Application e Infrastructure (T046, T047, T048, T044, T045), y la de pantalla al terminar T042.
   - Desarrollador C: T052 y T054 de US3; el resto al terminar T047.

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes.
- Las migraciones publicadas no se modifican; si `AuditTrail` necesita una corrección después de publicarse, va en una migración nueva.
- La bitácora nunca se modifica ni se borra: no usar `ExecuteUpdate`/`ExecuteDelete` sobre `AuditEntries` (T019 lo verifica).
- La entrada de bitácora siempre se agrega antes del `SaveChanges` del caso de uso, en la misma transacción (FR-012).
- Hacer commit después de cada tarea o grupo lógico.
