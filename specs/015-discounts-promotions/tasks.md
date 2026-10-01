---

description: "Task list for 015 Descuentos y promociones"
---

# Tasks: Descuentos y promociones

**Input**: Design documents from `/specs/015-discounts-promotions/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (application-ports.md, ui.md, ticket-format.md), quickstart.md

**Tests**: Incluidas, solo las que fija la política mínima de la constitución v1.2.0 y enumera research §14 (cálculos de dinero, validaciones de integridad, migración, inventario y arquitectura). Sin pruebas de ViewModels, vistas ni ticket. La persistencia se prueba sobre SQLite real (las pruebas de casos de uso con SQLite viven en `tests/Pos.Infrastructure.Tests`, como `CancelSaleTests`). Al implementar, ejecutar solo las pruebas del proyecto modificado (`dotnet test tests/<Proyecto> --verbosity quiet`).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- Include exact file paths in descriptions

## Path Conventions

Arquitectura por capas (Principio II): `src/Pos.Domain`, `src/Pos.Application`, `src/Pos.Infrastructure`, `src/Pos.Desktop`, y `tests/Pos.*.Tests`. Carpeta de funcionalidad `Discounts/` en cada capa; los cambios de la venta quedan en `Sales/`.

Convenciones transversales (aplican a todas las tareas):

- Importes en centavos (`long`), porcentajes en puntos base (`int`, 10 000 = 100 %). Nunca `double`/`float`.
- Identificadores `Guid.CreateVersion7`; fechas UTC, salvo `Coupon.StartsOn`/`EndsOn` (`DateOnly` local).
- Cada caso de uso devuelve `Result`/`Result<T>`, verifica el permiso con `IAccessControl` y responde `ModuleNotLicensed(Discounts)` si el módulo está inactivo.
- Los ViewModels no calculan importes: los leen del `Cart` (Domain) o de los casos de uso (Principio III).
- Mensajes al operador en español, sin detalles técnicos.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Preparar la versión y confirmar la línea base

- [X] T001 Cambiar `<Version>` de `0.9.0` a `0.10.0` en Directory.Build.props
- [X] T002 Verificar la línea base: `dotnet build -v q` y `dotnet test --verbosity quiet` terminan sin errores ni advertencias antes de empezar

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Value objects, cálculo, entidades, licencia, permisos, persistencia y la migración única `DiscountsAndCoupons`, que todas las historias necesitan

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Domain

- [X] T003 [P] Crear los enums `DiscountMode` (`Percent`, `Amount`; códigos de BD `PERCENT`/`AMOUNT`), `DiscountScope` (`Line`, `Order`; códigos `LINE`/`ORDER`) y `DiscountKind` (`Line`, `Order`, `Coupon`; códigos `LINE`/`ORDER`/`COUPON`) en src/Pos.Domain/Discounts/DiscountMode.cs, src/Pos.Domain/Discounts/DiscountScope.cs y src/Pos.Domain/Discounts/DiscountKind.cs
- [X] T004 [P] Crear el value object `DiscountValue(DiscountMode Mode, long Raw)` en src/Pos.Domain/Discounts/DiscountValue.cs: `Percent` exige Raw 1–10 000 pb; `Amount` exige Raw 1–`Money.MaxCents`. `Parse(mode, text)` acepta como máximo 2 decimales y nunca redondea ("12.5" → 1250 pb; más de 2 decimales es error, como `Money.Parse`). `ToString` muestra "10%", "12.5%" o "$15.00"
- [X] T005 Crear `DiscountMath` en src/Pos.Domain/Discounts/DiscountMath.cs (depende de T003, T004): `Amount(baseCents, value)` = `(base × pb + 5 000) / 10 000` para porcentaje y `value` para monto, lanza `DomainException` si el resultado es mayor que la base o si es 0 centavos ("El descuento resulta en $0.00"); `ExceedsLimit(discountCents, baseCents, limitBp)` = `discount × 10 000 > limit × base` (multiplicación cruzada entera; igual al límite NO supera; con base 0 no hay descuento posible); `EquivalentBasisPoints(discountCents, baseCents)` redondeado hacia arriba
- [X] T006 [P] Extraer `ReturnMath.Allocate` (reparto por resto mayor, empate por orden, topes por elemento, suma exacta) a `Proportional.Allocate` en src/Pos.Domain/Common/Proportional.cs y hacer que src/Pos.Domain/Returns/ReturnMath.cs delegue en él sin cambiar su comportamiento
- [X] T007 [P] Crear `CouponStatus` (`Active`, `NotStarted`, `Expired`, `Exhausted`, `Inactive`) y el agregado `Coupon` en src/Pos.Domain/Discounts/CouponStatus.cs y src/Pos.Domain/Discounts/Coupon.cs: `Code` recortado y en mayúsculas, `[A-Z0-9-]{3,30}`; `Value` (`DiscountValue`); `StartsOn`/`EndsOn` `DateOnly` inclusivos con `EndsOn ≥ StartsOn`; `UsageLimit` "> 0 o nulo (sin límite)"; `UsesCount` "≥ 0 y ≤ `UsageLimit`"; `IsActive`; campos de auditoría y `Version` (`DeletedAt` siempre nulo). `Status(today)` en este orden: `Inactive` > `Exhausted` (`UsesCount ≥ UsageLimit`) > `Expired` (`today > EndsOn`) > `NotStarted` (`today < StartsOn`) > `Active`. Métodos: `Create`, `Update(startsOn, endsOn, usageLimit)` (no permite límite menor que `UsesCount`), `Edit(code, value)` (lanza si `UsesCount > 0`), `Deactivate`, `Activate`, `ConsumeUse(today)` (exige `Active`), `ReleaseUse()` (no baja de 0)
- [X] T008 [P] Crear la entidad inmutable `DiscountApproval` en src/Pos.Domain/Discounts/DiscountApproval.cs: `Id`, `DraftId`, `RequestedBy`, `AuthorizedBy`, `Scope` (`DiscountScope`), `ProductId` ("Obligatorio si `Scope = LINE`"), `ApprovedBasisPoints` ("entre 1 y 10 000"), `CreatedAt` UTC; sin `Version`, sin `UpdatedAt`/`UpdatedBy`, sin borrado lógico (Complexity Tracking). Método `Covers(scope, productId, equivalentBp)` = mismo alcance/producto y `ApprovedBasisPoints ≥ equivalentBp`
- [X] T009 [P] Crear la entidad inmutable `SaleDiscount` en src/Pos.Domain/Sales/SaleDiscount.cs: `Id`, `SaleId`, `SaleLineId` ("Obligatorio si `Kind = LINE`"), `Kind`, `Mode`, `Value` (valor capturado), `AmountCents` ("> 0"), `CouponId` (si `Kind = COUPON`), `CouponCode` (TEXT(30), copia del código al vender), `AppliedBy`, `AuthorizedBy` (nulo si no se superó el límite), `CreatedAt` UTC
- [X] T010 Ampliar `SaleLine` en src/Pos.Domain/Sales/SaleLine.cs con `OriginalAmountCents` (cantidad × precio), `LineDiscountCents` ("≤ `OriginalAmountCents`", default 0) y `OrderDiscountCents` (default 0); `AmountCents` pasa a ser el **importe neto** = Original − Línea − Venta ≥ 0. Sin descuentos, `OriginalAmountCents = AmountCents`
- [X] T011 Cambiar `Sale.Register(..., discounts)` en src/Pos.Domain/Sales/Sale.cs (depende de T009, T010): agrega `DiscountCents` y la colección `Discounts`; exige `total = Σ AmountCents` con total ≥ 0; con total 0 no lleva pagos; exige `DiscountCents = Σ (Original − Amount)` de las líneas y `= Σ SaleDiscount.AmountCents`
- [X] T012 Cambiar `Checkout.CanConfirm` en src/Pos.Domain/Sales/Checkout.cs para aceptar total 0 sin pagos (research §6)
- [X] T013 [P] Agregar `LicensedModule.Discounts` en src/Pos.Domain/Licensing/LicensedModule.cs, su GUID fijo nuevo (generado una sola vez con `Guid.CreateVersion7` y pegado como literal) y el nombre "Descuentos y promociones" en src/Pos.Domain/Licensing/ModuleCatalog.cs, incluido en el período de evaluación, y mapear los 4 permisos nuevos a ese módulo en src/Pos.Domain/Licensing/ModuleAccess.cs
- [X] T014 [P] Agregar los permisos `ApplyDiscounts` (Cajero y Administrador), `ApproveDiscounts` (Administrador, en el conjunto `Authorizable`), `ManageDiscounts` (Administrador) y `ViewDiscountReport` (Administrador) en src/Pos.Domain/Users/Permission.cs y src/Pos.Domain/Users/RolePermissions.cs

### Domain tests

- [X] T015 [P] Pruebas de `DiscountValue.Parse` (rango válido y límite: 0, 100.00, 100.01, 3 decimales) en tests/Pos.Domain.Tests/Discounts/DiscountValueTests.cs
- [X] T016 [P] Pruebas de `DiscountMath` (99.99 × 15 % = 1 500 centavos → final 84.99; monto mayor que la base lanza; 1 % de $0.10 (redondea a 0) lanza; `ExceedsLimit` con descuento igual al límite = false y un centavo más = true) en tests/Pos.Domain.Tests/Discounts/DiscountMathTests.cs
- [X] T017 [P] Pruebas de `Proportional.Allocate` (suma exacta, resto mayor con empate por orden) en tests/Pos.Domain.Tests/Common/ProportionalTests.cs; confirmar que tests/Pos.Domain.Tests/Returns/ReturnMathTests.cs sigue pasando
- [X] T018 [P] Pruebas de `Sale.Register` con total 0 sin pagos y con `DiscountCents` inconsistente (rechazo) en tests/Pos.Domain.Tests/Sales/SaleTests.cs
- [X] T019 [P] Actualizar tests/Pos.Domain.Tests/Users/RolePermissionsTests.cs y tests/Pos.Domain.Tests/Licensing/ModuleAccessTests.cs (y ModuleCatalogTests.cs si enumera módulos) con los 4 permisos y el módulo `Discounts`

### Application (puertos, DTOs, bitácora)

- [X] T020 [P] Agregar las 7 acciones `DISCOUNT_AUTHORIZED`, `DISCOUNT_APPLIED_AUTHORIZED`, `COUPON_CREATED`, `COUPON_UPDATED`, `COUPON_DEACTIVATED`, `COUPON_USE_RELEASED` y `DISCOUNT_LIMIT_CHANGED` en src/Pos.Application/Audit/AuditActions.cs, y sus etiquetas en español en src/Pos.Desktop/Administration/AuditLogViewModel.cs
- [X] T021 [P] Crear `DiscountSettings { LimitBasisPoints = 1000 }` (rango 0–10 000) e `IDiscountSettingsStore { DiscountSettings Load(); void Save(DiscountSettings settings); }` en src/Pos.Application/Discounts/DiscountSettings.cs y src/Pos.Application/Discounts/IDiscountSettingsStore.cs
- [X] T022 [P] Crear los puertos `ICouponRepository` (`GetAsync`, `FindByCodeAsync(normalizedCode)`, `SearchAsync(text, status, today, page)`, `Add`, `SaveChangesAsync` → `SaveOutcome`), `IDiscountApprovalStore` (`Add`, `ListForDraftAsync(draftId, requestedBy)`) e `IDiscountReportReader` (`ReadAsync(DiscountReportQuery, ReportWindow)`) según contracts/application-ports.md en src/Pos.Application/Discounts/ICouponRepository.cs, src/Pos.Application/Discounts/IDiscountApprovalStore.cs y src/Pos.Application/Discounts/IDiscountReportReader.cs
- [X] T023 [P] Crear src/Pos.Application/Discounts/DiscountDtos.cs (`DiscountApprovalDto`, `CouponLookupDto`, `CouponListItemDto`, `CouponPage`, `CouponDetailDto`, `DiscountReportQuery`, `DiscountReport`, `DiscountReportRow` según contracts/application-ports.md), src/Pos.Application/Discounts/DiscountMessages.cs (mensajes de cupón de contracts/ui.md, aviso de descuento global retirado y de borrador sin licencia) y src/Pos.Application/Discounts/DiscountFields.cs
- [X] T024 Agregar `ExistsWithCodeAsync(string code)` (código de barras o SKU de productos no borrados, comparación sin mayúsculas ni espacios) a `IProductRepository` en src/Pos.Application/Products/ y su implementación en src/Pos.Infrastructure/Products/ProductRepository.cs
- [X] T025 Extender el contrato de `ConfirmSale` en src/Pos.Application/Sales/ConfirmSale/ConfirmSaleCommand.cs: `ConfirmLineInput.Discount: LineDiscountInput?(Mode, Value, ApprovalId?)` y `ConfirmSaleCommand.OrderDiscount: OrderDiscountInput?` (manual con `Mode`, `Value`, `ApprovalId?`, o `CouponCode`); errores nuevos `DiscountApprovalRequired(scope, productId?)`, `CouponNotValid(status)` y `OrderDiscountRemoved` en src/Pos.Application/Sales/SaleMessages.cs
- [X] T026 Permitir venta de total 0: src/Pos.Application/Sales/ConfirmSale/ConfirmSaleValidator.cs deja de exigir pagos; src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs exige pagos si el total es mayor que 0 y rechaza una venta a crédito con total 0. Si el comando trae descuentos y el módulo está inactivo, responde `ModuleNotLicensed(Discounts)`

### Infrastructure (persistencia y migración)

- [X] T027 [P] Crear `PreferencesDiscountSettingsStore` (clave `discounts`, igual que PreferencesReturnsSettingsStore) en src/Pos.Infrastructure/Discounts/PreferencesDiscountSettingsStore.cs
- [X] T028 [P] Crear src/Pos.Infrastructure/Persistence/Configurations/CouponConfiguration.cs: tabla `Coupons`, `Code` TEXT(30) con **índice único**, `Mode` TEXT(10), `Value` INTEGER, `StartsOn`/`EndsOn` como fecha, `UsageLimit` INTEGER NULL, `UsesCount`, `IsActive`, auditoría y `Version` como token de concurrencia
- [X] T029 [P] Crear src/Pos.Infrastructure/Persistence/Configurations/DiscountApprovalConfiguration.cs: tabla `DiscountApprovals`, índice por `DraftId`, `Scope` TEXT(10), `ProductId` NULL, sin `Version`
- [X] T030 [P] Crear src/Pos.Infrastructure/Persistence/Configurations/SaleDiscountConfiguration.cs: tabla `SaleDiscounts`, FK `SaleId` → `Sales` (Restrict, tabla nueva) con índice, `SaleLineId` NULL, `Kind`/`Mode` TEXT(10), `CouponId` NULL, `CouponCode` TEXT(30) NULL, índice por `CreatedAt`
- [X] T031 Agregar `Sales.DiscountCents` (default 0) en src/Pos.Infrastructure/Persistence/Configurations/SaleConfiguration.cs, `SaleLines.OriginalAmountCents`, `LineDiscountCents` y `OrderDiscountCents` (default 0) en src/Pos.Infrastructure/Persistence/Configurations/SaleLineConfiguration.cs, la navegación `Sale.Discounts` y los `DbSet` de `Coupons`, `DiscountApprovals` y `SaleDiscounts` en src/Pos.Infrastructure/Persistence/PosDbContext.cs
- [X] T032 Generar la migración `DiscountsAndCoupons` en src/Pos.Infrastructure/Persistence/Migrations/ (`dotnet ef migrations add DiscountsAndCoupons`), agregar `migrationBuilder.Sql("UPDATE SaleLines SET OriginalAmountCents = AmountCents")` después de los `AddColumn`, y revisar el SQL generado: solo `CREATE TABLE`, `CREATE INDEX`, `ALTER TABLE ... ADD` y el `UPDATE`; sin `DROP TABLE` ni `ef_temp_` (docs/migraciones.md)
- [X] T033 Persistir y cargar `Sale.Discounts` junto con la venta en src/Pos.Infrastructure/Sales/SaleRepository.cs
- [X] T034 Prueba de migración en tests/Pos.Infrastructure.Tests/SampleDatabases/DiscountsMigrationTests.cs: el SQL no contiene `DROP TABLE` ni `ef_temp_`, y al migrar `v0.9.0.db` cada `SaleLines.OriginalAmountCents = AmountCents`, `LineDiscountCents = OrderDiscountCents = 0` y `Sales.DiscountCents = 0`
- [X] T035 Registrar `IDiscountSettingsStore` en src/Pos.Infrastructure/DependencyInjection.cs

### Borrador (venta conservada)

- [X] T036 Extender el borrador de forma compatible hacia atrás en src/Pos.Application/Sales/SaleDtos.cs: `DraftLineDto.Discount?` (`mode`, `value`, `approvalId?`), `StoredDraft.OrderDiscount?` (`{ kind: "MANUAL", mode, value, approvalId? }` o `{ kind: "COUPON", code }`), `RecoveredDraft.DiscountsDropped: bool` y los descuentos en `RecoveredLineDto`; ajustar la serialización JSON en src/Pos.Infrastructure/Sales/SqliteSaleDraftStore.cs para leer borradores viejos sin estos campos
- [X] T037 Guardar y recuperar los descuentos en src/Pos.Application/Sales/SaveSaleDraft/SaveSaleDraftHandler.cs y src/Pos.Application/Sales/GetSaleDraft/GetSaleDraftHandler.cs; al recuperar con el módulo inactivo, descartar descuentos y cupón y devolver `DiscountsDropped = true`
- [X] T038 Agregar una prueba en tests/Pos.Infrastructure.Tests/Sales/SaleDraftTests.cs: un borrador JSON viejo (sin descuentos) se lee igual, y uno con descuentos se recupera con `DiscountsDropped = true` si el módulo está inactivo

**Checkpoint**: `dotnet build -v q` sin advertencias; Domain, Infrastructure y Architecture tests en verde. Una venta sin descuentos se registra y se imprime exactamente igual que antes.

---

## Phase 3: User Story 1 - Descuento por línea (Priority: P2) 🎯 MVP

**Goal**: El cajero aplica a una línea un descuento en % o $, con autorización persistente de Administrador si supera el límite; la línea muestra original tachado e importe final; el cobro guarda el descuento con aplicador y autorizador.

**Independent Test**: Quickstart 1–4 y 15: vender 2 × A, aplicar 10 % (sin autorización, $90.00), cambiar a $15.00 (pide autorización; contraseña incorrecta no cambia nada), 3 × B al 15 % = $84.99, cobrar y revisar `SaleDiscounts` y la bitácora; cerrar sesión y retomar la venta conservada con su descuento.

### Tests for User Story 1

- [X] T039 [P] [US1] Pruebas de `Cart` con descuento de línea en tests/Pos.Domain.Tests/Sales/CartTests.cs: 2 × 50.00 al 10 % = 90.00; monto mayor que el importe rechazado; `SetQuantity` recalcula el porcentaje; bajar la cantidad con monto fijo que supera el nuevo importe se rechaza y conserva la cantidad anterior; 100 % deja total 0 y `CanCheckout` = true
- [X] T040 [P] [US1] Pruebas de `ConfirmSale` con aprobaciones sobre SQLite real en tests/Pos.Infrastructure.Tests/Discounts/ConfirmSaleApprovalTests.cs: dentro del límite sin aprobación cobra; sobre el límite sin aprobación → `DiscountApprovalRequired`; aprobación de otro `DraftId` o con `ApprovedBasisPoints` menor que el equivalente actual → `DiscountApprovalRequired`; aprobación que cubre → cobra y guarda `SaleDiscount` con `AuthorizedBy`; Administrador que vende sobre el límite sin aprobación → `DiscountApprovalRequired`; límite bajado después de aplicar un descuento sin aprobación → `DiscountApprovalRequired`; 100 % con aprobación cobra con total 0 y sin pagos
- [X] T041 [P] [US1] Prueba de `ApproveDiscount` en tests/Pos.Infrastructure.Tests/Discounts/ApproveDiscountTests.cs: dentro del límite → `ApprovalNotNeeded` y no guarda nada; Cajero con concesión vencida → `AuthorizationRequired`; concesión válida guarda `DiscountApproval` y `DISCOUNT_AUTHORIZED` en la misma transacción y consume la concesión

### Implementation for User Story 1

- [X] T042 [US1] Agregar a `CartLine` el registro `LineDiscount(DiscountValue Value, Guid? ApprovalId)` y los derivados `OriginalAmount` (el `Amount` actual), `LineDiscountAmount` (`DiscountMath.Amount(OriginalAmount, Discount.Value)`) y `NetBeforeOrder` en src/Pos.Domain/Sales/CartLine.cs
- [X] T043 [US1] En src/Pos.Domain/Sales/Cart.cs: `SetLineDiscount(productId, LineDiscount?)` (valida con `DiscountMath`, `null` quita), `SetQuantity` recalcula y rechaza si un monto fijo supera el nuevo importe o si un porcentaje pasa a redondear a $0.00, `Subtotal` = Σ `NetBeforeOrder`, `Total` ≥ 0, `CanCheckout` = hay líneas y ninguna no disponible (el total puede ser 0) (depende de T042)
- [X] T044 [P] [US1] Implementar `DiscountApprovalStore` en src/Pos.Infrastructure/Discounts/DiscountApprovalStore.cs
- [X] T045 [US1] Crear el caso de uso `ApproveDiscount` en src/Pos.Application/Discounts/ApproveDiscount/ (ApproveDiscountCommand.cs, ApproveDiscountValidator.cs, ApproveDiscountHandler.cs) según contracts/application-ports.md: calcula el equivalente con `DiscountMath`; si no supera el límite vigente responde `ApprovalNotNeeded`; exige `ApproveDiscounts` (Administrador por rol o Cajero con concesión `GrantId` válida, consumida con `IAuthorizationGrants`; si no, `AuthorizationRequired`); guarda `DiscountApproval` y `DISCOUNT_AUTHORIZED` (DraftId, alcance, modalidad, valor, equivalente, autorizador) en una transacción; registra en Serilog sin la contraseña; no modifica la venta. Para los intentos fallidos (FR-019), agregar a `AuthorizeAdminCommand` en src/Pos.Application/Users/AuthorizeAdmin/AuthorizeAdminCommand.cs un `string? Context` opcional (descripción del descuento; nunca incluye la contraseña) que src/Pos.Application/Users/AuthorizeAdmin/AuthorizeAdminHandler.cs agrega al detalle de `ADMIN_AUTHORIZATION_DENIED` y `ADMIN_AUTHORIZATION_GRANTED`; sin `Context` el detalle queda igual que hoy
- [X] T046 [P] [US1] Crear `GetDiscountSettings` (requiere `ApplyDiscounts`) y `SaveDiscountSettings` (requiere `ManageDiscounts`, `LimitBasisPoints` "de 0 a 10 000", registra `DISCOUNT_LIMIT_CHANGED` con valor anterior y nuevo) en src/Pos.Application/Discounts/Settings/GetDiscountSettings/ y src/Pos.Application/Discounts/Settings/SaveDiscountSettings/
- [X] T047 [US1] Registrar `ApproveDiscount`, `GetDiscountSettings` y `SaveDiscountSettings` en src/Pos.Application/DependencyInjection.cs e `IDiscountApprovalStore` en src/Pos.Infrastructure/DependencyInjection.cs
- [X] T048 [US1] En src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs, descuentos de línea: reconstruir el `Cart` con `LineDiscountInput`, leer el límite vigente con `IDiscountSettingsStore`, leer `IDiscountApprovalStore.ListForDraftAsync(draftId, usuario)` como máximo una vez, y por cada descuento que supere el límite tomar la aprobación indicada por su `ApprovalId`, verificar que sea del mismo `DraftId`, solicitante, alcance y producto y que cubra el equivalente actual (`DiscountApproval.Covers`), o responder `DiscountApprovalRequired(Line, productId)`; no hay excepción para el Administrador: también necesita su aprobación (creada por `ApproveDiscount` sin concesión), y `AuthorizedBy` sale siempre de la aprobación; llenar `SaleLine.OriginalAmountCents`/`LineDiscountCents`, crear `SaleDiscount` `Kind = LINE` con `AppliedBy` y `AuthorizedBy`, registrar `DISCOUNT_APPLIED_AUTHORIZED` (folio, monto, aplicador, autorizador) por cada descuento autorizado, todo en la transacción `BEGIN IMMEDIATE` existente
- [X] T049 [P] [US1] Crear el diálogo de descuento (modalidad % o $, valor, vista previa del importe final obtenida del `Cart`, botón "Quitar descuento") en src/Pos.Desktop/Sales/DiscountDialogView.axaml, src/Pos.Desktop/Sales/DiscountDialogView.axaml.cs y src/Pos.Desktop/Sales/DiscountDialogViewModel.cs
- [X] T050 [US1] En src/Pos.Desktop/Sales/PointOfSaleViewModel.cs: acción "Descuento" en la línea (botón y atajo **F7**), visible solo con `ApplyDiscounts` y módulo activo; si no supera el límite aplica; si lo supera y es Cajero abre `AdminAuthorizationService` (007) pasando `Context` = "Descuento {línea|venta}, {valor}, {monto}, borrador {DraftId}" (sin contraseña) y después llama a `ApproveDiscount` con la concesión; si es Administrador llama a `ApproveDiscount` sin concesión; aplica con el `ApprovalId`; con cancelación o contraseña incorrecta la línea no cambia; "Quitar descuento" sin autorización
- [X] T051 [US1] En src/Pos.Desktop/Sales/PointOfSaleView.axaml: la línea con descuento muestra el importe original **tachado**, la etiqueta "-10%" o "-$15.00" y el importe final en negrita
- [X] T052 [US1] Al cobrar, manejar `DiscountApprovalRequired` en src/Pos.Desktop/Sales/PointOfSaleViewModel.cs y src/Pos.Desktop/Sales/CheckoutViewModel.cs: señalar la línea afectada, abrir la autorización y reintentar el cobro; con total 0, el cobro muestra "Total $0.00" y confirma sin pagos
- [X] T053 [US1] Enviar los descuentos de línea y su `ApprovalId` en el autosave (src/Pos.Desktop/Sales/DraftAutosaver.cs) y restaurarlos al retomar la venta; si `DiscountsDropped`, mostrar "Los descuentos de la venta conservada se quitaron porque el módulo Descuentos y promociones no está activo."
- [X] T054 [P] [US1] Crear la vista de configuración "Descuento máximo sin autorización (%)" con 2 decimales en src/Pos.Desktop/Discounts/DiscountSettingsView.axaml, src/Pos.Desktop/Discounts/DiscountSettingsView.axaml.cs y src/Pos.Desktop/Discounts/DiscountSettingsViewModel.cs
- [X] T055 [US1] Crear src/Pos.Desktop/Discounts/DiscountsModule.cs con el menú "Descuentos > Configuración" (solo `ManageDiscounts` y módulo activo), registrarlo en src/Pos.Desktop/Navigation/NavigationRegistry.cs y en src/Pos.Desktop/Composition/HostBuilder.cs, siguiendo docs/agregar-funcionalidad.md §7

**Checkpoint**: User Story 1 funciona sola (quickstart 1–4, 15). `dotnet test tests/Pos.Domain.Tests` y `tests/Pos.Infrastructure.Tests` en verde.

---

## Phase 4: User Story 2 - Descuento en la venta total (Priority: P2)

**Goal**: Descuento global manual en % o $ sobre el subtotal ya descontado, con el mismo control de autorización, repartido entre las líneas al cobrar por resto mayor.

**Independent Test**: Quickstart 6, 7 y 14: subtotal $150.00 con $15.00 (exactamente 10 %, sin autorización) = $135.00; subirlo a $60.00 con autorización y quitar C → se retira con aviso; devolución parcial de una línea con descuento global devuelve lo pagado.

### Tests for User Story 2

- [X] T056 [P] [US2] Pruebas de `Cart` con descuento global en tests/Pos.Domain.Tests/Sales/CartTests.cs: porcentaje sobre el subtotal ya descontado; recálculo al cambiar cantidades; `Manual` de monto que supera el nuevo subtotal se retira y devuelve `OrderDiscountRemoved`; `Allocation()` suma exactamente el descuento y la suma de netos es igual al total
- [X] T057 [P] [US2] Prueba sobre SQLite real en tests/Pos.Infrastructure.Tests/Discounts/ConfirmSaleOrderDiscountTests.cs: venta con descuento global sobre el límite exige aprobación con `Scope = Order`; al cobrar, Σ `SaleLine.AmountCents` = `Sale.TotalCents`, Σ `OrderDiscountCents` = descuento global y `Sale.DiscountCents` = Σ `SaleDiscount.AmountCents`; una venta a crédito verifica el límite de crédito con el total ya descontado
- [X] T058 [P] [US2] Prueba de devolución parcial (013) de una línea con descuento global en tests/Pos.Infrastructure.Tests/Returns/ReturnsWithDiscountsTests.cs: el monto devuelto es exacto y neto de los descuentos de línea y de la parte repartida

### Implementation for User Story 2

- [X] T059 [US2] Crear la unión `OrderDiscount` con `Manual(DiscountValue Value, Guid? ApprovalId)` y `CouponApplied(Guid CouponId, string Code, DiscountValue Value)` en src/Pos.Domain/Sales/OrderDiscount.cs
- [X] T060 [US2] En src/Pos.Domain/Sales/Cart.cs: `SetOrderDiscount(OrderDiscount?)` (reemplaza el anterior; un solo descuento de venta; rechaza un `Manual` de monto mayor que el subtotal actual o que redondee a $0.00, como en la línea), `OrderDiscountAmount` (porcentaje sobre `Subtotal`; un cupón de monto se limita al subtotal), recálculo tras cada cambio que retira un `Manual` de monto mayor que el subtotal y devuelve `OrderDiscountRemoved`, `Total = Subtotal − OrderDiscountAmount`, y `Allocation()` con `Proportional.Allocate` sobre `NetBeforeOrder` (resto mayor, empate por orden de captura) (depende de T059)
- [X] T061 [US2] En src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs, descuento global manual: aplicarlo al `Cart`, validar el límite contra el subtotal y la aprobación indicada por su `ApprovalId` con `Scope = Order`, con el mismo criterio de T048 y sin excepción para el Administrador (o `DiscountApprovalRequired(Order, null)`), responder `OrderDiscountRemoved` si ya no cabe, llenar `SaleLine.OrderDiscountCents` con `Cart.Allocation()`, crear `SaleDiscount` `Kind = ORDER` y `DISCOUNT_APPLIED_AUTHORIZED` si fue autorizado; la verificación de límite de crédito (014) usa el total descontado
- [X] T062 [US2] En src/Pos.Desktop/Sales/PointOfSaleViewModel.cs: acción "Descuento a la venta" (**Shift+F7**) con el mismo `DiscountDialogViewModel` sobre el subtotal y el mismo flujo de autorización con `Scope = Order`; mostrar el aviso "El descuento de $X se quitó porque supera el subtotal. Vuelva a aplicarlo si corresponde." cuando el `Cart` devuelva `OrderDiscountRemoved`; manejar `DiscountApprovalRequired(Order)` al cobrar
- [X] T063 [US2] En src/Pos.Desktop/Sales/PointOfSaleView.axaml: el pie de la venta muestra Subtotal, "Descuento (10%)" con el monto negativo y un botón para quitarlo, y Total
- [X] T064 [US2] Guardar y restaurar el descuento global manual (`kind: "MANUAL"`, con `ApprovalId`) en el borrador desde src/Pos.Desktop/Sales/DraftAutosaver.cs y src/Pos.Desktop/Sales/PointOfSaleViewModel.cs

**Checkpoint**: User Stories 1 y 2 funcionan de forma independiente.

---

## Phase 5: User Story 3 - Cupones (Priority: P2)

**Goal**: El Administrador gestiona cupones; el cajero los captura en el campo de productos (o "Aplicar cupón") y se aplican sin autorización; los usos se cuentan atómicamente al cobrar y la cancelación completa los devuelve.

**Independent Test**: Quickstart 8–13: crear `PRUEBA10` (1 uso), capturar `prueba10`, cobrar, intentar de nuevo ("ya alcanzó su límite de usos"), cancelar la venta (vuelve 1 uso), cupón con el SKU de un producto rechazado, cupón vencido rechazado.

### Tests for User Story 3

- [X] T065 [P] [US3] Pruebas de `Coupon` en tests/Pos.Domain.Tests/Discounts/CouponTests.cs: estado por fecha (día de inicio y de fin válidos completos) y por usos con la precedencia `Inactive` > `Exhausted` > `Expired` > `NotStarted` > `Active`; `Edit` con usos lanza; `Update` con límite menor que `UsesCount` lanza; `ReleaseUse` no baja de 0
- [X] T066 [P] [US3] Pruebas de `Cart` con cupón en tests/Pos.Domain.Tests/Sales/CartTests.cs: cupón de monto mayor que el subtotal se limita al subtotal y el total no queda negativo; `SetOrderDiscount` con cupón reemplaza al manual (exclusión)
- [X] T067 [P] [US3] Pruebas sobre SQLite real en tests/Pos.Infrastructure.Tests/Discounts/CouponUseCaseTests.cs: dos `ConfirmSale` por el último uso → solo una cobra y la otra recibe `CouponNotValid(Exhausted)`; cupón vencido al cobrar → `CouponNotValid(Expired)` sin registrar la venta; cancelación completa (013) devuelve el uso y registra `COUPON_USE_RELEASED`, la devolución parcial no; `SaveCoupon` con un código igual al SKU o código de barras de un producto → `CodeCollidesWithProduct`, y con código repetido → `CodeDuplicated`

### Implementation for User Story 3

- [X] T068 [P] [US3] Implementar `CouponRepository` (búsqueda por código normalizado, `SearchAsync` paginado de 100 con filtro por estado calculado con `today`, concurrencia por `Version`) en src/Pos.Infrastructure/Discounts/CouponRepository.cs y registrarlo en src/Pos.Infrastructure/DependencyInjection.cs
- [X] T069 [US3] Crear `SaveCoupon` en src/Pos.Application/Discounts/Coupons/SaveCoupon/ (Command `SaveCouponCommand(Guid? Id, string Code, DiscountMode Mode, string ValueText, DateOnly StartsOn, DateOnly EndsOn, int? UsageLimit, int ExpectedVersion)`, Validator con FluentValidation para la forma, Handler): requiere `ManageDiscounts`; errores `CodeDuplicated`, `CodeCollidesWithProduct` (con `IProductRepository.ExistsWithCodeAsync`), `CouponHasUses` y `Conflict`; registra `COUPON_CREATED` o `COUPON_UPDATED`
- [X] T070 [P] [US3] Crear `SearchCoupons` (`SearchCouponsQuery(string? Text, CouponStatus? Status, int Page)`, con vigencia, estado, usos realizados y restantes) y `GetCoupon` en src/Pos.Application/Discounts/Coupons/SearchCoupons/ y src/Pos.Application/Discounts/Coupons/GetCoupon/, ambos con `ManageDiscounts`
- [X] T071 [P] [US3] Crear `SetCouponActive` en src/Pos.Application/Discounts/Coupons/SetCouponActive/: requiere `ManageDiscounts`, registra `COUPON_DEACTIVATED` al desactivar (y `COUPON_UPDATED` al activar)
- [X] T072 [US3] Crear `ResolveCoupon` (`ResolveCouponQuery(string Code)` → `CouponLookupDto?`, `null` si no existe, estado con la fecha local de `IClock`) en src/Pos.Application/Discounts/ResolveCoupon/; requiere `ApplyDiscounts`
- [X] T073 [US3] En src/Pos.Application/Sales/FindProductsForSale/FindProductsForSaleHandler.cs y src/Pos.Application/Sales/SaleDtos.cs: agregar `LookupKind.Coupon` y `ProductLookup.Coupon: CouponLookupDto?`; buscar en orden producto exacto → cupón exacto (solo con módulo activo) → por nombre; sin licencia un código de cupón se trata como producto no encontrado
- [X] T074 [US3] En src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs, cupón: buscar por código dentro de la transacción, revalidar el estado con la fecha local (si no es `Active` → `CouponNotValid(status)` sin registrar), `Coupon.ConsumeUse(today)` y guardar con `Version`, aplicar `CouponApplied` al `Cart` (sin comparar con el límite), repartir con `Allocation()`, crear `SaleDiscount` `Kind = COUPON` con `CouponId` y `CouponCode`; Serilog registra el consumo con folio y código
- [X] T075 [US3] En src/Pos.Application/Returns/SaleReturnProcessor.cs: en la cancelación completa, si la venta tiene un `SaleDiscount` `COUPON` y el módulo está activo, ejecutar `Coupon.ReleaseUse()` y registrar `COUPON_USE_RELEASED` en la misma transacción; la devolución parcial no devuelve el uso
- [X] T076 [US3] Registrar `SaveCoupon`, `SearchCoupons`, `GetCoupon`, `SetCouponActive` y `ResolveCoupon` en src/Pos.Application/DependencyInjection.cs
- [X] T077 [P] [US3] Crear el listado de cupones (búsqueda por código, filtro por estado, paginación de 100; columnas código, descuento, vigencia, estado, usos y restantes; activar/desactivar) en src/Pos.Desktop/Discounts/CouponListView.axaml, src/Pos.Desktop/Discounts/CouponListView.axaml.cs y src/Pos.Desktop/Discounts/CouponListViewModel.cs
- [X] T078 [P] [US3] Crear el formulario de cupón (código, modalidad, valor, desde, hasta, límite de usos vacío = sin límite; con usos, código/modalidad/valor en solo lectura y la nota "El cupón ya se usó; solo puede cambiar la vigencia, el límite o desactivarlo.") en src/Pos.Desktop/Discounts/CouponFormView.axaml, src/Pos.Desktop/Discounts/CouponFormView.axaml.cs y src/Pos.Desktop/Discounts/CouponFormViewModel.cs
- [X] T079 [US3] Agregar "Descuentos > Cupones" en src/Pos.Desktop/Discounts/DiscountsModule.cs (solo `ManageDiscounts` y módulo activo)
- [X] T080 [US3] En src/Pos.Desktop/Sales/PointOfSaleViewModel.cs: si la búsqueda devuelve `LookupKind.Coupon` con estado `Active`, aplicar el cupón (si ya hay descuento global manual, preguntar "¿Reemplazar …?" y no cambiar nada si no se confirma); con otro estado o código inexistente mostrar el mensaje de `DiscountMessages`; un segundo cupón muestra "La venta ya tiene el cupón {código}; quítelo para aplicar otro."; acción "Aplicar cupón" que abre un campo de código y sigue el mismo flujo con `ResolveCoupon`; Shift+F7 con cupón presente pregunta "¿Reemplazar el cupón VERANO10 por este descuento?"
- [X] T081 [US3] Al cobrar, manejar `CouponNotValid` en src/Pos.Desktop/Sales/PointOfSaleViewModel.cs: mostrar el mensaje según el estado, retirar el cupón, recalcular el total y no cobrar hasta que el cajero confirme de nuevo; el pie muestra "Cupón VERANO10" con el monto negativo y botón para quitarlo (src/Pos.Desktop/Sales/PointOfSaleView.axaml)
- [X] T082 [US3] Guardar y restaurar el cupón (`kind: "COUPON"`, `code`) en el borrador desde src/Pos.Desktop/Sales/DraftAutosaver.cs; el cupón se revalida al cobrar

**Checkpoint**: User Stories 1, 2 y 3 funcionan de forma independiente.

---

## Phase 6: User Story 4 - Auditoría y desglose (Priority: P3)

**Goal**: Los descuentos aparecen desglosados en el ticket, en "Consultar ventas", en el reporte de ventas y en un reporte de descuentos exportable; la bitácora tiene todas las acciones.

**Independent Test**: Quickstart 5, 16, 17 y 18: ticket con desglose y "Usted ahorró"; "Descuentos > Reporte" con total igual a la suma de "Usted ahorró"; tarjeta "Total descontado" en "Reportes > Ventas"; bitácora con autorizaciones, intento fallido, alta de cupón, cambio de límite y devolución de uso.

### Tests for User Story 4

- [X] T083 [P] [US4] Prueba sobre SQLite real en tests/Pos.Infrastructure.Tests/Discounts/DiscountReportReaderTests.cs: el `TotalDiscountCents` del reporte es igual a Σ `Sales.DiscountCents` de las ventas completadas del período, excluye canceladas, y los filtros por cajero y tipo funcionan
- [X] T084 [P] [US4] Agregar a tests/Pos.Infrastructure.Tests/Reports/SalesReportReaderTests.cs una prueba de `SalesTotals.DiscountCents` igual a Σ `Sales.DiscountCents` de las ventas completadas del período

### Implementation for User Story 4

- [X] T085 [P] [US4] En src/Pos.Application/Sales/SaleDtos.cs y src/Pos.Application/Sales/GetSale/GetSaleHandler.cs: `SaleDetailDto` agrega `SubtotalCents`, `DiscountCents` y `Discounts: IReadOnlyList<SaleDiscountDto>` (tipo, modalidad, valor, monto, línea, cupón, aplicador y autorizador por nombre); `SaleLineDto` agrega `OriginalAmountCents` y `LineDiscountCents`; funciona aunque el módulo no tenga licencia (FR-023)
- [X] T086 [US4] En src/Pos.Desktop/Sales/SaleDetailView.axaml y src/Pos.Desktop/Sales/SaleDetailViewModel.cs: las líneas muestran original (`OriginalAmountCents`), descuento de línea (`LineDiscountCents`) e importe final de la línea (`OriginalAmountCents − LineDiscountCents`, **no** `AmountCents`, que ya incluye la parte repartida del descuento de venta); sección "Descuentos" con tipo, valor, monto, "Aplicó" y "Autorizó"
- [X] T087 [US4] En src/Pos.Application/Printing/Ticket/TicketBuilder.cs, según contracts/ticket-format.md: bajo cada línea con descuento, renglón "Desc. 10%" (o "Desc." con monto) con el monto negativo y el importe final de la línea (`OriginalAmountCents − LineDiscountCents`, **no** `AmountCents`) alineado a la derecha; si `DiscountCents > 0`, antes del TOTAL "SUBTOTAL" (Σ `OriginalAmountCents − LineDiscountCents`), "Descuento 5%" o "Cupón VERANO10" con monto negativo, y después de los pagos "Usted ahorró:"; se cumple SUBTOTAL − descuento de venta = TOTAL; etiquetas recortadas a 32 columnas; usa los importes guardados sin recalcular; una venta sin descuentos se imprime exactamente igual; reimpresión y ticket de venta cancelada con el mismo desglose; no depende de la licencia del módulo `Discounts` (FR-023)
- [X] T088 [P] [US4] Agregar `DiscountCents` a `SalesTotals` en src/Pos.Application/Reports/GetSalesReport/SalesReportDtos.cs y sumarlo (ventas completadas del período) en src/Pos.Infrastructure/Reports/SalesReportReader.cs; tarjeta "Total descontado" en src/Pos.Desktop/Reports/SalesReportView.axaml y src/Pos.Desktop/Reports/SalesReportViewModel.cs; el total y la tarjeta no dependen de la licencia del módulo `Discounts` (FR-023), solo de la del reporte de ventas
- [X] T089 [US4] Implementar `DiscountReportReader` (`SaleDiscounts` unido a `Sales` completadas, filtros de período, cajero por `Sales.CreatedBy` y tipo, paginación de 100, total con la misma consulta que la tabla) en src/Pos.Infrastructure/Discounts/DiscountReportReader.cs y registrarlo en src/Pos.Infrastructure/DependencyInjection.cs
- [X] T090 [US4] Crear `GetDiscountReport` (requiere `ViewDiscountReport`, módulo `Discounts`, `DiscountReportQuery(ReportPeriod Period, Guid? CashierId, DiscountKind? Kind, int Page = 1, int PageSize = 100)`, resuelve el período con `ReportPeriodResolver`) en src/Pos.Application/Discounts/GetDiscountReport/ y registrarlo en src/Pos.Application/DependencyInjection.cs
- [X] T091 [US4] Agregar `ReportKind.Discounts` en src/Pos.Application/Reports/Export/ExportRequest.cs y su documento (folio, fecha, cajero, tipo, valor, monto, autorizador, cupón) en src/Pos.Application/Reports/Export/ReportDocumentBuilder.cs y src/Pos.Application/Reports/Export/ReportTexts.cs, exportado con el `ExportReportHandler` existente
- [X] T092 [P] [US4] Crear la vista del reporte de descuentos (selector de período de 009, filtros por cajero y tipo línea/venta/cupón, tarjetas con total descontado y cantidad, tabla, exportación con `ReportExportCoordinator`) en src/Pos.Desktop/Discounts/DiscountReportView.axaml, src/Pos.Desktop/Discounts/DiscountReportView.axaml.cs y src/Pos.Desktop/Discounts/DiscountReportViewModel.cs
- [X] T093 [US4] Agregar "Descuentos > Reporte" en src/Pos.Desktop/Discounts/DiscountsModule.cs (solo `ViewDiscountReport` y módulo activo)

**Checkpoint**: Todas las historias funcionan de forma independiente.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Base de ejemplo 0.10.0, documentación y verificación final

- [X] T094 Ampliar `SampleData` en tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseGenerator.cs con un cupón con 1 uso, una venta con descuento de línea autorizado, una con cupón y su `SaleDiscounts`, una `DiscountApproval` y un borrador conservado con descuento
- [X] T095 Generar tests/Pos.Infrastructure.Tests/SampleDatabases/v0.10.0.db con `POS_GENERATE_SAMPLE_DB=1 dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.SampleDatabases.SampleDatabaseGenerator"` y agregarla al commit (nunca modificarla después) (depende de T094)
- [X] T096 Ampliar tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests.cs: al migrar de `v0.1.0.db` a `v0.10.0.db`, las tablas nuevas existen (vacías salvo en `v0.10.0.db`), `OriginalAmountCents = AmountCents` en ventas anteriores a 0.10.0, y en `v0.10.0.db` Σ `SaleLine.AmountCents` = `Sale.TotalCents` y `Sale.DiscountCents` = Σ `SaleDiscount.AmountCents`
- [X] T097 Confirmar que las pruebas de consistencia de inventario (tests/Pos.Infrastructure.Tests/Sales/SaleConsistencyTests.cs y tests/Pos.Infrastructure.Tests/Returns/InventoryConsistencyAfterReturnsTests.cs) y las de arquitectura (tests/Pos.ArchitectureTests/) pasan con la carpeta `Discounts/` nueva
- [X] T098 [P] Escribir docs/descuentos.md: cálculo y redondeo, comparación con el límite, reparto por resto mayor, `AmountCents` neto, aprobación persistente por `DraftId`, cupones (estados, usos, colisión con productos), licencia sin módulo, y el GUID de `LicensedModule.Discounts` para la herramienta de licencias del proveedor
- [X] T099 [P] Agregar la sección "0.10.0: descuentos y cupones (`DiscountsAndCoupons`)" en docs/migraciones.md (tablas, columnas, `UPDATE`, sin reconstrucción, `DiscountsMigrationTests`), y enlazar docs/descuentos.md desde docs/ventas.md y docs/usuarios-y-permisos.md (permisos nuevos)
- [ ] T100 Verificación final: `dotnet build -v q` sin errores ni advertencias, `dotnet test --verbosity quiet` en verde, y recorrer manualmente quickstart.md escenarios 1–19

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup; BLOQUEA todas las historias (la migración única `DiscountsAndCoupons` necesita todas las entidades)
- **US1 (Phase 3)**: depende de Foundational
- **US2 (Phase 4)**: depende de Foundational; reutiliza `ApproveDiscount`, `DiscountApprovalStore` y `DiscountDialogViewModel` de US1 (T044, T045, T049)
- **US3 (Phase 5)**: depende de Foundational y de `OrderDiscount` y `Cart.Allocation()` de US2 (T059, T060); no necesita la autorización de US1
- **US4 (Phase 6)**: depende de Foundational; para tener datos que mostrar conviene tener US1 (el desglose y los reportes funcionan con cualquier tipo de descuento)
- **Polish (Phase 7)**: depende de todas las historias (la base de ejemplo 0.10.0 se genera al final)

### Within Each User Story

- Pruebas de dominio junto con la entidad o el `Cart`, antes del caso de uso
- Domain → puertos/DTOs → infraestructura → caso de uso → DI → ViewModel/vista
- `ConfirmSaleHandler.cs`, `Cart.cs`, `PointOfSaleViewModel.cs`, `PointOfSaleView.axaml`, `DraftAutosaver.cs` y `DiscountsModule.cs` se editan en varias historias: esas tareas son secuenciales entre sí

### Parallel Opportunities

- Phase 2: T003, T004, T006, T007, T008, T009, T013, T014 (Domain); T015–T019 (pruebas); T020–T023 (Application); T027–T030 (Infrastructure)
- US1: T039–T041 juntas; T044, T046, T049 y T054 en paralelo con el dominio
- US2: T056–T058 juntas
- US3: T065–T067 juntas; T068, T070, T071, T077 y T078 en paralelo
- US4: T083, T084, T085, T088 y T092 en paralelo
- Polish: T098 y T099 en paralelo

---

## Parallel Example: Foundational

```bash
Task: "Crear DiscountValue en src/Pos.Domain/Discounts/DiscountValue.cs"
Task: "Extraer Proportional.Allocate en src/Pos.Domain/Common/Proportional.cs"
Task: "Crear el agregado Coupon en src/Pos.Domain/Discounts/Coupon.cs"
Task: "Crear DiscountApproval en src/Pos.Domain/Discounts/DiscountApproval.cs"
Task: "Crear SaleDiscount en src/Pos.Domain/Sales/SaleDiscount.cs"
```

## Parallel Example: User Story 1

```bash
Task: "Pruebas de Cart con descuento de línea en tests/Pos.Domain.Tests/Sales/CartTests.cs"
Task: "Pruebas de ConfirmSale con aprobaciones en tests/Pos.Infrastructure.Tests/Discounts/ConfirmSaleApprovalTests.cs"
Task: "Prueba de ApproveDiscount en tests/Pos.Infrastructure.Tests/Discounts/ApproveDiscountTests.cs"
Task: "Implementar DiscountApprovalStore en src/Pos.Infrastructure/Discounts/DiscountApprovalStore.cs"
Task: "Crear el diálogo de descuento en src/Pos.Desktop/Sales/DiscountDialogView.axaml"
```

## Parallel Example: User Story 3

```bash
Task: "Pruebas de Coupon en tests/Pos.Domain.Tests/Discounts/CouponTests.cs"
Task: "Pruebas de cupones sobre SQLite en tests/Pos.Infrastructure.Tests/Discounts/CouponUseCaseTests.cs"
Task: "Implementar CouponRepository en src/Pos.Infrastructure/Discounts/CouponRepository.cs"
Task: "Crear el listado de cupones en src/Pos.Desktop/Discounts/CouponListView.axaml"
Task: "Crear el formulario de cupón en src/Pos.Desktop/Discounts/CouponFormView.axaml"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup
2. Phase 2: Foundational (incluye la migración; verificar que una venta sin descuentos no cambia)
3. Phase 3: User Story 1
4. **STOP and VALIDATE**: quickstart 1–4 y 15, pruebas de Domain e Infrastructure
5. Demo con descuento por línea y autorización

### Incremental Delivery

1. Setup + Foundational → base lista (sin cambio visible)
2. US1 → descuento por línea (MVP)
3. US2 → descuento a la venta con reparto
4. US3 → cupones (requiere el `OrderDiscount` de US2)
5. US4 → ticket, consulta y reportes
6. Polish → base de ejemplo 0.10.0, docs y verificación completa

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes
- Ninguna tarea agrega dependencias externas (Principio VII)
- La migración `DiscountsAndCoupons` es una sola; si hay que corregirla antes de publicar, regenerarla; después de publicada, solo migraciones nuevas
- Commit después de cada tarea o grupo lógico; detenerse en cada checkpoint para validar
