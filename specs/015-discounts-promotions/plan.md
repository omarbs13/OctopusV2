# Implementation Plan: Descuentos y promociones

**Branch**: `015-discounts-promotions` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/015-discounts-promotions/spec.md`

## Summary

La funcionalidad permite aplicar descuentos por línea, a la venta completa o mediante un cupón. Los
cálculos son exactos al centavo, piden autorización de un Administrador cuando superan el límite y
dejan un rastro de quién aplicó y quién autorizó cada descuento. Todo depende del módulo licenciado
nuevo "Descuentos y promociones".

1. **Valores y cálculo** (research §1–§3):
   - `DiscountValue` guarda el porcentaje en puntos base o el monto en centavos.
   - `DiscountMath` redondea mitad hacia arriba, igual que `SaleMath`.
   - La comparación con el límite usa multiplicación cruzada entera, así que "= límite" pasa sin
     autorización.
2. **La venta en curso** (`Cart`) calcula todo: descuento de línea, después el subtotal y al final un
   único descuento de venta (manual **o** cupón, porque son excluyentes). La interfaz solo lo muestra.
3. **El descuento de venta se reparte entre las líneas** por resto mayor (research §4–§5).
   `SaleLine.AmountCents` pasa a ser el importe neto pagado. Así las devoluciones de 013 y los reportes
   existentes quedan correctos sin tocar `ReturnMath`. Cada descuento queda en `SaleDiscounts`, que es
   inmutable.
4. **Autorización persistente** (research §7):
   - `ApproveDiscount` consume la concesión de 2 minutos de 007 al **aplicar** el descuento y guarda un
     `DiscountApproval` ligado al `DraftId`.
   - `ConfirmSale` revalida con el límite vigente y exige una aprobación que cubra el porcentaje
     equivalente.
   - Funciona con la venta conservada y con los cambios de cantidad.
5. **Cupones** (research §9):
   - Agregado `Coupon` con código único que no puede coincidir con productos y estado derivado de la
     fecha local.
   - Los usos se cuentan dentro de la transacción del cobro y la cancelación completa los devuelve.
   - Se detectan en el mismo campo de captura de productos.
6. **Ticket, consulta y reportes** (research §12):
   - El ticket desglosa los descuentos ([contracts/ticket-format.md](contracts/ticket-format.md)).
   - El reporte de ventas agrega "Total descontado".
   - Hay un reporte nuevo de descuentos, exportable.
7. **Licencia, permisos y bitácora** (research §8, §13): `LicensedModule.Discounts`, 4 permisos y 7
   acciones de bitácora.
8. **Venta de total 0** (research §6): se permite sin pagos cuando hay un descuento del 100 % autorizado.

Hay una migración nueva, `DiscountsAndCoupons`: 3 tablas, 4 columnas y un `UPDATE` de datos, sin
reconstruir ninguna tabla. No se agrega ninguna dependencia.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia, CommunityToolkit.Mvvm, Hosting, Serilog, EF Core 10
Sqlite, FluentValidation). **No se agrega ninguna.**

**Storage**:

- SQLite, migración `DiscountsAndCoupons`:
  - Tablas `Coupons`, `SaleDiscounts` y `DiscountApprovals`.
  - Columnas `Sales.DiscountCents`, `SaleLines.OriginalAmountCents`, `LineDiscountCents` y
    `OrderDiscountCents`.
  - `UPDATE SaleLines SET OriginalAmountCents = AmountCents`.
- El límite de descuento se guarda en preferencias locales (`IPreferencesStore`), sin tabla.
- El borrador (`SaleDraft.LinesJson`) agrega campos opcionales y es compatible hacia atrás.
- `Version` pasa de 0.9.0 a 0.10.0, con una base de ejemplo nueva según [docs/migraciones.md](../../docs/migraciones.md).

**Testing**: xUnit, con la política mínima de la constitución v1.2.0 (detalle en research §14):

- **Domain**:
  - `DiscountValue`, `DiscountMath` (redondeo y límite), `Proportional.Allocate`.
  - `Cart` con descuentos, `Coupon`, `Sale.Register` con total 0.
  - `RolePermissions` y `ModuleAccess`.
- **Casos de uso sobre SQLite real**:
  - Aprobaciones en `ConfirmSale`.
  - Último uso del cupón en concurrencia.
  - Cupón vencido al cobrar.
  - Cancelación que devuelve el uso.
  - Devolución de una línea con descuento global.
  - Total del reporte.
  - Colisión entre el código de un cupón y un producto.
- **Obligatorias**: migración de las bases de ejemplo, consistencia de inventario y arquitectura.
- Sin pruebas de ViewModels, vistas ni ticket.

**Target Platform**: Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Aplicar un descuento dentro del límite tarda menos de 10 s de operación (SC-005). El cálculo es en
  memoria y tarda menos de 1 ms.
- Un cupón escaneado se aplica en menos de 2 s (SC-005). Es una búsqueda por índice único y tarda
  menos de 20 ms.
- `ConfirmSale` agrega a lo más una lectura de aprobaciones por `DraftId`, una actualización de
  cupón y N inserciones en `SaleDiscounts`: menos de 30 ms adicionales.
- El reporte de descuentos de un mes tarda menos de 500 ms con 50 000 ventas, gracias al índice por
  fecha.

**Constraints**:

- Funciona sin red.
- Cada cobro es una sola transacción `BEGIN IMMEDIATE`.
- Importes enteros en centavos y porcentajes en puntos base.
- Una venta sin descuentos se comporta y se imprime **exactamente igual** que antes.

**Scale/Scope**: una caja por instalación, cientos de cupones, decenas de miles de ventas por año. Se
agregan 1 menú de administración (con 3 vistas), 2 diálogos en el punto de venta y 1 reporte.

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó después del diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Todo es local. El cobro con descuentos, el consumo del cupón y la cancelación con devolución del uso ocurren cada uno en una sola transacción. Un cupón que deja de ser válido o una aprobación faltante devuelven un error de negocio: la venta en curso se conserva y la interfaz guía al cajero. Si falta la licencia, la venta básica sigue operando sin descuentos. |
| II. Capas | `DiscountValue`, `DiscountMath`, `Proportional`, `Coupon`, `DiscountApproval` y `SaleDiscount` viven en Domain. Los casos de uso y los puertos (`ICouponRepository`, `IDiscountApprovalStore`, `IDiscountSettingsStore`, `IDiscountReportReader`) viven en Application. Las implementaciones EF Core y de preferencias viven en Infrastructure. La carpeta nueva `Discounts/` queda cubierta por las pruebas de arquitectura existentes. |
| III. Lógica en el núcleo | Los importes, el subtotal, el reparto, el límite y el estado del cupón se calculan en Domain (`Cart`, `DiscountMath`, `Coupon`). El ViewModel no calcula descuentos (este principio lo menciona explícitamente). |
| IV. Integridad de datos | GUID v7, fechas UTC (la vigencia se guarda como fecha local, documentado), centavos y puntos base enteros. `Coupon` tiene `Version` y no se borra. `SaleDiscount` es inmutable. El código del cupón es único por índice. La migración es de EF Core, sin reconstrucciones, con SQL revisado y base de ejemplo 0.10.0. Hay una desviación en `DiscountApprovals` (ver Complexity Tracking). |
| V. Multiplataforma | Sin código de plataforma. El ticket usa los adaptadores de 006. |
| VI. Calidad verificable | Solo se prueban los cálculos de dinero (redondeo, reparto, total y devolución neta), las validaciones de integridad (límite y aprobación, usos del cupón, colisión de códigos) y las pruebas obligatorias de migración, inventario y arquitectura. La persistencia se prueba sobre SQLite real. |
| VII. Simplicidad | Sin dependencias nuevas. Se reutilizan `AuthorizeAdmin`, las concesiones, `IPreferencesStore`, `ExportReportHandler` y `ReturnMath.Allocate` (extraído). No hay promociones automáticas, cupones por producto ni límites por usuario. Los repositorios son específicos por agregado. |
| VIII. Soporte | Serilog registra cada aprobación, cada rechazo por aprobación faltante o cupón inválido, y cada consumo o devolución de uso, con `DraftId`, folio, usuario y montos (nunca la contraseña). `docs/descuentos.md` documenta el cálculo, el reparto, la aprobación persistente, los cupones y el GUID del módulo para la herramienta de licencias. |
| IX. Seguridad local | Los descuentos fuera de rango exigen la contraseña de un Administrador (este principio lo menciona explícitamente). La bitácora guarda el aplicador, el autorizador, la venta y el monto. Los intentos fallidos se registran sin la contraseña. El Cajero no gestiona cupones ni el límite. |

**Resultado**: sin violaciones no justificadas. Decisiones explícitas incorporadas a la especificación
durante el plan:

- **FR-004**: el residuo de redondeo del reparto se asigna por resto mayor (como 013), no "a las
  líneas de mayor importe".
- **Casos límite, cambio de límite**: los descuentos se revalidan al cobrar con el límite vigente.
  Los que ya tienen aprobación siguen válidos (research §7).

## Project Structure

### Documentation (this feature)

```text
specs/015-discounts-promotions/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── application-ports.md
│   ├── ui.md
│   └── ticket-format.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/Pos.Domain/
├── Common/Proportional.cs                 # nuevo (extraído de ReturnMath.Allocate)
├── Discounts/                             # nuevo
│   ├── DiscountMode.cs, DiscountValue.cs, DiscountMath.cs
│   ├── DiscountScope.cs, DiscountKind.cs
│   ├── Coupon.cs, CouponStatus.cs
│   └── DiscountApproval.cs
├── Sales/Cart.cs, CartLine.cs, Sale.cs, SaleLine.cs, Checkout.cs   # cambian
├── Sales/SaleDiscount.cs, OrderDiscount.cs                         # nuevos
├── Returns/ReturnMath.cs                  # delega a Proportional
├── Licensing/LicensedModule.cs, ModuleCatalog.cs, ModuleAccess.cs  # Discounts
└── Users/Permission.cs, RolePermissions.cs                         # 4 permisos

src/Pos.Application/
├── Discounts/                             # nuevo
│   ├── ApproveDiscount/, ResolveCoupon/
│   ├── Coupons/{SaveCoupon,SearchCoupons,GetCoupon,SetCouponActive}/
│   ├── Settings/{GetDiscountSettings,SaveDiscountSettings}/
│   ├── GetDiscountReport/
│   ├── ICouponRepository.cs, IDiscountApprovalStore.cs
│   ├── IDiscountSettingsStore.cs, IDiscountReportReader.cs
│   └── DiscountDtos.cs, DiscountMessages.cs, DiscountFields.cs
├── Sales/ConfirmSale/, FindProductsForSale/, SaveSaleDraft/, GetSaleDraft/, GetSale/, SaleDtos.cs  # cambian
├── Returns/SaleReturnProcessor.cs         # devuelve el uso del cupón
├── Reports/GetSalesReport/, Reports/Export/   # DiscountCents, tipo de reporte Discounts
├── Printing/Ticket/TicketBuilder.cs       # desglose
└── Audit/AuditActions.cs                  # 7 acciones

src/Pos.Infrastructure/
├── Discounts/                             # CouponRepository, DiscountApprovalStore,
│                                          # PreferencesDiscountSettingsStore, DiscountReportReader
├── Persistence/Configurations/            # Coupon, SaleDiscount, DiscountApproval; Sale/SaleLine cambian
├── Persistence/Migrations/*_DiscountsAndCoupons.cs
├── Sales/                                 # borrador JSON con descuentos
└── Reports/                               # SalesReportReader suma DiscountCents

src/Pos.Desktop/
├── Sales/                                 # diálogo de descuento, cupón, pie con subtotal
├── Discounts/                             # Cupones, Configuración, Reporte
└── Navigation/                            # menú "Descuentos" según permiso y módulo

tests/
├── Pos.Domain.Tests/Discounts/, Sales/ (Cart y Sale con descuentos)
├── Pos.Application.Tests/Discounts/, Sales/ConfirmSale (SQLite real)
├── Pos.Infrastructure.Tests/ (migración y bases de ejemplo 0.10.0)
└── Pos.ArchitectureTests/ (sin cambios en reglas)

docs/descuentos.md                         # nuevo
```

**Structure Decision**: se usa la estructura de capas existente (Principio II), con una carpeta de
funcionalidad `Discounts/` en cada capa. Los cambios a la venta se quedan en `Sales/`, porque el
descuento es parte del agregado `Sale`.

## Complexity Tracking

| Desviación | Por qué hace falta | Alternativa más simple rechazada porque |
|---|---|---|
| `DiscountApprovals` sin `Version`, sin `UpdatedAt`/`UpdatedBy` y sin borrado lógico (Principio IV) | Son registros inmutables de un evento, como `AuditEntry` y `ReceivableEntry`. Nunca se editan ni se borran. | Agregar los campos de auditoría completos no aporta nada a un registro que no cambia. Guardar la aprobación solo en memoria la perdería en la venta conservada y dejaría la verificación en la interfaz. |
| `SaleDiscounts` sin `Version` ni borrado lógico | Son parte inmutable de una venta registrada, igual que `SaleLines`. | Lo mismo que el renglón anterior. |
| `Coupons.StartsOn` y `EndsOn` como fecha local, no UTC (Principio IV) | La vigencia es un día de calendario del negocio ("del 1 al 31 de octubre"), no un instante. | Guardar instantes UTC haría que la vigencia dependa de la zona horaria al editarla y complicaría "el día de fin es válido completo". |
| `SaleLine.AmountCents` cambia de significado (bruto → neto) | Así las devoluciones (013) y los reportes que suman importes usan lo pagado sin cambios. | Agregar una columna neta separada obligaría a cambiar `ReturnMath` y cada consulta de reportes. En las ventas existentes bruto = neto, así que el dato histórico no cambia. |
