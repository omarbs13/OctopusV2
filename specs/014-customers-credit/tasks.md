# Tasks: Gestión de clientes y crédito

**Input**: documentos de diseño en `/specs/014-customers-credit/` (plan.md, spec.md, research.md, data-model.md, contracts/application-ports.md, contracts/ui.md, quickstart.md)

**Prerequisites**: plan.md, spec.md. Parte de 002 (formularios), 005 (ventas), 006 (impresión), 007 (autorización), 008 (turnos), 009 (reportes), 012 (licencias) y 013 (devoluciones) ya implementada.

**Tests**: Política mínima de la constitución v1.2.0 (research §15, Principio VI): pruebas de Domain para cálculos de dinero y validaciones de integridad, casos de uso sobre SQLite real, consistencia de inventario, migración y arquitectura (obligatorias). Sin pruebas de ViewModels, vistas, mapeos ni constructores del recibo.

**Organization**: Tareas agrupadas por historia de usuario. Comandos: compilar `dotnet build -v q`; pruebas `dotnet test <proyecto> --verbosity quiet` (solo los proyectos modificados).

## Format: `[ID] [P?] [Story] Descripción con ruta`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes). Excepción: las tareas que crean un caso de uso agregan una línea a `src/Pos.Application/DependencyInjection.cs`; si se ejecutan a la vez, cada una agrega su línea al final del bloque de su carpeta y el conflicto se resuelve al integrar
- **[Story]**: historia de usuario (US1–US4)

## Path Conventions

Aplicación de escritorio por capas: `src/Pos.Domain`, `src/Pos.Application`, `src/Pos.Infrastructure`, `src/Pos.Desktop`; pruebas en `tests/Pos.*.Tests`. Importes en centavos (`long`), GUID v7 (`Guid.CreateVersion7`), fechas UTC. Código en inglés; textos al operador y documentación en español.

---

## Phase 1: Setup

**Purpose**: línea base antes de tocar ventas, turnos y devoluciones

- [X] T001 Ejecutar `dotnet build -v q` en la raíz y anotar el estado base (0 errores, 0 advertencias) y qué pruebas de `tests/Pos.Domain.Tests/`, `tests/Pos.Application.Tests/` y `tests/Pos.Infrastructure.Tests/` pasan hoy, para distinguir regresiones de cambios esperados
- [X] T002 Crear las carpetas nuevas `src/Pos.Domain/Customers/`, `src/Pos.Domain/Receivables/`, `src/Pos.Application/Customers/`, `src/Pos.Application/Receivables/`, `src/Pos.Infrastructure/Customers/`, `src/Pos.Infrastructure/Receivables/`, `src/Pos.Desktop/Customers/`, `tests/Pos.Domain.Tests/Customers/`, `tests/Pos.Domain.Tests/Receivables/`, `tests/Pos.Infrastructure.Tests/Customers/`, `tests/Pos.Infrastructure.Tests/Receivables/` y confirmar que `tests/Pos.ArchitectureTests/` las cubre sin cambios en sus reglas

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: dominio, permisos, puertos, persistencia, migración `CustomersAndCredit` y totales de turno que todas las historias necesitan

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase

### Dominio

- [X] T003 [P] Agregar a `src/Pos.Domain/Users/Permission.cs` los permisos `ManageCustomers`, `SellOnCredit`, `RegisterCustomerPayments`, `ManageCustomerCredit`, `ApproveCreditOverLimit`, `VoidCustomerPayments` y `ViewReceivables`; en `src/Pos.Domain/Users/RolePermissions.cs` dar al Cajero `ManageCustomers`, `SellOnCredit` y `RegisterCustomerPayments`, al Administrador los 7, y marcar como autorizables `ApproveCreditOverLimit` y `VoidCustomerPayments`; en `src/Pos.Domain/Licensing/ModuleAccess.cs` mapear los 7 a `LicensedModule.CreditAndCustomers` (research §11)
- [X] T004 [P] Agregar `PaymentMethod.OnAccount` con código `ACCOUNT` (cabe en TEXT(10)) en `src/Pos.Domain/Sales/PaymentMethod.cs`; en `src/Pos.Domain/Sales/Sale.cs` agregar a `Sale.Register` la regla "un pago `ACCOUNT` debe ser el único pago de la venta" (pagos = total se conserva); en `src/Pos.Domain/Sales/Checkout.cs` agregar `SetOnAccount()`, que reemplaza todos los pagos capturados por un único `ACCOUNT` por el total, sin cambio ni recibido (research §1, FR-005)
- [X] T005 [P] Agregar `RefundStatus.Settled` (código `SETTLED`, cabe en la columna actual) en `src/Pos.Domain/Returns/RefundStatus.cs` y, en `src/Pos.Domain/Returns/SaleReturnRefund.cs`, hacer que `Create` con método `OnAccount` produzca estado `Settled`; el efectivo excedente se crea como renglón aparte `CASH` / `PAID` ligado al mismo `SalePaymentId` (research §8)
- [X] T006 [P] Crear `src/Pos.Domain/Customers/CreditMode.cs` (`CASH_ONLY` | `CREDIT`, TEXT(10)) y el agregado `src/Pos.Domain/Customers/Customer.cs` con los campos estándar del Principio IV (`CreatedAt/By`, `UpdatedAt/By`, `DeletedAt`, `Version`): `Name` "Obligatorio, recortado", TEXT(120); `Phone` "Obligatorio, recortado", TEXT(30); `Email` "Opcional; formato válido si se captura" con `public static bool IsValidEmail(string)` (contiene `@` y un dominio con punto), única regla que reutiliza el validador de T033, TEXT(254); `TaxId` "RUC opcional; recortado y en mayúsculas; único si no es nulo", TEXT(20); `CreditLimitCents` "≥ 0 y ≤ `Money.MaxCents`"; `CreditMode`; `IsActive` "`true` al crear"; `SearchText` TEXT(200) = `TextNormalizer` de nombre + teléfono + RUC (sin acentos, minúsculas). Métodos: `Create(...)`, `Update(...)` (datos, recalcula `SearchText`), `ChangeCredit(limit, mode)` (permite límite menor que el saldo), `Deactivate(balanceCents)` (rechaza si saldo > 0, FR-004), `Activate()` y propiedad `CanBuyOnCredit` = `IsActive && CreditMode == Credit` (data-model "Reglas de dominio")
- [X] T007 [P] Crear `CreditPolicy` estático en `src/Pos.Domain/Customers/CreditPolicy.cs`: `Check(balanceCents, limitCents, saleCents)` devuelve `Within` si `balance + saleTotal ≤ limit` y si no `Exceeded(balance + saleTotal − limit)` (research §4, FR-006)
- [X] T008 [P] Crear en `src/Pos.Domain/Receivables/`: `ReceivableStatus.cs` (`PENDING` | `PAID` | `CANCELLED`, TEXT(10)), `ReceivableEntryType.cs` (`PAYMENT`, `PAYMENT_VOID`, `RETURN`, `EXCESS_OUT`, `EXCESS_IN`, TEXT(14)), `ReceivableEntry.cs` inmutable (`AmountCents` "Con signo: negativo baja el saldo y positivo lo sube; ≠ 0", `CustomerPaymentId?` en `PAYMENT`/`PAYMENT_VOID`, `SaleReturnId?` en `RETURN`/`EXCESS_OUT`/`EXCESS_IN`, `CreatedAt/By`) y `Receivable.cs` (`SaleId`, `CustomerId`, `CustomerName` TEXT(120), `OriginalCents` "= total de la venta, > 0", `BalanceCents` "0 ≤ saldo ≤ `OriginalCents`", `Status`, `OverLimitAuthorizedBy?`, `CreatedAt/By`, `UpdatedAt/By`, `Version`, sin `DeletedAt`) con `Create(...)`, `ApplyPayment(paymentId, amount)`, `RevertPayment(paymentId)` (`PAYMENT_VOID` por lo aplicado de ese abono), `ApplyReturn(returnId, amount, out excess)` (`RETURN` y, si hace falta, `EXCESS_OUT`), `ApplyExcess(returnId, amount)` (`EXCESS_IN`) y `Cancel()`; cada método recalcula `BalanceCents` = `OriginalCents + Σ entradas` y `Status` (`PENDING ⇄ PAID`, `CANCELLED` terminal) y rechaza saldos < 0 o > `OriginalCents` (research §3, data-model "Transiciones")
- [X] T009 [P] Crear en `src/Pos.Domain/Receivables/`: `CustomerPaymentStatus.cs` (`ACTIVE` | `VOIDED`, TEXT(8)), `CustomerPaymentFolio.cs` (formato `AB-000001`, igual que `ReturnFolio`) y `CustomerPayment.cs` (`Number` `long`, `RequestId`, `CustomerId`, `AmountCents` "> 0 y ≤ saldo del cliente", `Method` solo `CASH`, `CARD` o `TRANSFER` (rechaza `CREDIT` y `ACCOUNT`, FR-009), `Reference` "Opcional" TEXT(50) recortada, `CashShiftId?`, `BalanceBeforeCents`, `BalanceAfterCents` con `after = before − amount`, `Status`, `VoidedAt`, `VoidedBy`, `VoidAuthorizedBy`, `VoidReason` "Obligatorio al anular" TEXT(250), `VoidCashShiftId`, `CreatedAt/By`); `Register(...)` y `Void(reason, userId, authorizedBy, shiftId, utcNow)` como única mutación (`ACTIVE → VOIDED`, una sola vez, motivo obligatorio ≤ 250; si no `InvalidOperationException`) (research §5, §7)
- [X] T010 [P] Crear `PaymentAllocator` estático en `src/Pos.Domain/Receivables/PaymentAllocator.cs`: `Allocate(amountCents, pending[])` con las cuentas en orden (`CreatedAt`, `Id`), cubre por completo una cuenta antes de pasar a la siguiente y devuelve `[(ReceivableId, AmountCents)]`; lanza si el monto es ≤ 0 o mayor que Σ saldos (FR-010, FR-011)
- [X] T011 Crear `CreditReturnSettlement` estático en `src/Pos.Domain/Receivables/CreditReturnSettlement.cs`: `Settle(returnedCents, own, otherPending[])` devuelve `(ExcessCents = máx(0, R − saldo propio), Reapplied[] vía PaymentAllocator sobre las otras pendientes, CashRefundCents = Excess − reaplicado)` (research §8) (depende de T008, T010)
- [X] T012 [P] Crear `ReceivableAging` estático en `src/Pos.Domain/Receivables/ReceivableAging.cs`: `DaysOverdue(saleLocalDate, todayLocal, termDays)` = `max(0, hoy − (venta + plazo))` en días calendario locales; `IsOverdue` cuando > 0 y saldo > 0 (FR-018, research §9)
- [X] T013 [P] Ampliar `src/Pos.Domain/CashShifts/ShiftSalesTotals.cs` con `OnAccountSalesCents`, `CustomerPaymentsCashCents`, `CustomerPaymentsNonCashCents`, `CustomerPaymentVoidsCashCents` y `CustomerPaymentVoidsNonCashCents` (valor 0 por defecto); `CashShiftMath.ExpectedCash(...)` en `src/Pos.Domain/CashShifts/CashShiftMath.cs` suma `CustomerPaymentsCashCents` y resta `CustomerPaymentVoidsCashCents`; agregar las 5 propiedades `long?` a la instantánea de cierre en `src/Pos.Domain/CashShifts/CashShift.cs` (nulas en turnos cerrados antes de 0.9.0) (research §6, SC-007)

### Pruebas de dominio

- [X] T014 [P] Pruebas en `tests/Pos.Domain.Tests/Customers/CustomerTests.cs` (límite negativo rechazado, email sin `@`/dominio rechazado, RUC recortado y en mayúsculas, `Deactivate` con saldo > 0 rechazado) y `tests/Pos.Domain.Tests/Customers/CreditPolicyTests.cs` (saldo + venta igual al límite pasa; un centavo más devuelve `Exceeded(1)`)
- [X] T015 [P] Pruebas en `tests/Pos.Domain.Tests/Receivables/PaymentAllocatorTests.cs` (FIFO cubre la más antigua antes de la siguiente; monto mayor que Σ saldos o ≤ 0 lanza) y `tests/Pos.Domain.Tests/Receivables/ReceivableTests.cs` (invariante `BalanceCents = OriginalCents + Σ entradas`; `PENDING → PAID` al llegar a 0, `PAID → PENDING` con `RevertPayment`; saldo nunca < 0 ni > `OriginalCents`)
- [X] T016 [P] Pruebas en `tests/Pos.Domain.Tests/Receivables/CustomerPaymentTests.cs` (`Void` solo una vez y con motivo; método `ACCOUNT`/`CREDIT` rechazado) y `tests/Pos.Domain.Tests/Receivables/CreditReturnSettlementTests.cs` (sin abonos solo reduce; con abonos de más reaplica FIFO; sin otras deudas reintegra en efectivo)
- [X] T017 [P] Ampliar `tests/Pos.Domain.Tests/CashShifts/CashShiftMathTests.cs` con abonos en efectivo (suman), abonos no en efectivo y ventas `ACCOUNT` (no afectan) y anulaciones en efectivo (restan); actualizar `tests/Pos.Domain.Tests/Users/RolePermissionsTests.cs` y `tests/Pos.Domain.Tests/Licensing/ModuleAccessTests.cs` con los 7 permisos nuevos; ajustar las pruebas existentes que construyen `ShiftSalesTotals`, `PaymentMethod`, `RolePermissions` o `SaleReturnRefund` a las firmas nuevas

### Application

- [X] T018 [P] Agregar a `src/Pos.Application/Abstractions/Error.cs` los errores `CreditLimitExceeded(long ExcessCents)`, `CustomerNotEligibleForCredit`, `CustomerHasBalance(long BalanceCents)` y `PaymentExceedsBalance(long MaxCents)` con los mensajes exactos de contracts/application-ports.md; agregar a `src/Pos.Application/Audit/AuditActions.cs` `CUSTOMER_CREATED`, `CUSTOMER_UPDATED`, `CUSTOMER_CREDIT_CHANGED`, `CUSTOMER_DEACTIVATED`, `CUSTOMER_ACTIVATED`, `CREDIT_SALE_REGISTERED`, `CREDIT_LIMIT_OVERRIDE`, `CUSTOMER_PAYMENT_REGISTERED`, `CUSTOMER_PAYMENT_VOIDED` y `CREDIT_SETTINGS_CHANGED` con los textos del catálogo de bitácora
- [X] T019 [P] Crear en `src/Pos.Application/Customers/`: `ICustomerRepository.cs` (`GetAsync(id)`, `Add`, `SearchAsync(CustomerSearch)` → página de 100, `FindForSaleAsync(text)` solo activos `CREDIT` ≤ 20, `GetBalanceAsync(id)` = Σ `BalanceCents` de cuentas `PENDING`, `GetBalancesAsync(ids)`, `SaveChangesAsync()` → `SaveOutcome` con `DuplicateField = TaxId`), `CustomerDtos.cs` (`CustomerPage`, `CustomerDetailDto`, `CustomerForSaleDto`, `CustomerCreditStatusDto`), `CustomerFields.cs` y `CustomerMessages.cs`
- [X] T020 [P] Crear en `src/Pos.Application/Receivables/`: `IReceivableRepository.cs` (`GetBySaleAsync`, `GetPendingAsync(customerId)` en orden FIFO, `GetManyAsync(ids)`, `Add`, `GetEntriesByPaymentAsync(paymentId)`, `HasReturnAfterAsync(receivableIds, utc)`, `ListByCustomerAsync(customerId, página)`), `ICustomerPaymentRepository.cs` (`NextNumberAsync`, `FindByRequestAsync`, `GetAsync`, `Add`, `ListByCustomerAsync`, `SaveChangesAsync`), `IReceivablesSettingsStore.cs` (`Load`/`Save`), `ReceivablesSettings.cs` (`PaymentTermDays` = 30, rango 1–3650) y `ReceivableDtos.cs` (`PaymentReceipt`, filas de abonos y de cuentas)
- [X] T021 Ampliar contratos en `src/Pos.Application/Sales/ISaleRepository.cs` y `src/Pos.Application/Sales/SaleDtos.cs` (la carga la hace T030): `GetShiftTotalsAsync` con los 5 acumulados de research §6; `SaleDetailDto` con `CreditInfo? { CustomerId, CustomerName, BalanceCents, Status }`; `SaleSearch` con filtro opcional `CustomerId`; fila de búsqueda con estado de crédito (depende de T013, T020)
- [X] T022 Ajustar los casos de uso de turnos en `src/Pos.Application/CashShifts/` (`GetShiftDetail`, `CountShiftCash`, `CloseShift`, `CashShiftDtos.cs`) y `src/Pos.Application/Reports/GetMyShiftSummary/` para leer el `ShiftSalesTotals` ampliado: el esperado incluye abonos y anulaciones en efectivo, el cierre guarda la instantánea de las 5 columnas y los DTOs exponen los acumulados del bloque "Crédito"; revelar el esperado a Cajero sigue prohibido (regla de 008) (depende de T013, T021)

### Infrastructure y migración

- [X] T023 [P] Crear configuraciones EF en `src/Pos.Infrastructure/Persistence/Configurations/`: `CustomerConfiguration.cs` (longitudes de T006, `CreditMode` TEXT(10), `IX_Customers_TaxId` único filtrado `TaxId IS NOT NULL`, `IX_Customers_Active_Search` sobre (`IsActive`, `SearchText`), `Version` como token de concurrencia), `ReceivableConfiguration.cs` (FK Restrict a `Sales` y `Customers`, `IX_Receivables_SaleId` único, `IX_Receivables_Customer_Status` sobre (`CustomerId`, `Status`, `CreatedAt`, `Id`), sin `DeletedAt`), `ReceivableEntryConfiguration.cs` (`Type` TEXT(14), FK Restrict, `IX_ReceivableEntries_Receivable` sobre (`ReceivableId`, `CreatedAt`), `IX_ReceivableEntries_Payment` sobre (`CustomerPaymentId`)) y `CustomerPaymentConfiguration.cs` (`Method` TEXT(10), `Status` TEXT(8), `Reference` TEXT(50), `VoidReason` TEXT(250), FK Restrict a `Customers` y `CashShifts`, índices `IX_CustomerPayments_Number` único, `IX_CustomerPayments_RequestId` único, `IX_CustomerPayments_Customer` (`CustomerId`, `CreatedAt`), `IX_CustomerPayments_CashShiftId`, `IX_CustomerPayments_VoidCashShiftId`); `ReceivableEntry` y `CustomerPayment` sin `DeletedAt`, `UpdatedAt/By` ni `Version` (Complexity Tracking)
- [X] T024 [P] Agregar a `src/Pos.Infrastructure/Persistence/Configurations/CashShiftConfiguration.cs` las 5 columnas `long?` (`OnAccountSalesCents`, `CustomerPaymentsCashCents`, `CustomerPaymentsNonCashCents`, `CustomerPaymentVoidsCashCents`, `CustomerPaymentVoidsNonCashCents`)
- [X] T025 Agregar a `src/Pos.Infrastructure/Persistence/PosDbContext.cs` los `DbSet` de `Customers`, `Receivables`, `ReceivableEntries` y `CustomerPayments`, y extender `RejectImmutableChanges`: rechazar cualquier modificación o borrado de `ReceivableEntry`; en `CustomerPayment` solo permitir la transición `ACTIVE → VOIDED` una vez (con los campos de anulación); rechazar el borrado de `Receivable` (depende de T023, T024)
- [X] T026 Generar la migración con `dotnet ef migrations add CustomersAndCredit --project src/Pos.Infrastructure --startup-project src/Pos.Desktop` y revisar el SQL: 4 tablas nuevas con sus índices, FK solo desde las tablas nuevas hacia `Sales`, `Customers` y `CashShifts`, 5 `AddColumn` nulas en `CashShifts`, sin reconstruir ninguna tabla existente (sin `ef_temp_`) y sin relleno de datos; subir `<Version>` de 0.8.0 a 0.9.0 en `Directory.Build.props` (depende de T025)
- [X] T027 [P] Implementar `CustomerRepository` en `src/Pos.Infrastructure/Customers/CustomerRepository.cs`: búsqueda `LIKE %texto%` sobre `SearchText` normalizado con `TextNormalizer` (100 por página, filtro de inactivos), `FindForSaleAsync` solo `IsActive` y `CREDIT` (≤ 20), saldo con **una sola consulta** Σ `BalanceCents` de `Receivables` `PENDING` (`GetBalanceAsync` y `GetBalancesAsync` por lote, FR-015), y traducción de la violación de `IX_Customers_TaxId` a `SaveOutcome` duplicado de `TaxId` (depende de T019, T025)
- [X] T028 [P] Implementar en `src/Pos.Infrastructure/Receivables/`: `ReceivableRepository.cs` (pendientes en orden (`CreatedAt`, `Id`) con el índice, entradas por abono, `HasReturnAfterAsync` sobre entradas `RETURN`/`EXCESS_OUT` posteriores), `CustomerPaymentRepository.cs` (folio `MAX + 1` dentro de la transacción, búsqueda por `RequestId`, historial por cliente con anulados) y `PreferencesReceivablesSettingsStore.cs` sobre `IPreferencesStore` (30 días por defecto, mismo patrón que `PreferencesReturnsSettingsStore`) (depende de T020, T025)
- [X] T029 Registrar en `src/Pos.Infrastructure/DependencyInjection.cs` los repositorios y el store de T027–T028. Cada tarea posterior que cree un caso de uso lo registra en `src/Pos.Application/DependencyInjection.cs` como parte de la misma tarea (depende de T027, T028)
- [X] T030 Actualizar `src/Pos.Infrastructure/Sales/SaleRepository.cs`: `GetShiftTotalsAsync` suma por turno `OnAccountSalesCents` (pagos `ACCOUNT` de ventas completadas), abonos `ACTIVE` y `VOIDED` por `CashShiftId` separados en efectivo y no efectivo, y anulaciones por `VoidCashShiftId` separadas en efectivo y no efectivo; `GetDetailAsync` carga `CreditInfo` por `Receivables.SaleId`; `SearchAsync` filtra por `CustomerId` vía `Receivables` y devuelve el estado de crédito (depende de T021, T025)

### Pruebas de migración

- [X] T031 Crear `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.9.0.db` siguiendo `docs/migraciones.md` (con `SampleDatabaseGenerator`), agregar `tests/Pos.Infrastructure.Tests/SampleDatabases/CustomersMigrationTests.cs` (SQL sin `CREATE TABLE "ef_temp_…"`, la última migración es `CustomersAndCredit`, columnas nuevas de `CashShifts` nulas en turnos ya cerrados) y ampliar `SampleDatabaseUpgradeTests.cs` para migrar `v0.1.0.db` … `v0.8.0.db` a la versión actual verificando integridad (depende de T026)

**Checkpoint**: base lista; `dotnet build -v q` sin advertencias y pruebas de dominio, migración y arquitectura en verde

---

## Phase 3: User Story 1 - Catálogo de clientes (Priority: P1) 🎯 MVP

**Goal**: dar de alta, buscar, editar, activar y desactivar clientes; el Cajero solo crea clientes "solo efectivo" con límite 0 y solo el Administrador asigna crédito.

**Independent Test**: como Cajero crear "Ana" (queda solo efectivo / 0); como Administrador darle crédito de 1 000; crear dos clientes con el mismo RUC (se rechaza el segundo); buscar por nombre, teléfono y RUC; desactivar un cliente sin saldo (quickstart escenarios 1–3).

### Tests for User Story 1

- [X] T032 [P] [US1] Pruebas de casos de uso sobre SQLite real en `tests/Pos.Infrastructure.Tests/Customers/CustomerUseCaseTests.cs`: RUC duplicado (también contra un cliente inactivo y con distinto uso de mayúsculas/espacios) rechazado con `Duplicate(TaxId)`; desactivar con saldo pendiente (cuenta `PENDING` insertada con el soporte de pruebas) rechazado con `CustomerHasBalance` y sin cambios; un Cajero que envía límite y modalidad `CREDIT` crea el cliente `CASH_ONLY` con límite 0 (FR-020)

### Implementation for User Story 1

- [X] T033 [P] [US1] Crear `CreateCustomer` en `src/Pos.Application/Customers/CreateCustomer/` (comando, validador FluentValidation con nombre obligatorio ≤ 120, teléfono obligatorio ≤ 30, email `Must(Customer.IsValidEmail)` ≤ 254 (no `EmailAddress()`, research §10), RUC ≤ 20, límite ≥ 0; handler): licencia `CreditAndCustomers` → permiso `ManageCustomers` → validación; sin `ManageCustomerCredit` **ignora** límite y modalidad y crea `CASH_ONLY` / 0 (FR-020); traduce el RUC duplicado a `Duplicate(TaxId)`; audita `CUSTOMER_CREATED` en la misma transacción; devuelve `Result<Guid>`; registrar en DI
- [X] T034 [P] [US1] Crear `UpdateCustomer` en `src/Pos.Application/Customers/UpdateCustomer/` (comando con `Id`, `ExpectedVersion`, datos, `CreditLimitCents?`, `CreditMode?`; validador; handler): versión distinta → `Conflict`; cambiar límite o modalidad sin `ManageCustomerCredit` → `Forbidden`; audita `CUSTOMER_UPDATED` y, si cambió el crédito, `CUSTOMER_CREDIT_CHANGED` con valores anterior y nuevo; registrar en DI
- [X] T035 [P] [US1] Crear `SetCustomerActive` en `src/Pos.Application/Customers/SetCustomerActive/`: permiso `ManageCustomerCredit`; dentro de una transacción `BEGIN IMMEDIATE` lee el saldo con `GetBalanceAsync` y desactivar con saldo > 0 devuelve `CustomerHasBalance(saldo)` (FR-004); reactivar sin condición; audita `CUSTOMER_DEACTIVATED` / `CUSTOMER_ACTIVATED`; registrar en DI
- [X] T036 [P] [US1] Crear `SearchCustomers` (`Text?`, `IncludeInactive`, página de 100 → `CustomerPage { Id, Name, Phone, TaxId, CreditMode, LimitCents, BalanceCents, IsActive }` con saldos por lote) y `GetCustomer` (`CustomerDetailDto` + saldo, disponible = máx(0, límite − saldo) y días vencido de la cuenta pendiente más antigua con `ReceivableAging` y `IReceivablesSettingsStore`; `NotFound`) en `src/Pos.Application/Customers/SearchCustomers/` y `src/Pos.Application/Customers/GetCustomer/`, ambos con permiso `ManageCustomers`; registrar en DI
- [X] T037 [P] [US1] Agregar a `src/Pos.Desktop/Resources/Strings.resx` los textos `Nav_Customers` y `Customer_*` (lista, columnas, "Mostrar inactivos", formulario, sección "Crédito", "Crédito disponible" / "Solo efectivo", "Solo efectivo, límite $0,00", errores por campo, "El RUC ya está registrado para otro cliente", `CustomerHasBalance`, ficha y acciones)
- [X] T038 [US1] Crear `src/Pos.Desktop/Customers/CustomersModule.cs` con `AddCustomersModule()`: grupo `customers` con orden 6 y licencia `CreditAndCustomers`, página `customers.list` (orden 0, permiso `ManageCustomers`); registrar ViewModels; invocarlo desde `src/Pos.Desktop/Composition/HostBuilder.cs` (contracts/ui.md "Navegación") (depende de T037)
- [X] T039 [US1] Crear `CustomerListView.axaml`, `CustomerListView.axaml.cs` y `CustomerListViewModel.cs` en `src/Pos.Desktop/Customers/`: búsqueda con retardo mientras se escribe, casilla "Mostrar inactivos", columnas nombre, teléfono, RUC, modalidad, límite, saldo y estado (inactivos atenuados), botón "Nuevo cliente", doble clic o Enter abre la ficha; solo invoca `SearchCustomers` (depende de T036, T038)
- [X] T040 [US1] Crear `src/Pos.Desktop/Customers/CustomerFormViewModel.cs` sobre el `FormViewModel` de 002 (`src/Pos.Desktop/Forms/`): campos nombre*, teléfono*, email y RUC; sección "Crédito" (modalidad y límite) de solo lectura sin `ManageCustomerCredit` mostrando "Solo efectivo, límite $0,00"; errores por campo; `Conflict` muestra el mensaje estándar de recarga; invoca `CreateCustomer`/`UpdateCustomer` (depende de T033, T034, T038)
- [X] T041 [US1] Crear `CustomerDetailView.axaml`, `CustomerDetailView.axaml.cs` y `CustomerDetailViewModel.cs` en `src/Pos.Desktop/Customers/`: encabezado (nombre, teléfono, email, RUC, modalidad, límite, saldo pendiente, disponible y días vencido si los hay, todos de `GetCustomer`), acciones "Editar" (abre `CustomerFormViewModel`) y "Desactivar"/"Activar" solo con `ManageCustomerCredit` (muestra `CustomerHasBalance`); deja las pestañas "Ventas a crédito" y "Abonos" como contenedores que llenan US2 y US3 (depende de T035, T036, T040)

**Checkpoint**: el catálogo de clientes funciona y se prueba por sí solo; es el MVP

---

## Phase 4: User Story 2 - Vender a crédito (Priority: P1)

**Goal**: en el punto de venta elegir un cliente con crédito y registrar la venta con un único pago `ACCOUNT` por el total, creando su cuenta por cobrar en la misma transacción; si se excede el límite, exigir autorización de un Administrador.

**Independent Test**: vender 200 y 500 a crédito a Ana (límite 1 000), comprobar "Pendiente de pago", saldo 700, esperado del turno sin cambio y ticket con cliente y "A crédito"; intentar 400 (excede por 100) sin y con autorización (quickstart escenarios 4–5).

### Tests for User Story 2

- [X] T042 [P] [US2] Pruebas de casos de uso sobre SQLite real en `tests/Pos.Infrastructure.Tests/Receivables/CreditSaleUseCaseTests.cs` para `ConfirmSale` con pago `ACCOUNT`: total igual al disponible pasa sin concesión; Cajero que excede sin concesión → `CreditLimitExceeded(exceso)` y no queda venta, cuenta ni movimiento; con concesión válida de `ApproveCreditOverLimit` se registra, la concesión se consume y la bitácora tiene `CREDIT_LIMIT_OVERRIDE` con cajero, autorizador, cliente, venta, saldo previo, límite y monto; Administrador pasa sin concesión y también se audita `CREDIT_LIMIT_OVERRIDE` con él mismo como autorizador (FR-006); cliente `CASH_ONLY` o inactivo → `CustomerNotEligibleForCredit`; `ACCOUNT` mezclado con otro pago rechazado; falla forzada antes de confirmar no deja venta, cuenta ni inventario (H2-6, SC-008); el esperado del turno no cambia y `OnAccountSalesCents` sí (SC-007)
- [X] T043 [P] [US2] Ampliar la prueba obligatoria `tests/Pos.Infrastructure.Tests/Inventory/InventoryConsistencyTests.cs` con una venta a crédito: las existencias coinciden con el cálculo manual igual que una venta de contado

### Implementation for User Story 2

- [X] T044 [P] [US2] Crear `FindCustomersForSale` en `src/Pos.Application/Customers/FindCustomersForSale/` (permiso `SellOnCredit`, licencia, ≤ 20 `{ Id, Name, Phone, TaxId }` solo activos `CREDIT`) y `GetCustomerCreditStatus` en `src/Pos.Application/Customers/GetCustomerCreditStatus/` (solo lectura, `CustomerId` + `SaleTotalCents` → `{ BalanceCents, LimitCents, AvailableCents, WouldExceedByCents, HasOverdue }` con `CreditPolicy` y `ReceivableAging`); registrar ambos en DI
- [X] T045 [US2] Ampliar `src/Pos.Application/Sales/ConfirmSale/ConfirmSaleCommand.cs` (+ `CustomerId?`, `OverLimitGrantId?`), `ConfirmSaleValidator.cs` y `ConfirmSaleHandler.cs`: un pago `ACCOUNT` exige licencia `CreditAndCustomers`, `SellOnCredit`, `CustomerId` y ser el único pago; **dentro** de la transacción `BEGIN IMMEDIATE` carga el cliente (`CanBuyOnCredit` o `CustomerNotEligibleForCredit`), lee el saldo y aplica `CreditPolicy`; si excede, el Administrador pasa directo (queda como autorizador en `OverLimitAuthorizedBy` y en la bitácora) y un Cajero necesita la concesión de `ApproveCreditOverLimit` (consumida al final de las validaciones; sin ella `CreditLimitExceeded`, y una concesión inválida registra `ADMIN_AUTHORIZATION_DENIED` sin contraseña); crea `Receivable` (copia `CustomerName`, `OriginalCents` = total, `OverLimitAuthorizedBy`) y audita `CREDIT_SALE_REGISTERED` y, si hubo excedente, `CREDIT_LIMIT_OVERRIDE`; Serilog con cliente, venta, usuario y montos; idempotencia por `DraftId` intacta (depende de T029, T030)
- [X] T046 [P] [US2] Crear `ListCustomerReceivables` en `src/Pos.Application/Receivables/ListCustomerReceivables/` (permiso `ManageCustomers`; `CustomerId`, `OnlyPending`, página → `{ SaleId, SaleFolio, SaleDateUtc, OriginalCents, BalanceCents, Status, DaysOverdue }`); registrar en DI
- [X] T047 [P] [US2] Actualizar `src/Pos.Application/Sales/GetSale/GetSaleHandler.cs` y `src/Pos.Application/Sales/SearchSales/` para exponer `CreditInfo` y el estado "Pendiente de pago" / "Pagada" de las ventas a crédito, y aceptar el filtro `CustomerId` (depende de T030)
- [X] T048 [P] [US2] En `src/Pos.Application/Printing/Ticket/TicketBuilder.cs` imprimir para ventas a crédito "Cliente: {nombre}" (de `Receivables.CustomerName`) y "A crédito: $X" en lugar del bloque de pagos, sin cambio ni recibido (research §12)
- [X] T049 [P] [US2] Etiquetar `ACCOUNT` como "A crédito" en `src/Pos.Desktop/Sales/PaymentMethodLabels.cs` y en el desglose por forma de pago de `src/Pos.Infrastructure/Reports/SalesReportReader.cs` (el total vendido la incluye); agregar los textos `Credit_*` (botón "Cliente…", "Venta a crédito", saldo/límite/disponible, "El cliente tiene ventas vencidas", "Excede el límite por $X; requiere autorización", `CustomerNotEligibleForCredit`, bloque "Crédito" del detalle de venta, "Pendiente de pago", "Pagada", "Cancelada") a `src/Pos.Desktop/Resources/Strings.resx`
- [X] T050 [US2] Crear `CustomerPickerView.axaml`, `CustomerPickerView.axaml.cs` y `CustomerPickerViewModel.cs` en `src/Pos.Desktop/Sales/` (buscador con `FindCustomersForSale`) y agregar a `src/Pos.Desktop/Sales/PointOfSaleView.axaml` / `PointOfSaleViewModel.cs` el botón "Cliente…" (visible con `SellOnCredit` y licencia) y la etiqueta del cliente elegido con "×"; el cliente **no** se guarda en el borrador (`SaleDrafts` no cambia) (depende de T044)
- [X] T051 [US2] Ampliar `src/Pos.Desktop/Sales/CheckoutView.axaml` y `CheckoutViewModel.cs`: opción "Venta a crédito" visible solo con cliente elegido, que invoca `Checkout.SetOnAccount()` y reemplaza los pagos; panel con saldo, límite y disponible de `GetCustomerCreditStatus`; avisos de vencido (no bloquea) y de excedente; al recibir `CreditLimitExceeded` abre `AdminAuthorizationView` de 007 con `ApproveCreditOverLimit` y reintenta con `OverLimitGrantId`; cancelar o contraseña incorrecta no registra nada; muestra `CustomerNotEligibleForCredit` si el cliente cambió (depende de T045, T050)
- [X] T052 [US2] Agregar el bloque "Crédito" (cliente, saldo de la venta, estado, enlace a la ficha del cliente) en `src/Pos.Desktop/Sales/SaleDetailView.axaml` / `SaleDetailViewModel.cs` y el estado de crédito en la lista de `src/Pos.Desktop/Sales/SalesHistoryView.axaml` / `SalesHistoryViewModel.cs` (depende de T047)
- [X] T053 [US2] Llenar la pestaña "Ventas a crédito" de `src/Pos.Desktop/Customers/CustomerDetailView.axaml` / `CustomerDetailViewModel.cs` con `ListCustomerReceivables` (folio, fecha, monto, saldo, estado y días vencido); abrir una fila lleva al detalle de venta existente (depende de T041, T046)

**Checkpoint**: la venta a crédito con control de límite funciona; no publicar sin la subsección de devoluciones de US3 (una cancelación de venta a crédito debe ajustar el saldo)

---

## Phase 5: User Story 3 - Registrar abono (Priority: P1)

**Goal**: registrar abonos FIFO dentro de un turno abierto, con folio `AB-…` y recibo; anularlos con autorización; y que las cancelaciones y devoluciones de ventas a crédito ajusten el saldo (FR-016).

**Independent Test**: abonar 300 en efectivo a Ana con doble clic (un solo `AB-000001`, saldo 1 100 → 800, la venta de 200 pasa a "Pagada", esperado +300); intentar 900 y 0; anular el abono; abonar 600 y cancelar la venta de 500; abonar sin turno (quickstart escenarios 6–9, 12, 13).

### Tests for User Story 3

- [X] T054 [P] [US3] Pruebas de casos de uso sobre SQLite real en `tests/Pos.Infrastructure.Tests/Receivables/CustomerPaymentUseCaseTests.cs`: reparto FIFO y venta saldada pasa a `PAID`; monto mayor que el saldo o ≤ 0 → `PaymentExceedsBalance(saldo)`; sin turno abierto → `ShiftRequired` con cualquier forma de pago; mismo `RequestId` devuelve el abono existente sin duplicar; dos abonos concurrentes no dejan saldo negativo; efectivo suma al esperado y tarjeta no; anulación sin concesión → `Forbidden(CanBeAuthorized: true)` también para Administrador; anulación válida revierte saldo y estados (`PAID → PENDING`) y resta del esperado; anular dos veces → `InvalidState`; efectivo insuficiente → `InsufficientCash(null)`; anular sin turno abierto → `ShiftRequired`; sin licencia de Turnos el abono se registra y se anula sin turno; sin licencia `CreditAndCustomers` registrar y anular → `ModuleNotLicensed`; `BalanceCents` = `OriginalCents` + Σ entradas para cada cuenta tras una secuencia mixta de ventas, abonos, anulaciones y cancelaciones (SC-004)
- [X] T055 [P] [US3] Pruebas sobre SQLite real en `tests/Pos.Infrastructure.Tests/Receivables/CreditReturnTests.cs`: cancelar una venta a crédito sin abonos reduce el saldo y deja la cuenta `CANCELLED`; cancelar una venta parcialmente abonada reaplica el excedente FIFO a otras cuentas (quickstart escenario 9); sin otras deudas reintegra el excedente en efectivo desde el turno abierto (renglón `CASH`/`PAID`) y sin efectivo suficiente → `InsufficientCash(null)` sin cambios; compensación `CREDIT_NOTE` rechazada; anular un abono aplicado a una venta devuelta después → `InvalidState`; ruta de `CancelSale` sin licencia de Devoluciones también ajusta el saldo; ampliar `tests/Pos.Infrastructure.Tests/Inventory/InventoryConsistencyTests.cs` con la cancelación de una venta a crédito

### Implementation for User Story 3: abonos

- [X] T056 [US3] Crear `RegisterCustomerPayment` en `src/Pos.Application/Receivables/RegisterCustomerPayment/` (comando con `RequestId`, `CustomerId`, `AmountCents`, `Method`, `Reference?`; validador: `Reference` ≤ 50, método `CASH`/`CARD`/`TRANSFER`; handler) en una transacción `BEGIN IMMEDIATE` con el orden de research §5: licencia → permiso `RegisterCustomerPayments` → validación → turno abierto propio (cualquiera con `ManageShifts`; sin turno con licencia de Turnos → `ShiftRequired`; sin licencia de Turnos se registra sin turno) → idempotencia por `RequestId` → cliente y saldo → `PaymentAllocator` (o `PaymentExceedsBalance`) → `CustomerPayment` con folio `MAX + 1`, saldos anterior/nuevo y entradas `PAYMENT` → estados de las cuentas → bitácora `CUSTOMER_PAYMENT_REGISTERED` → guardar; reintento ante folio duplicado; devuelve `PaymentReceipt`; Serilog con cliente, folio, usuario y monto; registrar en DI (depende de T029)
- [X] T057 [US3] Crear `VoidCustomerPayment` en `src/Pos.Application/Receivables/VoidCustomerPayment/` (comando `PaymentId`, `Reason` obligatorio ≤ 250, `AuthorizationGrantId`; validador; handler) con el orden de contracts/application-ports.md: licencia → permiso `RegisterCustomerPayments` → motivo → abono `ACTIVE` (si no `InvalidState`) → `HasReturnAfterAsync` sobre sus cuentas (si hay, `InvalidState`) → turno abierto (si no `ShiftRequired`) → si fue en efectivo `CashShiftMath.CanRefund` (si no `InsufficientCash(null)`); sin licencia de Turnos se omiten ambos pasos y `VoidCashShiftId` queda nulo (FR-014) → consumir siempre la concesión `VoidCustomerPayments` (también para Administrador; inválida audita `ADMIN_AUTHORIZATION_DENIED`) → `RevertPayment` por cada entrada `PAYMENT` → `CustomerPayment.Void(...)` con `VoidCashShiftId` → bitácora `CUSTOMER_PAYMENT_VOIDED` (quién, autorizador, motivo, folio, monto) → guardar; registrar en DI (depende de T056)
- [X] T058 [P] [US3] Crear `ListCustomerPayments` en `src/Pos.Application/Receivables/ListCustomerPayments/` (permiso `ManageCustomers`; `CustomerId`, página → `{ PaymentId, Folio, CreatedAtUtc, AmountCents, Method, Reference, BalanceBeforeCents, BalanceAfterCents, Status, VoidReason? }`, anulados visibles); registrar en DI
- [X] T059 [P] [US3] Crear `src/Pos.Application/Printing/Ticket/CustomerPaymentReceiptBuilder.cs` (folio, fecha local, cliente, monto, forma de pago, referencia, saldo anterior y nuevo; leyenda "ANULADO" si el abono está anulado) y agregar `PrintSource.CustomerPaymentSource(paymentId)` en `src/Pos.Application/Printing/PrintTicket/PrintTicketCommand.cs` y `PrintTicketHandler.cs`; una falla de impresión no revierte el abono (research §12)
- [X] T060 [P] [US3] Agregar el bloque "Crédito" (ventas a crédito, abonos en efectivo, abonos con tarjeta o transferencia, anulaciones) a `src/Pos.Application/Printing/Ticket/ShiftTicketBuilder.cs`, `src/Pos.Desktop/CashShifts/ShiftDetailView.axaml` / `ShiftDetailViewModel.cs` y `src/Pos.Desktop/Reports/MyShiftView.axaml` / `MyShiftViewModel.cs`; el esperado solo se muestra a quien hoy lo ve (FR-012, regla de 008) (depende de T022)
- [X] T061 [P] [US3] Agregar los textos `Payment_*` a `src/Pos.Desktop/Resources/Strings.resx`: diálogo de abono ("Se aplicará a las ventas más antiguas primero", `PaymentExceedsBalance`, `ShiftRequired`, "Abono registrado. No se pudo imprimir el recibo; puede reimprimirlo desde el historial"), diálogo de anulación (motivo, "Este abono se aplicó a una venta que después se canceló o devolvió; no se puede anular", `InsufficientCash` sin montos), pestaña "Abonos" ("Registrar abono", "Reimprimir recibo", "Anular", "Anulado") y bloque "Crédito" del corte
- [X] T062 [US3] Crear `RegisterPaymentView.axaml`, `RegisterPaymentView.axaml.cs` y `RegisterPaymentViewModel.cs` en `src/Pos.Desktop/Customers/`: genera el `RequestId` al abrir; campos monto*, forma de pago (Efectivo / Tarjeta / Transferencia) y referencia; muestra el saldo actual; sin turno abierto muestra `ShiftRequired` y deshabilita "Registrar"; "Registrar" se deshabilita mientras procesa; al confirmar imprime el recibo vía `PrintTicket` y, si falla, muestra el aviso de reimpresión (depende de T056, T059, T061)
- [X] T063 [US3] Crear `VoidPaymentView.axaml`, `VoidPaymentView.axaml.cs` y `VoidPaymentViewModel.cs` en `src/Pos.Desktop/Customers/`: resumen del abono y motivo* (≤ 250); al confirmar abre **siempre** `AdminAuthorizationView` de 007 con `VoidCustomerPayments` y luego invoca `VoidCustomerPayment`; muestra `InvalidState`, `ShiftRequired` e `InsufficientCash` sin montos (depende de T057, T061)
- [X] T064 [US3] Llenar la pestaña "Abonos" de `src/Pos.Desktop/Customers/CustomerDetailView.axaml` / `CustomerDetailViewModel.cs` con `ListCustomerPayments` (folio, fecha, monto, forma de pago, referencia, saldo anterior y nuevo, estado; "Anulado" en rojo con el motivo en la información emergente) y los botones "Registrar abono", "Reimprimir recibo" y "Anular" (visibles con `RegisterCustomerPayments`); refrescar encabezado y pestañas tras registrar o anular (depende de T053, T058, T062, T063)

### Implementation for User Story 3: devoluciones de ventas a crédito (FR-016)

- [X] T065 [US3] Crear `src/Pos.Application/Receivables/CreditSettlementService.cs`, punto único que ajusta cuentas por una devolución dentro de la transacción del llamador: carga la cuenta de la venta y las otras pendientes del cliente, aplica `CreditReturnSettlement.Settle`, registra `ApplyReturn` (`RETURN`/`EXCESS_OUT`) y `ApplyExcess` (`EXCESS_IN`) con el `SaleReturnId`, pasa la cuenta a `CANCELLED` si la venta se cancela completa o queda totalmente devuelta, y devuelve `{ ReducesBalanceCents, ReappliedCents, CashRefundCents }`; funciona aunque el módulo `CreditAndCustomers` no tenga licencia (research §11); registrar en DI (depende de T029)
- [X] T066 [US3] Ampliar `src/Pos.Application/Returns/SaleReturnProcessor.cs` y `src/Pos.Application/Returns/ReturnPlan.cs`: si la venta tiene pago `ACCOUNT`, rechazar `CREDIT_NOTE` (`ValidationFailed`), invocar `CreditSettlementService`, pasar a `ReturnCashGate` solo el `CashRefundCents` calculado, y crear el renglón `ACCOUNT`/`SETTLED` por `R − CashRefund` y, si hay, el `CASH`/`PAID` ligado al mismo `SalePaymentId` (research §8) (depende de T065)
- [X] T067 [US3] Ampliar `src/Pos.Application/Returns/PreviewReturn/PreviewReturnHandler.cs` y `src/Pos.Application/Returns/ReturnDtos.cs` con `CreditSettlement? { ReducesBalanceCents, ReappliedCents, CashRefundCents }` para ventas a crédito, calculado sin escribir (depende de T065)
- [X] T068 [US3] Ajustar la ruta sin licencia de Devoluciones en `src/Pos.Application/Sales/CancelSale/CancelSaleHandler.cs` para que una venta a crédito también ajuste su cuenta con `CreditSettlementService` en la misma transacción (research §8) (depende de T065)
- [X] T069 [US3] En `src/Pos.Desktop/Sales/ReturnSaleView.axaml` / `ReturnSaleViewModel.cs` ocultar "Nota de crédito" en ventas a crédito y mostrar la vista previa "Reduce el saldo en $X", "Se aplica a otras ventas del cliente $Y" y "Reintegro en efectivo $Z" desde `PreviewReturn`; agregar esos textos a `src/Pos.Desktop/Resources/Strings.resx` (depende de T067)

**Checkpoint**: el ciclo venta a crédito → abono → anulación → devolución funciona con saldos exactos; US1–US3 se prueban por separado

---

## Phase 6: User Story 4 - Reporte de cuentas por cobrar (Priority: P2)

**Goal**: "Reportes > Créditos" con saldo, límite, último abono y días vencido por cliente con saldo, filtros por estado y totales sobre las filas visibles; plazo de pago configurable.

**Independent Test**: con clientes al día, vencidos y al límite, filtrar por cada estado y comprobar que filas y totales coinciden con la suma de saldos de las fichas; bajar el plazo a 1 día (quickstart escenario 11).

### Tests for User Story 4

- [X] T070 [P] [US4] Pruebas sobre SQLite real en `tests/Pos.Infrastructure.Tests/Receivables/ReceivablesReportTests.cs`: solo aparecen clientes con saldo > 0; el total del reporte es igual a la suma de `GetBalanceAsync` de cada cliente (SC-006); filtros `Overdue`/`Current` excluyentes y `AtLimit` independiente (saldo ≥ límite); días vencido de la cuenta pendiente más antigua con el plazo configurado

### Implementation for User Story 4

- [X] T071 [P] [US4] Crear `GetReceivablesSettings` y `SaveReceivablesSettings` en `src/Pos.Application/Receivables/GetReceivablesSettings/` y `src/Pos.Application/Receivables/SaveReceivablesSettings/` (permiso `ManageCustomerCredit`; `PaymentTermDays` en rango 1–3650; audita `CREDIT_SETTINGS_CHANGED`); registrar en DI
- [X] T072 [P] [US4] Crear `src/Pos.Application/Reports/IReceivablesReportReader.cs` (`GetAsync(ReceivablesFilter, todayLocal, termDays)`) e implementar `src/Pos.Infrastructure/Reports/ReceivablesReportReader.cs` con el índice (`CustomerId`, `Status`): una fila por cliente con saldo > 0 con nombre, saldo, límite, fecha del último abono `ACTIVE`, días vencido e `IsOverdue`/`IsAtLimit`, filtro por texto; registrar en `src/Pos.Infrastructure/DependencyInjection.cs` (< 500 ms con 10 000 clientes y 100 000 cuentas)
- [X] T073 [US4] Crear `GetReceivablesReport` en `src/Pos.Application/Reports/GetReceivablesReport/` (permiso `ViewReceivables`, licencia `CreditAndCustomers`; `Status?` (`Current`, `Overdue`, `AtLimit`), `Text?` → `ReceivablesReport { Rows, TotalBalanceCents, CustomerCount }` con totales calculados sobre las filas devueltas; sin caché); registrar en DI (depende de T072)
- [X] T074 [US4] Crear `ReceivablesReportView.axaml`, `ReceivablesReportView.axaml.cs` y `ReceivablesReportViewModel.cs` en `src/Pos.Desktop/Reports/`: filtros Todos / Al día / Vencido / Al límite y texto, botón "Actualizar", columnas cliente, saldo pendiente, límite, último abono, días vencido y etiquetas de estado, pie "Saldo total pendiente" y "Clientes con saldo", "Plazo de pago (días)" editable por el Administrador; abrir una fila lleva a la ficha del cliente; registrar la página `reports.receivables` (orden 30, permiso `ViewReceivables`, licencia `CreditAndCustomers`) en `src/Pos.Desktop/Reports/ReportsModule.cs` y sus textos en `src/Pos.Desktop/Resources/Strings.resx` (depende de T071, T073)

**Checkpoint**: todas las historias funcionan de forma independiente

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: documentación, diagnóstico y validación final

- [X] T075 [P] Crear `docs/clientes-y-credito.md` (guía de soporte: saldo y libro `ReceivableEntries`, reparto FIFO, anulación y su restricción tras devoluciones, devoluciones de ventas a crédito, plazo de pago, consulta SQL para comparar `BalanceCents` contra el libro)
- [X] T076 [P] Actualizar `docs/ventas.md` (venta a crédito y autorización de límite), `docs/turnos-de-caja.md` (bloque "Crédito" y esperado), `docs/devoluciones.md` (liquidación a crédito), `docs/usuarios-y-permisos.md` (7 permisos nuevos y licencia) y `docs/migraciones.md` (sección 0.9.0 sin reconstrucciones)
- [X] T077 Revisar que cada venta a crédito, abono, anulación y rechazo se registre en Serilog con cliente, venta, folio, usuario y montos, nunca la contraseña (Principio VIII), en los handlers de T045, T056, T057 y T066
- [X] T078 Ejecutar `dotnet build -v q` (0 errores, 0 advertencias) y `dotnet test --verbosity quiet` desde la raíz; corregir regresiones frente a la línea base de T001
- [ ] T079 Recorrer los 13 escenarios manuales y las verificaciones de integridad de `specs/014-customers-credit/quickstart.md` como Administrador y Cajero

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup; BLOQUEA todas las historias
- **US1 (Phase 3)**: depende de Foundational
- **US2 (Phase 4)**: depende de Foundational; necesita clientes (US1 provee la interfaz para darlos de alta, pero las pruebas los crean directamente). T053 depende de la ficha de T041
- **US3 (Phase 5)**: depende de Foundational; los abonos necesitan cuentas por cobrar creadas por US2 (`ConfirmSale` con `ACCOUNT`). T064 depende de T053
- **US4 (Phase 6)**: depende de Foundational; su valor real requiere US2 y US3, aunque las pruebas siembran los datos
- **Polish (Phase 7)**: depende de las historias que se entreguen

### Within Foundational

- T003–T010, T012, T013 en paralelo; T011 tras T008 y T010
- T014–T017 tras sus tipos de dominio
- T018–T020 en paralelo; T021 tras T013 y T020; T022 tras T021
- T023 y T024 en paralelo → T025 → T026 → T031
- T027 y T028 tras T025 → T029; T030 tras T021 y T025

### Within Each User Story

- Las pruebas se escriben primero y deben fallar antes de implementar
- Casos de uso antes de ViewModels y vistas
- Cada caso de uso se registra en DI en su propia tarea
- En US3, los abonos (T056–T064) y las devoluciones (T065–T069) son dos cadenas independientes

### Parallel Opportunities

- Fase 2: todo el dominio (T003–T010, T012, T013), sus pruebas (T014–T017), los puertos (T018–T020) y las configuraciones (T023, T024)
- US1: T032–T037 en paralelo
- US2: T042–T044 y T046–T049 en paralelo
- US3: T054, T055, T058–T061 en paralelo; T065 en paralelo con la cadena de abonos
- US4: T070–T072 en paralelo
- Polish: T075 y T076

---

## Parallel Example: User Story 1

```bash
# Pruebas y casos de uso de US1 a la vez:
Task: "Pruebas de casos de uso en tests/Pos.Infrastructure.Tests/Customers/CustomerUseCaseTests.cs"
Task: "Crear CreateCustomer en src/Pos.Application/Customers/CreateCustomer/"
Task: "Crear UpdateCustomer en src/Pos.Application/Customers/UpdateCustomer/"
Task: "Crear SetCustomerActive en src/Pos.Application/Customers/SetCustomerActive/"
Task: "Crear SearchCustomers y GetCustomer en src/Pos.Application/Customers/"
Task: "Agregar textos Customer_* a src/Pos.Desktop/Resources/Strings.resx"
```

## Parallel Example: User Story 3

```bash
Task: "Pruebas en tests/Pos.Infrastructure.Tests/Receivables/CustomerPaymentUseCaseTests.cs"
Task: "Pruebas en tests/Pos.Infrastructure.Tests/Receivables/CreditReturnTests.cs"
Task: "Crear CustomerPaymentReceiptBuilder en src/Pos.Application/Printing/Ticket/"
Task: "Bloque Crédito en ShiftTicketBuilder, ShiftDetailView y MyShiftView"
Task: "Crear CreditSettlementService en src/Pos.Application/Receivables/"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Completar Phase 1 y Phase 2 (incluida la migración 0.9.0 y sus pruebas)
2. Completar Phase 3 (US1): catálogo de clientes
3. **Detenerse y validar**: quickstart escenarios 1–3
4. Entregar si conviene: el catálogo es útil aunque todavía no haya ventas a crédito

### Incremental Delivery

1. Setup + Foundational → base lista
2. US1 → catálogo (MVP)
3. US2 + US3 → venta a crédito, abonos y devoluciones; **entregar juntas**, porque sin abonos el saldo solo crece y sin la liquidación una cancelación dejaría saldos incorrectos
4. US4 → reporte "Créditos"
5. Polish → documentación y quickstart completo

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes
- Cada escritura es una transacción `BEGIN IMMEDIATE` con su entrada de bitácora (FR-019)
- Todos los casos de uso nuevos verifican primero la licencia `CreditAndCustomers` (`ModuleNotLicensed`)
- Los ViewModels no calculan saldos, disponibles, excedentes ni días vencidos (Principio III)
- Al implementar, ejecutar solo las pruebas del proyecto modificado; la suite completa en T078
- Hacer commit después de cada tarea o grupo lógico
