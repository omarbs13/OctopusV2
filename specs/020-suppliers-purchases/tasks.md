---

description: "Lista de tareas de la funcionalidad 020: proveedores y compras básicas"
---

# Tasks: Proveedores y compras básicas

**Input**: Documentos de diseño en `specs/020-suppliers-purchases/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: el plan pide pruebas concretas según la política mínima de la constitución v1.2.0 (research §16). Solo se incluyen esas: `PurchaseMath`, `Purchase`, `Supplier`, `ProductStock` con los tipos de compra, permisos y licencia; casos de uso sobre SQLite real (existencia exacta, factura duplicada, atomicidad, concurrencia, anulación, inmutabilidad, acumulados del reporte, RUC único); consistencia de inventario (obligatoria); migración y arquitectura. **No** hay pruebas de ViewModels, vistas ni DTOs.

**Organization**: tareas agrupadas por historia de usuario. Las tres historias son P3 y se ejecutan en el orden de la spec, que es el de sus dependencias (proveedores → compras → reporte).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes).
- **[Story]**: historia a la que pertenece (US1, US2, US3).
- Comandos del proyecto: `dotnet build -v q` y `dotnet test --verbosity quiet`. Al implementar, ejecutar solo las pruebas del proyecto modificado (constitución VI).

## Path Conventions

- Capas: `src/Pos.Domain/`, `src/Pos.Application/`, `src/Pos.Infrastructure/`, `src/Pos.Desktop/`.
- Pruebas: `tests/Pos.Domain.Tests/`, `tests/Pos.Infrastructure.Tests/`, `tests/Pos.ArchitectureTests/`.
- Código en inglés; textos de interfaz, mensajes y documentación en español.
- Molde de referencia: el catálogo de clientes (014) en `src/Pos.*/Customers/` y los casos de uso transaccionales `ConfirmSale` / `CancelSale`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dejar la base de ejemplo de la versión anterior y subir la versión. No se agrega ninguna dependencia externa.

- [X] T001 Generar la base de ejemplo faltante tests/Pos.Infrastructure.Tests/SampleDatabases/v0.13.0.db con el código actual (0.13.0, commit `614a463`), **antes** de cualquier cambio de modelo: `POS_GENERATE_SAMPLE_DB=1 dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.SampleDatabases.SampleDatabaseGenerator"` (research §12, docs/migraciones.md). Verificar que el archivo se creó y que `SampleDatabaseUpgradeTests` lo toma.
- [X] T002 Subir `<Version>` de `0.13.0` a `0.14.0` en Directory.Build.props (depende de T001)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: permisos, licencia, bitácora, entidades, persistencia y la migración única `SuppliersAndPurchases` (las tres tablas van en una sola migración, así que todo el esquema se hace aquí).

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase.

### Domain

- [X] T003 [P] Agregar los permisos `ManageSuppliers`, `RegisterPurchases`, `VoidPurchases` y `ViewPurchaseReport` al enum en src/Pos.Domain/Users/Permission.cs. Solo el Administrador los recibe (`AdminPermissions` incluye todo el enum); **no** agregarlos a `CashierPermissions` ni a `Authorizable` en src/Pos.Domain/Users/RolePermissions.cs (research §11, FR-024–FR-026)
- [X] T004 Asignar los cuatro permisos nuevos a `LicensedModule.Inventory` en `ModuleAccess.Required` de src/Pos.Domain/Licensing/ModuleAccess.cs (depende de T003)
- [X] T005 [P] Crear `EmailAddress.IsValid(string?)` en src/Pos.Domain/Common/EmailAddress.cs moviendo la regla actual de `Customer.IsValidEmail`; hacer que `Customer.IsValidEmail` en src/Pos.Domain/Customers/Customer.cs delegue en ella sin cambiar el comportamiento
- [X] T006 [P] Agregar a src/Pos.Domain/Inventory/MovementType.cs los valores `Purchase` (código `PURCHASE`, aumenta) y `PurchaseVoid` (código `PURCH_VOID`, disminuye) en `ToCode`/`FromCode`; `IsIncrease()` devuelve `false` para `PurchaseVoid`; `RequiresReason()` sigue en `false` para ambos (research §2: "caben en `InventoryMovements.Type` (TEXT(12))"). En el mismo cambio agregar los casos a src/Pos.Desktop/Inventory/MovementTypeLabels.cs con `Strings.MovementType_Purchase` ("Entrada de compra") y `Strings.MovementType_PurchaseVoid` ("Anulación de compra") en src/Pos.Desktop/Resources/Strings.resx, para que el `switch` siga completo y la solución compile con 0 advertencias
- [X] T007 [P] Crear `PaymentTerms` (enum `Cash`, `Credit` con códigos `CASH` / `CREDIT`, TEXT(10)) en src/Pos.Domain/Suppliers/PaymentTerms.cs
- [X] T008 Crear el agregado `Supplier` en src/Pos.Domain/Suppliers/Supplier.cs con el molde de `Customer` (depende de T005, T007). Reglas de data-model.md:
  - `Name`: "Obligatorio, recortado. Puede repetirse", máx. 150 (`NameMaxLength = 150`).
  - `TaxId`: "RUC opcional; recortado y en mayúsculas", máx. 20; vacío → nulo. Exponer `NormalizeTaxId(string?)` estático.
  - `Phone`: "Opcional, recortado", máx. 30. `Email`: "Opcional; formato válido si se captura (`EmailAddress.IsValid`)", máx. 254. `Address`: "Opcional, recortado", máx. 300.
  - `PaymentTerms` obligatorio; `CreditDays`: "Nulo en `CASH`; 1–365 en `CREDIT`".
  - `IsActive` (`true` al crear), `SearchText` = `TextNormalizer.ForSearch(nombre + RUC)` (máx. 200), `DeletedAt` (no se usa) y `Version`.
  - Métodos `Create(...)`, `Update(...)` (recalcula `SearchText`; al pasar a `CASH` limpia `CreditDays`), `Deactivate()`, `Activate()`. Violaciones → `DomainException` con mensaje en español.
- [X] T009 [P] Crear `PurchaseStatus` (enum `Active` = `ACTIVE` "Vigente", `Voided` = `VOIDED` "Anulada", TEXT(10)) en src/Pos.Domain/Purchases/PurchaseStatus.cs
- [X] T010 [P] Crear `PurchaseMath` en src/Pos.Domain/Purchases/PurchaseMath.cs (research §4):
  - `LineAmount(quantityThousandths, unitCostCents)` delega en `SaleMath.LineAmount` ("milésimas × centavos + 500, entre 1,000", mitad hacia arriba).
  - `Subtotal(amounts)` = Σ importes de línea ya redondeados, con `checked`.
  - `Total(subtotal, tax)` = subtotal + impuestos.
  - Límites: importe de línea, subtotal, impuestos y total ≤ `Money.MaxCents` ($999,999.99); exponer una función que diga cuál se excede para que el caso de uso devuelva el campo.
- [X] T011 Crear `PurchaseLine` en src/Pos.Domain/Purchases/PurchaseLine.cs (depende de T010). Campos de data-model.md: `Id`, `PurchaseId`, `LineNumber` ("1..N en el orden de captura"), `ProductId`, `ProductName` (máx. `Product.NameMaxLength`), `ProductSku` (máx. `Product.SkuMaxLength`), `UnitCode` (máx. `UnitOfMeasure.CodeMaxLength`), `QuantityThousandths` ("> 0"), `UnitCostCents` ("≥ 0 … 0 = bonificación"), `AmountCents` (`PurchaseMath.LineAmount`), `MovementId` (`Guid`), `VoidMovementId` (`Guid?`). `IsBonus => UnitCostCents == 0` (no se guarda). Sin `UpdatedAt/By`, `DeletedAt` ni `Version`. Métodos internos `LinkMovement(Guid)` y `LinkVoidMovement(Guid)` (este último solo si es nulo).
- [X] T012 Crear el agregado `Purchase` en src/Pos.Domain/Purchases/Purchase.cs (depende de T009, T011). Campos de data-model.md: `SupplierId`, `SupplierName` (máx. 150, copia), `InvoiceNumber` ("Obligatorio, recortado, tal como se capturó", máx. 50), `InvoiceKey` ("`InvoiceNumber` en mayúsculas invariantes", máx. 50; `NormalizeInvoice` estático), `InvoiceDate` (`DateOnly`), `LineCount` ("≥ 1"), `SubtotalCents`, `TaxCents` ("≥ 0"), `TotalCents`, `Status`, `VoidedAt`, `VoidedBy`, `VoidReason` (máx. 250), `Version`; sin `DeletedAt`.
  - `Register(supplierId, supplierName, invoiceNumber, invoiceDate, today, lines, taxCents)`: "Al menos una línea; como máximo una línea por producto"; `InvoiceDate ≤ today`; `SubtotalCents = Σ AmountCents`, `TotalCents = SubtotalCents + TaxCents`, `SubtotalCents > 0` (FR-008a); límites de `PurchaseMath`; numera las líneas 1..N.
  - `Void(reason, userId, utcNow)`: "exige `Status = ACTIVE` y motivo recortado de 1 a 250 caracteres"; sin regreso a `ACTIVE`. Sin ningún método de edición (FR-017).

### Application

- [X] T013 [P] Agregar a src/Pos.Application/Audit/AuditActions.cs los eventos `SUPPLIER_CREATED` ("Proveedor creado"), `SUPPLIER_UPDATED` ("Proveedor modificado"), `SUPPLIER_DEACTIVATED` ("Proveedor desactivado"), `SUPPLIER_ACTIVATED` ("Proveedor activado"), `PURCHASE_REGISTERED` ("Compra registrada") y `PURCHASE_VOIDED` ("Compra anulada") con sus textos en `All`, y las constantes de entidad `SupplierEntity = "Supplier"` y `PurchaseEntity = "Purchase"` (research §14)
- [X] T014 Agregar el grupo `Purchases` ("Proveedores y compras") con las entidades `Supplier` y `Purchase` en src/Pos.Application/Audit/AuditEntityGroup.cs, y su nombre visible en src/Pos.Desktop/Resources/Strings.resx (depende de T013)

### Infrastructure

- [X] T015 [P] Crear src/Pos.Infrastructure/Persistence/Configurations/SupplierConfiguration.cs (depende de T008): tabla `Suppliers`, longitudes de T008, `PaymentTerms` como texto (10), `Version` como token de concurrencia, índices `IX_Suppliers_TaxId` (único, filtrado `"TaxId" IS NOT NULL`) e `IX_Suppliers_Active_Search` (`IsActive`, `SearchText`)
- [X] T016 [P] Crear src/Pos.Infrastructure/Persistence/Configurations/PurchaseConfiguration.cs (depende de T012): tabla `Purchases`, FK `SupplierId` → `Suppliers` (Restrict), `Status` como texto (10), `InvoiceDate` como `DateOnly` (igual que `Coupon.StartsOn`), `Version` como token, colección de líneas con backing field; índices `IX_Purchases_Supplier_InvoiceKey` (**único**, filtrado `"Status" = 'ACTIVE'`), `IX_Purchases_InvoiceDate` (`InvoiceDate`, `CreatedAt`, `Id`) e `IX_Purchases_Supplier_InvoiceDate` (`SupplierId`, `InvoiceDate`)
- [X] T017 [P] Crear src/Pos.Infrastructure/Persistence/Configurations/PurchaseLineConfiguration.cs (depende de T011): tabla `PurchaseLines`, FK `PurchaseId` (Cascade), `ProductId` → `Products` (Restrict), `MovementId` y `VoidMovementId` → `InventoryMovements` (Restrict), sin navegación inversa desde `InventoryMovement`; ignorar `IsBonus`; índices `IX_PurchaseLines_Purchase_LineNumber` (único), `IX_PurchaseLines_Purchase_Product` (único), `IX_PurchaseLines_MovementId` (único), `IX_PurchaseLines_VoidMovementId` (único, filtrado `"VoidMovementId" IS NOT NULL`) e `IX_PurchaseLines_ProductId`
- [X] T018 En src/Pos.Infrastructure/Persistence/PosDbContext.cs (depende de T015–T017):
  - Agregar los `DbSet` `Suppliers`, `Purchases` y `PurchaseLines`.
  - Ampliar `RejectImmutableChanges` (research §10): `PurchaseLine` solo admite cambiar `VoidMovementId` de nulo a un valor; cualquier otra modificación o borrado lanza `InvalidOperationException`. `Purchase` no se borra; una `VOIDED` no se modifica; en una `ACTIVE` solo cambian `Status`, `VoidedAt`, `VoidedBy`, `VoidReason`, `UpdatedAt/By` y `Version`.
- [X] T019 Generar la migración con `dotnet ef migrations add SuppliersAndPurchases` en src/Pos.Infrastructure/Persistence/Migrations/ (depende de T018). Revisar el SQL (`dotnet ef migrations script`): solo tres `CREATE TABLE` (`Suppliers`, `Purchases`, `PurchaseLines`) y sus índices; **ningún** `ALTER TABLE`, `DROP TABLE`, `UPDATE`, `INSERT` ni `ef_temp_` (research §12)

### Pruebas obligatorias de la fase

- [X] T020 [P] Ampliar tests/Pos.Domain.Tests/Users/RolePermissionsTests.cs: el Cajero no tiene `ManageSuppliers`, `RegisterPurchases`, `VoidPurchases` ni `ViewPurchaseReport`; el Administrador sí; ninguno es autorizable; `RegisterPurchases` y `RegisterMovements` son independientes (depende de T003)
- [X] T021 [P] Ampliar tests/Pos.Domain.Tests/Licensing/ModuleAccessTests.cs: los cuatro permisos nuevos requieren `LicensedModule.Inventory` (depende de T004)
- [X] T022 [P] Crear `SupplierTests` en tests/Pos.Domain.Tests/Suppliers/SupplierTests.cs (depende de T008): crédito sin días, con 0 o con 366 se rechaza; contado no pide días y `Update` a contado limpia `CreditDays`; RUC " abc123 " se guarda "ABC123" y vacío como nulo; email "correo@" se rechaza; nombre vacío o de 151 caracteres se rechaza
- [X] T023 [P] Crear `PurchaseMathTests` en tests/Pos.Domain.Tests/Purchases/PurchaseMathTests.cs (depende de T010): 5 × $12.50 + 2.5 kg × $40.00 = $162.50, + $26.00 = $188.50; 3 × $10.00 + 1.255 kg × $20.00 = $30.00 + $25.10 = $55.10, + $8.82 = $63.92; redondeo mitad hacia arriba por línea y subtotal = suma de redondeados; importe, subtotal o total > `Money.MaxCents` se señala
- [X] T024 [P] Crear `PurchaseTests` en tests/Pos.Domain.Tests/Purchases/PurchaseTests.cs (depende de T012): subtotal $0.00 (todas bonificación) se rechaza; una línea de $0.00 junto a otra con costo se acepta con `IsBonus`; producto repetido, sin líneas, impuestos negativos y fecha futura se rechazan; `Void` sin motivo, con motivo de 251 caracteres o sobre una compra ya anulada se rechaza; `Void` válido deja `VOIDED` con fecha, usuario y motivo
- [X] T025 Crear `SuppliersAndPurchasesMigrationTests` en tests/Pos.Infrastructure.Tests/SampleDatabases/SuppliersAndPurchasesMigrationTests.cs con el molde de `AuditTrailMigrationTests` (depende de T001, T019): el SQL de la migración tiene exactamente tres `CREATE TABLE` y no tiene `ALTER TABLE`, `DROP TABLE`, `UPDATE` ni `ef_temp_`; migrar `v0.13.0.db` conserva el número de movimientos de inventario y las existencias
- [X] T026 Ejecutar `dotnet build -v q` (0 advertencias) y las pruebas de Domain, Infrastructure y Architecture; ajustar las pruebas existentes que enumeran `MovementType` o `Permission` (p. ej. tests/Pos.Infrastructure.Tests/Inventory/InventoryConsistencyTests.cs) solo si fallan por los valores nuevos

**Checkpoint**: esquema migrado, permisos y entidades listos; las historias pueden empezar.

---

## Phase 3: User Story 1 - Catálogo de proveedores (Priority: P3, primera) 🎯 MVP

**Goal**: el Administrador da de alta, busca, edita, desactiva y reactiva proveedores con RUC único opcional y condiciones de pago.

**Independent Test**: dar de alta un proveedor con todos los datos y otro solo con nombre; buscar por nombre y por RUC; editar el teléfono; desactivar uno y verificar que desaparece del listado por omisión y aparece con "incluir inactivos" (quickstart §2).

### Tests for User Story 1

- [X] T027 [US1] Crear `SupplierUseCaseTests` sobre SQLite real en tests/Pos.Infrastructure.Tests/Suppliers/SupplierUseCaseTests.cs con el molde de `CustomerUseCaseTests` (depende de T036, T038): RUC "abc123 " y luego "ABC123" → `SupplierTaxIdInUse` con el nombre del existente, también si el existente está inactivo; dos proveedores sin RUC se aceptan; `SearchSuppliers` encuentra por "norte" y por "abc123" y excluye inactivos salvo con `IncludeInactive`; `UpdateSupplier` registra en la bitácora solo los campos cambiados y nada si no cambió nada; versión vieja → `Conflict`; un Cajero recibe `Forbidden`

### Implementation for User Story 1

- [X] T028 [P] [US1] Agregar el error `SupplierTaxIdInUse(Guid SupplierId, string Name)` en src/Pos.Application/Abstractions/Error.cs, con el mensaje "El RUC ya está registrado para el proveedor {nombre}"
- [X] T029 [P] [US1] Crear src/Pos.Application/Suppliers/SupplierFields.cs (nombres de campo: `Name`, `TaxId`, `Phone`, `Email`, `Address`, `PaymentTerms`, `CreditDays`) y src/Pos.Application/Suppliers/SupplierMessages.cs (mensajes en español: nombre obligatorio y longitud, email inválido, días de crédito "obligatorios con crédito, de 1 a 365", etc.)
- [X] T030 [P] [US1] Crear src/Pos.Application/Suppliers/SupplierDtos.cs: `SupplierDto` (todos los campos + `IsActive`, `Version`), `SupplierSearch(Text?, IncludeInactive, Page)`, `SupplierPage` con filas `{ Id, Name, TaxId, Phone, PaymentTerms, CreditDays, IsActive, Version }`, `SupplierOption { Id, Name, TaxId }` y `SupplierFilterOption { Id, Name, IsActive }`
- [X] T031 [P] [US1] Crear `SupplierAuditFields.Snapshot(Supplier)` en src/Pos.Application/Suppliers/SupplierAuditFields.cs: Nombre, RUC, Teléfono, Email, Dirección, Condiciones de pago ("Contado" / "Crédito a {n} días"), Estado (`AuditFormat`)
- [X] T032 [US1] Crear el puerto `ISupplierRepository` en src/Pos.Application/Suppliers/ISupplierRepository.cs (depende de T030): `GetAsync(id)`, `FindByTaxIdAsync(taxId)`, `Add(supplier)`, `SearchAsync(SupplierSearch)` → `SupplierPage` (100 por página), `ListForFilterAsync()` (todos, con estado), `ListActiveAsync(text?)` (≤ 50), `SaveChangesAsync()` → `SaveOutcome` (violación del índice de RUC → duplicado; versión → conflicto)
- [X] T033 [US1] Crear el caso de uso `CreateSupplier` en src/Pos.Application/Suppliers/CreateSupplier/ (`CreateSupplierCommand`, `CreateSupplierValidator`, `CreateSupplierHandler`) (depende de T028, T029, T031, T032): permiso `ManageSuppliers`; validador FluentValidation de forma (nombre 1–150, RUC ≤ 20, teléfono ≤ 30, email ≤ 254 y `EmailAddress.IsValid`, dirección ≤ 300, `CreditDaysText` entero 1–365 solo con crédito); en `IWriteTransactions.BeginAsync`: RUC normalizado ya usado → `SupplierTaxIdInUse`; `Supplier.Create`; bitácora `SUPPLIER_CREATED` con `AuditChanges.Created(snapshot)` y `EntityName` = nombre; un solo `SaveChangesAsync`; índice violado → `SupplierTaxIdInUse`. Devuelve `Result<Guid>`
- [X] T034 [US1] Crear el caso de uso `UpdateSupplier` en src/Pos.Application/Suppliers/UpdateSupplier/ (`UpdateSupplierCommand` con `Id` y `ExpectedVersion`, `UpdateSupplierValidator`, `UpdateSupplierHandler`) (depende de T033): mismas reglas que crear, excluyendo al propio proveedor en la búsqueda de RUC; `NotFound`; versión → `Conflict`; bitácora `SUPPLIER_UPDATED` solo con `AuditChanges.Compare` y sin entrada si no hay cambios
- [X] T035 [P] [US1] Crear el caso de uso `SetSupplierActive` en src/Pos.Application/Suppliers/SetSupplierActive/ (`SetSupplierActiveCommand(Id, ExpectedVersion, Active)`, handler) (depende de T031, T032): siempre permitido (FR-006); bitácora `SUPPLIER_DEACTIVATED` / `SUPPLIER_ACTIVATED` con el cambio de "Estado"; versión → `Conflict`
- [X] T036 [US1] Crear los casos de uso de consulta (depende de T032): `SearchSuppliers` en src/Pos.Application/Suppliers/SearchSuppliers/ (`ManageSuppliers`, texto normalizado con `TextNormalizer.ForSearch`), `GetSupplier` en src/Pos.Application/Suppliers/GetSupplier/ (`ManageSuppliers`, `NotFound`) y `ListSuppliersForPurchase` en src/Pos.Application/Suppliers/ListSuppliersForPurchase/ (`RegisterPurchases`, solo activos, ≤ 50, FR-007)
- [X] T037 [US1] Registrar los handlers y validadores de proveedores en src/Pos.Application/DependencyInjection.cs (depende de T033–T036)
- [X] T038 [US1] Implementar `SupplierRepository` en src/Pos.Infrastructure/Suppliers/SupplierRepository.cs con el molde de `CustomerRepository` y registrarlo en src/Pos.Infrastructure/DependencyInjection.cs (depende de T032, T018): búsqueda por `SearchText` con `IsActive` primero en el índice; `ListActiveAsync` ordenado por nombre; traducción de la violación de `IX_Suppliers_TaxId` en `SaveOutcome`
- [X] T039 [P] [US1] Agregar a src/Pos.Desktop/Resources/Strings.resx los textos `Nav_Suppliers` ("Proveedores") y `Supplier_*`: títulos, columnas (nombre, RUC, teléfono, condiciones de pago, estado), "Nuevo proveedor", "Editar", "Desactivar", "Activar", "Incluir inactivos", "Contado", "Crédito", "Crédito {0} días", "Días de crédito" (contracts/ui.md)
- [X] T040 [US1] Crear la lista de proveedores en src/Pos.Desktop/Purchases/SupplierListViewModel.cs, src/Pos.Desktop/Purchases/SupplierListView.axaml y src/Pos.Desktop/Purchases/SupplierListView.axaml.cs con el molde de `CustomerListView` (depende de T037, T039): búsqueda con retardo, casilla "Incluir inactivos", inactivos atenuados, botones "Nuevo proveedor", "Editar" (doble clic o Enter) y "Desactivar" / "Activar"
- [X] T041 [US1] Crear el formulario en src/Pos.Desktop/Purchases/SupplierFormViewModel.cs con el formulario de 002 (molde `CustomerFormViewModel`) (depende de T040): campos nombre*, RUC, teléfono, email, dirección multilínea y condiciones de pago* (Contado / Crédito); "Días de crédito" habilitado y obligatorio solo con Crédito; errores por campo desde `ValidationFailed`; `SupplierTaxIdInUse` en el campo RUC; `Conflict` con el mensaje estándar de recarga
- [X] T042 [US1] Crear src/Pos.Desktop/Purchases/PurchasesModule.cs con `AddPurchasesModule()` que registra la página `inventory.suppliers` (orden 30, `ManageSuppliers`) en el grupo `InventoryModule.GroupId`, y llamarlo desde src/Pos.Desktop/Composition/HostBuilder.cs después de `AddInventoryModule()` (depende de T040, T041)

**Checkpoint**: catálogo de proveedores funcional y probado (quickstart §2).

---

## Phase 4: User Story 2 - Registrar compra (entrada de mercancía) (Priority: P3, segunda)

**Goal**: registrar una compra con factura, fecha, líneas e impuestos que suma existencia con movimientos "Entrada de compra" en una sola transacción; consultarla y anularla completa.

**Independent Test**: con "Refresco" en existencia 10, registrar 5 a $12.50 y "Queso" 2.5 kg a $40.00 con impuestos $26.00; verificar subtotal $162.50, total $188.50, existencia 15, dos "Entrada de compra" con la factura como referencia y el producto sin cambios; anular y verificar la reversión (quickstart §3 y §4).

### Tests for User Story 2

- [X] T043 [US2] Ampliar tests/Pos.Domain.Tests/Inventory/ProductStockTests.cs (depende de T046): `RecordPurchase` suma y valida cantidad > 0, decimales de la unidad, producto activo, que controla inventario y máximo de existencia; `RecordPurchaseVoid` resta y rechaza existencia menor que la cantidad, producto inactivo y producto que no controla inventario; `Record(...)` rechaza `Purchase` y `PurchaseVoid`
- [X] T044 [US2] Crear `RegisterPurchaseTests` sobre SQLite real en tests/Pos.Infrastructure.Tests/Purchases/RegisterPurchaseTests.cs (depende de T055). Solo reglas que viven en el caso de uso; las de cálculo, bonificación, fecha, impuestos y producto repetido ya se prueban en Domain (T023, T024, T043) (constitución VI: caso válido + caso límite más importante):
  - Existencia exacta (10 + 5 = 15) y dos movimientos `PURCHASE` con `Reference` = factura, enlazados por `PurchaseLine.MovementId`.
  - El producto no cambia (precio y demás datos, SC-002); una segunda compra a $13.00 no cambia el costo de la primera.
  - Subtotal, impuestos y total guardados = suma de líneas y `LineCount` correcto.
  - Factura " f-100" con el mismo proveedor → `DuplicateInvoice`; con otro proveedor se acepta; tras anular la primera, se acepta.
  - Una compra con proveedor inactivo y una línea de un producto que no controla inventario → **un solo** `ValidationFailed` con ambos campos (`SupplierId`, `Lines[{i}].Product`) y **ningún** movimiento ni cambio de existencia.
  - Bitácora `PURCHASE_REGISTERED` con proveedor, factura, subtotal, impuestos y total.
- [X] T045 [US2] Crear `PurchaseAtomicityTests` en tests/Pos.Infrastructure.Tests/Purchases/PurchaseAtomicityTests.cs con el molde de `InventoryAtomicityTests` (depende de T055): fallo inyectado en `SaveChanges` → sin compra, sin movimientos y sin cambio de existencia (SC-006); una compra de 200 líneas se guarda completa con un solo `SaveChanges`. La medición de tiempo (< 1 s) va en una prueba aparte con `[Fact(Explicit = true)]`, como T071, para no depender de la velocidad del equipo de CI

### Implementation for User Story 2 — Domain

- [X] T046 [US2] Agregar a src/Pos.Domain/Inventory/ProductStock.cs (research §2):
  - `Record(...)` rechaza `Purchase` y `PurchaseVoid` igual que hoy rechaza los de venta (FR-014).
  - `RecordPurchase(quantity, unit, productActive, tracksInventory, reference)`: suma; valida cantidad, decimales, producto activo, que controla inventario y máximo de existencia; devuelve el `InventoryMovement` `PURCHASE` con `Reason` nulo.
  - `RecordPurchaseVoid(quantity, productActive, tracksInventory, reference)`: resta; rechaza producto inactivo, que no controla inventario o existencia menor que la cantidad (nunca bajo cero, FR-017b); devuelve el `PURCH_VOID`.

### Implementation for User Story 2 — Application

- [X] T047 [P] [US2] Agregar a src/Pos.Application/Abstractions/Error.cs los errores `DuplicateInvoice(Guid PurchaseId, DateOnly InvoiceDate, DateTime RegisteredAtUtc)` y `PurchaseVoidBlocked(IReadOnlyList<PurchaseVoidBlocker> Lines)`, con `PurchaseVoidBlocker(Guid ProductId, string ProductName, VoidBlockReason Reason, long OnHandThousandths, long RequiredThousandths)` y `VoidBlockReason` = `InsufficientStock` | `ProductInactive` | `NotTracked` (contracts/application-ports.md)
- [X] T048 [P] [US2] Crear src/Pos.Application/Purchases/PurchaseFields.cs (`SupplierId`, `InvoiceNumber`, `InvoiceDate`, `Tax`, `Lines`, `Subtotal`, `Reason` y los de línea `Lines[{i}].Product`, `Lines[{i}].Quantity`, `Lines[{i}].UnitCost`) y src/Pos.Application/Purchases/PurchaseMessages.cs (mensajes en español, incluidos "La compra ya está anulada", el de subtotal $0.00 y los de `PurchaseVoidBlocked`: "{producto}: existencia {actual}, se requieren {cantidad}" / "{producto}: está inactivo; actívelo primero" / "{producto}: ya no controla inventario")
- [X] T049 [P] [US2] Crear src/Pos.Application/Purchases/PurchaseDtos.cs: `PurchaseLineInput { ProductId, QuantityText, UnitCostText }`, `PurchaseTotalsDto { Lines[{ AmountCents?, IsBonus, Errors }], SubtotalCents, TaxCents, TotalCents, Errors }`, `PurchaseRegisteredDto { PurchaseId, SubtotalCents, TaxCents, TotalCents }` y `PurchaseDetailDto` (encabezado con nombres congelados, factura, fecha, usuario y fecha de registro, importes, estado, anulación con fecha, usuario y motivo; líneas con `LineNumber`, producto, SKU, cantidad, unidad, costo, importe, `IsBonus`)
- [X] T050 [US2] Crear el puerto `IPurchaseRepository` en src/Pos.Application/Purchases/IPurchaseRepository.cs (depende de T049): `GetAsync(id)` con líneas y seguimiento, `FindActiveByInvoiceAsync(supplierId, invoiceKey)`, `Add(purchase)`, `GetDetailAsync(id)` → `PurchaseDetailDto`, `SaveChangesAsync()` → `SaveOutcome` (índice de factura → duplicado; versión → conflicto)
- [X] T051 [US2] Ampliar `MovementDto` con `PurchaseId?` y `SupplierName?` en src/Pos.Application/Inventory/IInventoryRepository.cs; el filtro por tipo de `SearchMovements` en src/Pos.Application/Inventory/SearchMovements/ acepta `Purchase` y `PurchaseVoid` (research §3)
- [X] T052 [US2] Crear el caso de uso sin base de datos `CalculatePurchaseTotals` en src/Pos.Application/Purchases/CalculatePurchaseTotals/ (query y handler) (depende de T048, T049): permiso `RegisterPurchases`; entrada `Lines[{ ProductId, DecimalPlaces, QuantityText, UnitCostText }]` y `TaxText?`; interpreta cantidades con los decimales de la unidad y costos e impuestos con `Money.Parse` (vacío = $0.00); usa `PurchaseMath`; devuelve importes, `IsBonus` y errores por línea y generales (research §4). Exponer el núcleo de cálculo como función interna reutilizable por `RegisterPurchase`
- [X] T053 [US2] Crear `RegisterPurchaseValidator` (FluentValidation) en src/Pos.Application/Purchases/RegisterPurchase/RegisterPurchaseValidator.cs (depende de T048, T049): proveedor obligatorio, factura 1–50 tras recortar, fecha obligatoria, al menos una línea, sin `ProductId` repetido, textos de cantidad, costo e impuestos interpretables, con los campos por línea `Lines[{i}].*`
- [X] T054 [US2] Crear el caso de uso `RegisterPurchase` en src/Pos.Application/Purchases/RegisterPurchase/ (`RegisterPurchaseCommand`, `RegisterPurchaseHandler`) siguiendo el **orden** de contracts/application-ports.md (depende de T046, T047, T050, T052, T053, T032, T038):
  1. Permiso `RegisterPurchases` → validador; 2. fecha ≤ `DiscountDates.LocalToday(clock)`; 3. `IWriteTransactions.BeginAsync` (`BEGIN IMMEDIATE`); 4. proveedor existe y activo; 5. cada producto existe, no borrado, activo, controla inventario y la cantidad respeta los decimales de su unidad; 6. importes con el núcleo de `CalculatePurchaseTotals` (subtotal > 0, límites); 7. `FindActiveByInvoiceAsync` → `DuplicateInvoice`; 8. `Purchase.Register(...)` con los nombres, SKU y `UnitCode` congelados; por línea `ProductStock.RecordPurchase` (con `ProductStock.Start` si no hay fila), `AddMovement` y `LinkMovement`; 9. bitácora `PURCHASE_REGISTERED` (`EntityName` "Compra {factura} · {proveedor}"; cambios: Proveedor, Factura, Fecha de factura, Líneas, Subtotal, Impuestos, Total); 10. un solo `SaveChangesAsync` (índice → `DuplicateInvoice`, versión → `Conflict`); 11. `Commit` y Serilog con proveedor, factura, número de líneas, total y usuario.
  - Los errores de campo de los pasos 1–7 se juntan en un solo `ValidationFailed` (escenario 6).
- [X] T055 [US2] Implementar `PurchaseRepository` en src/Pos.Infrastructure/Purchases/PurchaseRepository.cs y registrarlo en src/Pos.Infrastructure/DependencyInjection.cs; registrar `CalculatePurchaseTotals`, `RegisterPurchase` y su validador en src/Pos.Application/DependencyInjection.cs (depende de T050, T054): `GetDetailAsync` sin seguimiento, con el nombre del usuario que registró y del que anuló; traducción de la violación de `IX_Purchases_Supplier_InvoiceKey` a duplicado
- [X] T056 [US2] Crear el caso de uso `VoidPurchase` en src/Pos.Application/Purchases/VoidPurchase/ (`VoidPurchaseCommand(PurchaseId, ExpectedVersion, Reason)`, handler) y registrarlo en src/Pos.Application/DependencyInjection.cs (depende de T046, T047, T055). Orden de research §7: licencia → permiso `VoidPurchases` (no autorizable) → motivo 1–250 → `NotFound` → estado `ACTIVE` (`InvalidState` "La compra ya está anulada") → versión (`Conflict`) → por cada línea producto activo, que controla inventario y existencia ≥ cantidad; si alguna falla, `PurchaseVoidBlocked` con **todas** las líneas afectadas sin cambiar nada (y Serilog con los productos). Si pasa: `RecordPurchaseVoid` por línea, `LinkVoidMovement`, `Purchase.Void`, bitácora `PURCHASE_VOIDED` (`Reason` = motivo; cambios: Estado "Vigente" → "Anulada", Subtotal, Impuestos, Total), un solo `SaveChangesAsync`, `Commit`
- [X] T057 [P] [US2] Crear el caso de uso `GetPurchase` en src/Pos.Application/Purchases/GetPurchase/ (permiso `ViewPurchaseReport` **o** `RegisterPurchases`; `NotFound`) y registrarlo en src/Pos.Application/DependencyInjection.cs (depende de T055)

### Implementation for User Story 2 — Infrastructure (kárdex)

- [X] T058 [US2] En src/Pos.Infrastructure/Inventory/InventoryRepository.cs, `SearchMovementsAsync` hace dos *left join* del movimiento a `PurchaseLines` (por `MovementId` y por `VoidMovementId`) y de ahí a `Purchases`, para llenar `PurchaseId` y `SupplierName` (depende de T051, T018)

### Tests for User Story 2 (dependen de los casos de uso)

- [X] T059 [P] [US2] Crear `PurchaseConcurrencyTests` en tests/Pos.Infrastructure.Tests/Purchases/PurchaseConcurrencyTests.cs con el molde de `InventoryConcurrencyTests` (depende de T055, T056): dos compras simultáneas del mismo producto suman ambas cantidades; dos con el mismo proveedor y factura → solo una se guarda y la otra recibe `DuplicateInvoice`; una anulación y una venta simultáneas del mismo producto cuya existencia solo alcanza para una de las dos → la existencia nunca queda bajo cero, una se rechaza (`PurchaseVoidBlocked` o el error de existencia de la venta) y el kárdex sigue consistente (edge case de spec.md)
- [X] T060 [P] [US2] Crear `VoidPurchaseTests` en tests/Pos.Infrastructure.Tests/Purchases/VoidPurchaseTests.cs (depende de T056): compra de 5 con existencia 8 → existencia 3, movimiento `PURCH_VOID` enlazado en `VoidMovementId`, compra `VOIDED` con fecha, usuario y motivo, bitácora con motivo; existencia 2 → `PurchaseVoidBlocked` con el producto y nada cambia; producto inactivo → bloqueado; sin motivo → `ValidationFailed`; anulación doble → `InvalidState`; Cajero → `Forbidden`; `SearchMovements` devuelve `PurchaseId` y `SupplierName` en ambos movimientos
- [X] T061 [P] [US2] Crear `PurchaseImmutabilityTests` en tests/Pos.Infrastructure.Tests/Purchases/PurchaseImmutabilityTests.cs con el molde de `InventoryImmutabilityTests` (depende de T055, T056): modificar o borrar una `PurchaseLine` lanza `InvalidOperationException` salvo `VoidMovementId` de nulo a valor; borrar una `Purchase`, cambiar su factura o importes, o modificar una `VOIDED` lanza
- [X] T062 [US2] Ampliar tests/Pos.Infrastructure.Tests/Inventory/InventoryConsistencyTests.cs (**obligatoria**) (depende de T056): compras y anulaciones mezcladas con ventas y ajustes mantienen existencia = último `ResultingStock` y secuencias 1..N por producto

### Implementation for User Story 2 — Desktop

- [X] T063 [P] [US2] Agregar a src/Pos.Desktop/Resources/Strings.resx `Nav_PurchaseEntry` ("Entrada de mercancía") y los textos `Purchase_*` de contracts/ui.md: encabezado, columnas, "Bonificación", "Subtotal", "Impuestos", "Total", "Registrar compra", "Descartar", "Compra registrada: {0} factura {1}, total {2}", "Ver compra", "Anular compra", "Se descontarán de la existencia las cantidades de cada línea", "Anulada", "Factura {0} · {1}" y el mensaje genérico de falla
- [X] T064 [US2] Crear src/Pos.Desktop/Purchases/PurchaseLineViewModel.cs y src/Pos.Desktop/Purchases/PurchaseEntryViewModel.cs (depende de T055, T063):
  - Encabezado: proveedor (selector con búsqueda vía `ListSuppliersForPurchase`), factura (máx. 50), fecha (por omisión hoy, sin fechas futuras).
  - Agregar producto con `ProductPickerViewModel` (sobre `SearchStock`); si ya está en la compra, enfoca su línea y selecciona la cantidad (escenario 10).
  - Totales en vivo **solo** con `CalculatePurchaseTotals` en cada cambio; el ViewModel no suma.
  - "Registrar compra" deshabilitado mientras guarda; al terminar muestra el mensaje y limpia; "Descartar" confirma si hay líneas.
  - Errores: `ValidationFailed` marca campos y líneas; `DuplicateInvoice` con fecha y enlace "Ver compra"; falla inesperada con mensaje genérico **conservando la captura** (escenario 12).
- [X] T065 [US2] Crear src/Pos.Desktop/Purchases/PurchaseEntryView.axaml y src/Pos.Desktop/Purchases/PurchaseEntryView.axaml.cs (depende de T064): tabla de líneas (producto y SKU, unidad, cantidad*, costo*, importe, etiqueta "Bonificación", quitar), errores en la celda, pie con subtotal, impuestos y total; teclado: Enter en el buscador agrega y lleva a cantidad, Tab a costo, Enter regresa al buscador (SC-003)
- [X] T066 [US2] Crear el detalle compartido en src/Pos.Desktop/Purchases/PurchaseDetailViewModel.cs, src/Pos.Desktop/Purchases/PurchaseDetailView.axaml y su .axaml.cs (depende de T057, T063): encabezado con datos congelados, registro en hora local, importes; insignia "Anulada" con fecha, usuario y motivo; líneas con núm., producto, SKU, cantidad, unidad, costo, importe y "Bonificación"; sin opciones de edición (escenario 11); botón "Anular compra" visible solo con `VoidPurchases` y compra vigente
- [X] T067 [US2] Crear el diálogo src/Pos.Desktop/Purchases/VoidPurchaseViewModel.cs, src/Pos.Desktop/Purchases/VoidPurchaseView.axaml y su .axaml.cs con el molde de `VoidPaymentView` (depende de T056, T066): motivo* (máx. 250) y aviso; `PurchaseVoidBlocked` como lista de productos con su causa; `InvalidState`; `Conflict` recarga el detalle
- [X] T068 [US2] Registrar en src/Pos.Desktop/Purchases/PurchasesModule.cs la página `inventory.purchases` (orden 20, `RegisterPurchases`) y los ViewModels de entrada, detalle y anulación (depende de T042, T065–T067)
- [X] T069 [US2] Actualizar el kárdex en src/Pos.Desktop/Inventory/MovementsViewModel.cs y src/Pos.Desktop/Inventory/MovementsView.axaml (depende de T058, T066): el filtro por tipo ofrece "Entrada de compra" y "Anulación de compra"; la columna "Referencia" muestra "Factura {número} · {proveedor}" y abre el detalle de la compra con `ViewPurchaseReport` o `RegisterPurchases`

**Checkpoint**: registro, consulta y anulación de compras funcionales y probados (quickstart §3 y §4).

---

## Phase 5: User Story 3 - Reporte de compras (Priority: P3, tercera)

**Goal**: "Reportes > Compras" con filtros por proveedor (incluidos inactivos), fechas de factura y total; 100 por página; acumulados de todo el filtro solo con compras vigentes.

**Independent Test**: registrar tres compras de dos proveedores en fechas distintas; filtrar por un proveedor y un rango de fechas; verificar las filas esperadas, de la más reciente a la más antigua, y los acumulados; abrir una y ver sus líneas (quickstart §5).

### Tests for User Story 3

- [X] T070 [P] [US3] Crear `PurchaseReportTests` sobre SQLite real en tests/Pos.Infrastructure.Tests/Purchases/PurchaseReportTests.cs (depende de T075): con 150 compras, los acumulados (número, subtotal, impuestos, total) son la suma de **todas** las páginas y la página 1 trae 100 filas en orden `InvoiceDate` desc, `CreatedAt` desc, `Id` desc; filtros combinados por proveedor (inactivo incluido), fechas y total; anuladas excluidas por omisión y, con `IncludeVoided`, listadas con `IsVoided` sin sumarse (`TotalCount` sí las cuenta); rango sin compras → acumulados $0.00; fecha inicial > final, mínimo > máximo e importe negativo → `ValidationFailed`
- [X] T071 [P] [US3] Crear `PurchaseReportPerformanceTests` con `[Fact(Explicit = true)]` en tests/Pos.Infrastructure.Tests/Purchases/PurchaseReportPerformanceTests.cs (SC-005) (depende de T075): siembra 10,000 compras y mide la primera página con cada filtro y con todos juntos; cada consulta < 2 s (objetivo < 300 ms)

### Implementation for User Story 3

- [X] T072 [P] [US3] Crear el puerto `IPurchaseReportReader` en src/Pos.Application/Reports/IPurchaseReportReader.cs con `SearchAsync(PurchaseReportFilter)` → `PurchaseReportPage`, y los tipos `PurchaseReportFilter(SupplierId?, FromDate?, ToDate?, MinTotalCents?, MaxTotalCents?, IncludeVoided, Page)` y `PurchaseReportPage { Rows[{ PurchaseId, InvoiceDate, SupplierName, InvoiceNumber, LineCount, SubtotalCents, TaxCents, TotalCents, RegisteredByName, IsVoided }], TotalCount, Page, PageSize = 100, PurchaseCount, SubtotalSumCents, TaxSumCents, TotalSumCents }`
- [X] T073 [US3] Crear el caso de uso `GetPurchaseReport` en src/Pos.Application/Reports/GetPurchaseReport/ (query y handler) (depende de T072): permiso `ViewPurchaseReport`; interpreta `MinTotalText`/`MaxTotalText` con `Money.Parse`; fecha inicial > final, mínimo > máximo, importe negativo o mal escrito → `ValidationFailed` por campo (FR-023, campos en src/Pos.Application/Reports/ReportFields.cs y mensajes en src/Pos.Application/Reports/ReportMessages.cs); página de `ReportPaging.ScreenPageSize`
- [X] T074 [P] [US3] Crear el caso de uso `ListSuppliersForReport` en src/Pos.Application/Suppliers/ListSuppliersForReport/ (permiso `ViewPurchaseReport`; todos los proveedores con `IsActive`, ordenados por nombre, vía `ISupplierRepository.ListForFilterAsync`)
- [X] T075 [US3] Implementar `PurchaseReportReader` en src/Pos.Infrastructure/Reports/PurchaseReportReader.cs y registrarlo en src/Pos.Infrastructure/DependencyInjection.cs; registrar `GetPurchaseReport` y `ListSuppliersForReport` en src/Pos.Application/DependencyInjection.cs (depende de T072–T074): página con el orden de research §13 y el nombre del usuario que registró; acumulados en **una** consulta agregada sobre todo el filtro con `Status = 'ACTIVE'` (`COUNT`, `SUM(SubtotalCents)`, `SUM(TaxCents)`, `SUM(TotalCents)`)
- [X] T076 [P] [US3] Agregar a src/Pos.Desktop/Resources/Strings.resx `Nav_ReportPurchases` ("Compras") y los textos del reporte: filtros, "(inactivo)", "Incluir anuladas", "Aplicar", resumen, columnas y "No hay compras con estos filtros."
- [X] T077 [US3] Crear src/Pos.Desktop/Reports/PurchaseReportViewModel.cs, src/Pos.Desktop/Reports/PurchaseReportView.axaml y su .axaml.cs con el molde de `ReceivablesReportView` (depende de T075, T076, T066): filtros (proveedor con inactivos marcados, desde/hasta, mínimo/máximo, "Incluir anuladas", "Aplicar"); errores de filtro en el campo y sin resultados; resumen de todo el filtro; tabla de 100 por página con anuladas en gris e insignia "Anulada"; sin resultados → mensaje y $0.00; doble clic o Enter abre `PurchaseDetailView`
- [X] T078 [US3] Registrar la página `reports.purchases` (orden 25, `ViewPurchaseReport`) en src/Pos.Desktop/Reports/ReportsModule.cs, entre Inventario (20) y Créditos (30) (depende de T077)

**Checkpoint**: las tres historias funcionan y se prueban de forma independiente.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: base de ejemplo 0.14.0, documentación y validación final.

- [X] T079 Ampliar `SampleDatabaseGenerator` en tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseGenerator.cs con proveedores (activo, inactivo, sin RUC), una compra vigente con bonificación y una anulada, usando los handlers reales; generar tests/Pos.Infrastructure.Tests/SampleDatabases/v0.14.0.db con `POS_GENERATE_SAMPLE_DB=1` (depende de T056)
- [X] T080 Ampliar tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests.cs para cubrir `v0.13.0.db` y `v0.14.0.db` (la segunda conserva proveedores, compras, líneas y sus enlaces a movimientos) (depende de T079)
- [X] T081 [P] Crear docs/compras.md (guía de soporte): proveedores, captura de una compra, bonificaciones, factura duplicada, anulación y por qué puede bloquearse, tipos `PURCHASE` / `PURCH_VOID` en el kárdex, reporte y cómo corregir una compra (anular y registrar de nuevo)
- [X] T082 [P] Agregar la sección 0.14.0 a docs/migraciones.md: migración `SuppliersAndPurchases`, solo `CREATE TABLE` e índices, sin reconstrucciones; tipos nuevos en `InventoryMovements.Type` sin cambiar la tabla; bases `v0.13.0.db` y `v0.14.0.db`
- [X] T083 [P] Actualizar docs/usuarios-y-permisos.md (los cuatro permisos nuevos, solo Administrador, no autorizables, módulo Inventario) y docs/auditoria.md (seis eventos nuevos y el grupo "Proveedores y compras")
- [X] T084 Verificar en src/Pos.Desktop/Purchases/ y src/Pos.Desktop/Reports/PurchaseReportViewModel.cs que ningún ViewModel calcula importes, subtotales, impuestos ni totales (Principio III) y que ningún caso de uso nuevo genera movimientos `PURCHASE` / `PURCH_VOID` fuera de `RegisterPurchase` y `VoidPurchase` (FR-014)
- [X] T085 Ejecutar `dotnet build -v q` (0 advertencias) y `dotnet test --verbosity quiet` desde la raíz, incluidas las pruebas de arquitectura
- [ ] T086 Validación final con quickstart.md §1–§5 en la aplicación (Administrador y Cajero) y §7 (`PurchaseReportPerformanceTests` en Release)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 va **primero** (genera `v0.13.0.db` con el código actual); T002 después.
- **Foundational (Phase 2)**: depende de Setup. Bloquea todas las historias (esquema y migración únicos).
- **US1 (Phase 3)**: depende de Foundational.
- **US2 (Phase 4)**: depende de Foundational y de US1 (`ListSuppliersForPurchase` T036, `PurchasesModule` T042, y proveedores para registrar compras).
- **US3 (Phase 5)**: depende de Foundational, de US1 (`ISupplierRepository`) y de US2 (compras registradas, `PurchaseDetailView` T066).
- **Polish (Phase 6)**: depende de las tres historias.

### User Story Dependencies

- **US1**: independiente después de Foundational.
- **US2**: usa proveedores de US1; se prueba sola sembrando proveedores con `CreateSupplier`.
- **US3**: lee compras de US2; se prueba sola sembrando compras con `RegisterPurchase`.

### Within Each User Story

- Errores, campos y DTOs → puertos → casos de uso → repositorios → registro en DI → pruebas sobre SQLite → textos → ViewModels → vistas → módulo.
- En US2: `ProductStock` (T046) antes de `RegisterPurchase` y `VoidPurchase`; `CalculatePurchaseTotals` (T052) antes de `RegisterPurchase` (comparten el núcleo de cálculo).

### Parallel Opportunities

- Phase 2: T003, T005, T006, T007, T009, T010 en paralelo; luego T015–T017; las pruebas T020–T024 en paralelo.
- US1: T028–T031 en paralelo; T035 en paralelo con T034; T039 en paralelo con la capa Application.
- US2: T047–T049 en paralelo; T057 en paralelo con T056; las pruebas T059–T061 en paralelo; T063 en paralelo con Application.
- US3: T072 y T074 en paralelo; T070 y T071 en paralelo; T076 en paralelo con Application.
- Polish: T081–T083 en paralelo.

---

## Parallel Example: Foundational

```bash
Task: "Agregar los cuatro permisos en src/Pos.Domain/Users/Permission.cs"
Task: "Crear EmailAddress en src/Pos.Domain/Common/EmailAddress.cs"
Task: "Agregar PURCHASE y PURCH_VOID en src/Pos.Domain/Inventory/MovementType.cs"
Task: "Crear PurchaseMath en src/Pos.Domain/Purchases/PurchaseMath.cs"
```

## Parallel Example: User Story 1

```bash
Task: "Agregar SupplierTaxIdInUse en src/Pos.Application/Abstractions/Error.cs"
Task: "Crear SupplierFields.cs y SupplierMessages.cs en src/Pos.Application/Suppliers/"
Task: "Crear SupplierDtos.cs en src/Pos.Application/Suppliers/"
Task: "Crear SupplierAuditFields.cs en src/Pos.Application/Suppliers/"
```

## Parallel Example: User Story 2

```bash
# Después de T055 y T056:
Task: "PurchaseConcurrencyTests en tests/Pos.Infrastructure.Tests/Purchases/"
Task: "VoidPurchaseTests en tests/Pos.Infrastructure.Tests/Purchases/"
Task: "PurchaseImmutabilityTests en tests/Pos.Infrastructure.Tests/Purchases/"
```

## Parallel Example: User Story 3

```bash
Task: "Crear IPurchaseReportReader en src/Pos.Application/Reports/IPurchaseReportReader.cs"
Task: "Crear ListSuppliersForReport en src/Pos.Application/Suppliers/ListSuppliersForReport/"
Task: "Agregar textos del reporte en src/Pos.Desktop/Resources/Strings.resx"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1: Setup (T001 antes de tocar el modelo).
2. Phase 2: Foundational, con la migración revisada y sus pruebas.
3. Phase 3: US1 — catálogo de proveedores.
4. **Validar** con quickstart §2.

Nota: US1 sola da poco valor al negocio; el incremento útil mínimo real es **US1 + US2** (registrar compras con inventario y anulación).

### Incremental Delivery

1. Setup + Foundational → esquema listo.
2. US1 → proveedores (quickstart §2).
3. US2 → compras, kárdex y anulación (quickstart §3–§4).
4. US3 → reporte (quickstart §5).
5. Polish → base 0.14.0, documentación, validación final.

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes. Varias tareas tocan src/Pos.Desktop/Resources/Strings.resx o los `DependencyInjection.cs`: no ejecutarlas a la vez.
- Cada escritura es una transacción `BEGIN IMMEDIATE` con su entrada de bitácora y un solo `SaveChangesAsync`.
- Nada de pruebas de ViewModels, vistas ni DTOs.
- Hacer commit al terminar cada tarea o grupo lógico.
