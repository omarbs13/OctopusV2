# Implementation Plan: Auditoría detallada de cambios

**Branch**: `018-audit-trail` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/018-audit-trail/spec.md`

## Summary

Completar la bitácora de la spec 007 para que cada cambio registre quién, cuándo, sobre qué registro
y con qué valores antes y después. También se agregan filtros por entidad, historial por registro y
exportación a PDF y Excel.

1. **Modelo** (research §1, §8):
   - `AuditEntry` gana tres columnas: `EntityName`, `Reason` y `Changes`.
   - `Changes` es un JSON con una lista de `AuditFieldChange` (campo, antes, después), ya
     formateados.
   - La migración `AuditTrail` solo agrega columnas e índices, sin reconstrucciones.
2. **Antes y después** (research §2, §3):
   - Cada entidad auditada tiene una instantánea en Application.
   - `AuditChanges.Compare`, `Created` y `Removed` calculan los cambios.
   - Nueva sobrecarga `IAuditLog.Add(AuditRecord)`. La firma actual se conserva.
3. **Cobertura nueva**:
   - Productos: `PRODUCT_CREATED`, `PRODUCT_UPDATED` (también desde `SetProductCritical`) y
     `PRODUCT_DELETED` (research §5).
   - Ediciones existentes con antes y después (research §6).
   - Una entrada `SALE_DISCOUNTS_APPLIED` por cada venta con descuento (research §7).
   - Motivo estructurado en cancelaciones, devoluciones y cajón.
4. **Consulta** (research §9, §10):
   - Filtros nuevos: entidad (`AuditEntityGroup`) e historial por registro.
   - Índices compuestos para responder en menos de 1 s con 1,000,000 de entradas.
5. **Exportación** (research §12):
   - `ExportAuditLog` genera el archivo PDF o XLSX con los escritores de la spec 009.
   - `ConfirmAuditExport` registra la exportación solo si el archivo se guardó.
6. **Pantalla** (research §13): se amplía `AuditLogView` con filtro de entidad, panel de detalle con
   la tabla de cambios, historial del registro y botones de exportación.

No se agrega ninguna dependencia externa.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12, CommunityToolkit.Mvvm, Hosting, Serilog,
EF Core 10 Sqlite, FluentValidation, ClosedXML y el generador PDF de la spec 009). **No se agrega
ninguna.**

**Storage**:

- SQLite. Migración `AuditTrail`: 3 columnas nulas en `AuditEntries`, 1 índice reemplazado y 3
  índices nuevos ([data-model.md](data-model.md)).
- `Version` 0.12.0 → 0.13.0. Base de ejemplo `v0.13.0.db` según
  [docs/migraciones.md](../../docs/migraciones.md).

**Testing**: xUnit v3 con la política mínima de la constitución v1.2.0 (research §14):

- **Domain**: `AuditFieldChangeTests` (antes y después iguales se rechaza).
- **Application**: `AuditChangesTests` (comparador) y `UserAuditFieldsTests` (sin secretos).
- **Infrastructure, sobre SQLite real**:
  - `ProductAuditTests`: atomicidad, sin entrada cuando no hay cambios y producto crítico.
  - `AuditAuthorTests`: el autor nunca queda vacío (FR-010).
  - `SaleDiscountAuditTests` (SC-007).
  - `AuditLogReaderTests` (se amplía): entidad, historial, autorizador y entradas anteriores.
  - `ExportAuditLogTests`: completitud y registro de la exportación.
- **Rendimiento**: `AuditLogPerformanceTests`, explícita, 1,000,000 de entradas (SC-002).
- **Migración**: `AuditTrailMigrationTests` y las bases de ejemplo.
- **Arquitectura**: las existentes, más la regla que prohíbe `ExecuteUpdate` y `ExecuteDelete`
  sobre `AuditEntries`.
- Sin pruebas de ViewModels, vistas, mapeos ni del formato del PDF.

**Target Platform**: Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Primera página de cualquier búsqueda en menos de 1 s con 1,000,000 de entradas (SC-002); la meta
  interna es 300 ms.
- Exportar 10,000 entradas en menos de 30 s (SC-006).
- Registrar la auditoría agrega menos de 5 ms a cada operación: es un `INSERT` más en la misma
  transacción.

**Constraints**:

- Funciona sin conexión.
- La entrada de bitácora va en la misma transacción que el cambio (FR-012).
- La bitácora nunca se modifica ni se borra (FR-013).
- 0 advertencias.
- Sin reconstrucción de tablas en la migración.
- La exportación no bloquea el hilo de la interfaz.

**Scale/Scope**:

- Unas 500 entradas diarias, unas 200,000 por año. Se dimensiona para 1,000,000.
- **Casos de uso nuevos (2)**: `ExportAuditLog` y `ConfirmAuditExport`.
- **Casos de uso que cambian**:
  - `SearchAuditLog`.
  - Productos: `CreateProduct`, `UpdateProduct`, `DeleteProduct`, `SetProductCritical`.
  - Categorías: `DeleteCategory`, `UpdateCategory`, `SetCategoryActive`, `CreateCategory`.
  - Usuarios: `CreateUser`, `UpdateUser`.
  - Clientes y cupones: `CreateCustomer`, `UpdateCustomer`, los casos de uso de cupones.
  - Configuración: devoluciones, crédito, descuentos y reportes.
  - Ventas y caja: `ConfirmSale`, `CancelSale`, `SaleReturnProcessor`, `OpenCashDrawer`.
- **Pantallas**: cambia "Auditoría". No hay pantallas nuevas.

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó después del diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Todo es local. La auditoría es un `INSERT` más dentro de la transacción existente de cada caso de uso: no agrega transacciones ni bloqueos a la venta. La exportación corre fuera del hilo de la interfaz y solo lee. |
| II. Capas | `AuditFieldChange` en Domain. Instantáneas, `AuditChanges`, `AuditEntityGroup` y casos de uso en Application/Audit y en la carpeta de cada funcionalidad. Lector, configuración EF y migración en Infrastructure. El ViewModel solo invoca casos de uso y escribe el archivo devuelto, como en los reportes. |
| III. Lógica en el núcleo | Qué campos se auditan y cómo se formatean vive en Application (instantáneas). La comparación vive en `AuditChanges`. La interfaz no calcula diferencias. |
| IV. Integridad de datos | GUID v7 y UTC. La bitácora es inmutable (`RejectImmutableChanges` más una prueba de arquitectura). Importes formateados desde el value object de dinero. Migración Code First solo con `ADD COLUMN` e índices, SQL revisado y base de ejemplo 0.13.0. `AuditEntry` sigue sin `UpdatedAt`, `Version` ni `DeletedAt`, con la justificación de la spec 007. |
| V. Multiplataforma | No hay código de plataforma. El diálogo de guardar ya es multiplataforma (reportes). |
| VI. Calidad verificable | Solo se prueban el comparador, la exclusión de secretos, la atomicidad y la completitud (integridad de datos), la exportación completa y las pruebas obligatorias de migración y arquitectura. SQLite real. El rendimiento se verifica con una prueba explícita antes de publicar. |
| VII. Simplicidad | Sin dependencias nuevas. JSON en una columna en lugar de una tabla hija (research §1). Se amplía `IAuditLog` con una sobrecarga en lugar de migrar los más de 40 llamadores. Se reutilizan la pantalla, el lector y los escritores de reportes. Sin triggers ni cadena de hashes (aclaración P2) y sin costo de producto (aclaración P1). |
| VIII. Soporte | Serilog registra cada exportación (formato, entradas, duración) y los rechazos. `docs/auditoria.md` (nuevo) documenta los eventos, los grupos de entidad, el formato de los cambios y la exportación. `docs/migraciones.md` agrega la sección 0.13.0. |
| IX. Seguridad local | Es el principio que esta funcionalidad refuerza: más operaciones sensibles quedan con autor, autorizador, motivo y antes y después. Solo el Administrador consulta y exporta. Nunca se registran contraseñas ni hashes (FR-006). |

**Resultado**: sin violaciones. Decisiones explícitas:

- **JSON en `AuditEntries` (research §1)**: es una desnormalización deliberada. Los cambios nunca se
  filtran por valor, y una sola fila mantiene la atomicidad y la inmutabilidad existentes.
- **Valores ya formateados (research §2)**: congelan la vista del dato en ese momento (FR-005). El
  costo es que no se pueden recalcular, y la spec no lo pide.
- **Registro de la exportación en dos pasos (research §12)**: difiere de `ExportReportHandler`
  porque la spec exige no registrar las exportaciones fallidas.
- **Descuentos (research §7)**: una entrada por venta en lugar de una por descuento autorizado. El
  evento anterior se conserva solo para mostrar las entradas viejas.

## Project Structure

### Documentation (this feature)

```text
specs/018-audit-trail/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── application-ports.md
│   └── ui.md
├── checklists/
└── tasks.md             # Lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Pos.Domain/
│   └── Audit/AuditEntry.cs  AuditFieldChange.cs                          # + EntityName, Reason, Changes
├── Pos.Application/
│   ├── Abstractions/IAuditLog.cs                                          # + Add(AuditRecord)
│   ├── Audit/
│   │   ├── AuditActions.cs                                                # + PRODUCT_*, SALE_DISCOUNTS_APPLIED, AUDIT_EXPORTED
│   │   ├── AuditRecord.cs  AuditChanges.cs  AuditEntityGroup.cs  AuditFormat.cs
│   │   ├── AuditDtos.cs  IAuditLogReader.cs                               # AuditFilter, AuditRow ampliado, ListAsync
│   │   ├── SearchAuditLog/                                                # + Entity, Record
│   │   ├── ExportAuditLog/  ConfirmAuditExport/                           # nuevos
│   ├── Products/ProductAuditFields.cs                                     # nuevo
│   ├── Products/CreateProduct/  UpdateProduct/  DeleteProduct/            # auditan
│   ├── Reports/SetProductCritical/                                        # PRODUCT_UPDATED (Crítico)
│   ├── Categories/CategoryAuditFields.cs  DeleteCategory/ …               # antes y después
│   ├── Users/UserAuditFields.cs  CreateUser/  UpdateUser/
│   ├── Customers/CustomerAuditFields.cs  …   Discounts/CouponAuditFields.cs  …
│   ├── Sales/ConfirmSale/ConfirmSaleHandler.cs                            # SALE_DISCOUNTS_APPLIED
│   ├── Sales/CancelSale/  Returns/SaleReturnProcessor.cs  Printing/OpenCashDrawer/   # Reason, EntityName
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── Audit/AuditLog.cs  AuditLogReader.cs                               # registro y lectura ampliados
│   ├── Persistence/AuditingInterceptor.cs                                 # autor Guid.Empty → SystemUser.Id
│   ├── Reports/XlsxReportWriter.cs                                        # sin hoja "Gráficas" si no hay gráficas
│   ├── Persistence/Configurations/AuditEntryConfiguration.cs              # columnas, ToJson, índices
│   └── Persistence/Migrations/…_AuditTrail.cs
├── Pos.Desktop/
│   ├── Administration/AuditLogView.axaml  AuditLogViewModel.cs            # entidad, detalle, historial, exportar
│   └── Resources/Strings.resx                                             # Audit_*
tests/
├── Pos.Domain.Tests/Audit/                   AuditFieldChangeTests
├── Pos.Application.Tests/Audit/              AuditChangesTests, UserAuditFieldsTests
├── Pos.Infrastructure.Tests/Audit/           ProductAuditTests, AuditAuthorTests, SaleDiscountAuditTests,
│                                             AuditLogReaderTests, ExportAuditLogTests,
│                                             AuditLogPerformanceTests (explícita)
├── Pos.Infrastructure.Tests/Discounts/       DiscountTestBase, ConfirmSaleApprovalTests (se ajustan)
├── Pos.Infrastructure.Tests/SampleDatabases/ v0.13.0.db, AuditTrailMigrationTests
└── Pos.ArchitectureTests/                    regla (revisa el código fuente): sin ExecuteUpdate/ExecuteDelete sobre AuditEntries
docs/
├── auditoria.md                 # nuevo: eventos, entidades, antes y después, exportación, inmutabilidad
├── usuarios-y-permisos.md       # la exportación de la bitácora es del Administrador
└── migraciones.md               # + sección 0.13.0 (sin reconstrucciones)
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de las specs 001 a
017.

- El núcleo de la auditoría vive en las carpetas `Audit/` existentes de cada capa.
- Cada instantánea vive junto a su entidad, en la carpeta de su funcionalidad (por ejemplo,
  `Products/ProductAuditFields.cs`), porque conoce sus campos y su formato.
- `AuditRow` y `AuditSearch` cambian de firma. Hoy solo los usan `AuditLogReader`,
  `SearchAuditLogHandler`, `AuditLogViewModel` y `AuditLogReaderTests`; se ajustan juntos.

## Complexity Tracking

Sin violaciones que justificar.
