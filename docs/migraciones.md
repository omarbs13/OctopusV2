# Migraciones de la base de datos

Reglas y procedimiento para cambiar el esquema de la base local (constitución, Principio IV).

## Reglas

- **Code First**: el modelo en C# (entidades de `Pos.Domain` y configuraciones en
  `src/Pos.Infrastructure/Persistence/Configurations/`) es la fuente de verdad. No se escriben
  scripts SQL de esquema a mano.
- **Una migración publicada nunca se modifica ni se elimina.** Las correcciones se hacen con
  migraciones nuevas. "Publicada" significa que llegó a `main` o que se entregó a algún cliente.
- **El SQL generado se revisa antes de integrarlo**, con especial atención a las
  reconstrucciones de tablas (ver más abajo).
- **Cada versión publicada conserva una base de ejemplo**, y una prueba automática migra todas
  a la versión actual.
- **Siembra de datos**: los catálogos fijos (por ejemplo, unidades de medida) se siembran con
  `HasData` en la configuración de la entidad. Los datos propios de cada instalación (sucursal,
  primer usuario...) se crean en el asistente de primer arranque, nunca con `HasData`.

## Crear una migración

```bash
dotnet tool restore
dotnet ef migrations add <NombreDescriptivo> --project src/Pos.Infrastructure --output-dir Persistence/Migrations
```

Usa nombres en inglés que describan el cambio: `AddProductCategory`, `AddSaleTables`...

Las migraciones se generan en `src/Pos.Infrastructure/Persistence/Migrations/`. Esa carpeta está
marcada como código generado en `.editorconfig`, así que las reglas de estilo no aplican ahí.
Tampoco se editan a mano.

## Revisar el SQL

SQLite **no** admite scripts idempotentes (`--idempotent`). Genera el script entre la última
migración publicada y la nueva:

```bash
# Todo el esquema desde cero
dotnet ef migrations script --project src/Pos.Infrastructure -o migracion.sql

# Solo lo que cambia entre dos migraciones
dotnet ef migrations script <UltimaPublicada> <Nueva> --project src/Pos.Infrastructure -o migracion.sql
```

Qué revisar:

1. **Reconstrucciones de tabla.** SQLite no puede alterar columnas, cambiar tipos ni quitar
   restricciones; EF Core lo resuelve creando `ef_temp_<Tabla>`, copiando los datos, borrando la
   tabla original y renombrando. Verifica que el `INSERT INTO ... SELECT` copie **todas** las
   columnas con los valores correctos, sobre todo si agregas una columna `NOT NULL` (necesita un
   valor por defecto para las filas existentes).
2. **Índices parciales.** Los índices únicos de SKU y código de barras filtran por
   `"DeletedAt" IS NULL`. Si una migración los recrea, el filtro debe mantenerse.
3. **Pérdida de datos.** EF Core advierte con "An operation was scaffolded that may result in the
   loss of data". No lo ignores: ajusta la migración (con una migración nueva) o documenta por
   qué es seguro.

### Ejemplo: reconstrucción de `Products` en `ProductCatalogImprovements`

Agregar `Products.UnitCode` con clave foránea a `UnitsOfMeasure` obliga a EF Core a reconstruir
`Products`. En el SQL generado se verificó:

- La columna nueva se agrega primero con `NOT NULL DEFAULT 'H87'` (`HasDefaultValue` en el
  modelo), así los productos existentes quedan con "Pieza" antes de copiarse.
- Las 8 unidades se insertan (`HasData`) antes de crear la tabla con la clave foránea.
- El `INSERT INTO "ef_temp_Products" ... SELECT ... FROM "Products"` copia **todas** las
  columnas, incluidas `Version` y `DeletedAt`, y todas las filas, incluidas las borradas.
- Los índices `IX_Products_Sku`, `IX_Products_Barcode` e `IX_Products_NameSearch` se recrean
  con su filtro `WHERE "DeletedAt" IS NULL`.
- `ProductImages` referencia a `Products`; la llave foránea sigue siendo válida porque la tabla
  reconstruida conserva su nombre.

`SampleDatabaseUpgradeTests` lo comprueba sobre `v0.1.0.db`: unidad asignada, índices con su
filtro y `PRAGMA foreign_key_check` sin violaciones.

Al aplicarla, EF Core registra la advertencia `NonTransactionalMigrationOperationWarning` porque
`PRAGMA foreign_keys = 0` no puede ir dentro de una transacción. Es esperado en toda
reconstrucción de tabla en SQLite; la protección es el orden de arranque: respaldo previo y
restauración automática si la migración falla.

## Cómo se aplican las migraciones

Al arrancar, la aplicación sigue este orden obligatorio (ver
`src/Pos.Application/Startup/DatabaseStartup.cs`):

1. Verifica que haya una sola instancia en ejecución.
2. Verifica la integridad de la base (`PRAGMA quick_check`).
3. Detecta si la base es **más nueva** que la aplicación: tiene migraciones aplicadas que esta
   versión no conoce. En ese caso no la abre ni la modifica.
4. Si hay migraciones pendientes, comprueba que haya espacio libre (al menos el doble del tamaño
   de la base) y la **respalda** en `backups/pre-migration/` con la API de backup de SQLite.
5. **Migra.** Si falla, restaura el respaldo, registra el error, informa al operador y no
   continúa.

## Base de ejemplo de cada versión

Antes de publicar una versión nueva (cuando `Version` cambia en `Directory.Build.props`):

```bash
POS_GENERATE_SAMPLE_DB=1 dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.SampleDatabases.SampleDatabaseGenerator"
```

Esto crea `tests/Pos.Infrastructure.Tests/SampleDatabases/v<versión>.db` con los datos de
`SampleData`: 20 productos, incluidos inactivos, uno borrado y uno con acentos. Desde 0.2.0
también hay uno por kilo, uno con imagen y uno con precio 0 (permitido antes de 0.2.0). Desde 0.3.0
el producto por kilo controla inventario (mínimo 5) y tiene movimientos de los cuatro tipos, con
existencia final de 10 kg; los productos de versiones anteriores quedan sin control de inventario.
El generador se
niega a sobrescribir un archivo existente. Agrega el `.db` al commit; nunca lo modifiques
después.

`SampleDatabaseUpgradeTests` toma **cada** `v*.db`, lo migra a la versión actual con la secuencia
real de arranque y verifica la integridad y los datos. Si una migración nueva rompe una base
antigua, esta prueba falla.

Desde 0.4.0 la base de ejemplo también trae 3 ventas (una cancelada, con su movimiento de regreso y
su entrada de bitácora), una pieza con inventario en −2 por una venta y un borrador de venta. La
migración `SalesModule` solo crea las 5 tablas nuevas (`Sales`, `SaleLines`, `SalePayments`,
`SaleDrafts` y `AuditEntries`) con sus índices: la liga entre venta y movimiento vive en
`SaleLines`, así que **no** reconstruye `Products`, `ProductStocks` ni `InventoryMovements`.
`SalesModuleMigrationTests` revisa el SQL generado y falla si aparece un `ALTER TABLE`, un
`DROP TABLE` o la reconstrucción de una tabla existente.

Desde 0.5.0 (usuarios y roles) la base de ejemplo `v0.5.0.db` se generó con el esquema de
`BusinessProfile`, **antes** de crear la migración `UsersAndRoles`, y trae un borrador en `SaleDrafts`.
La migración `UsersAndRoles`:

- Crea `Users` y siembra con `HasData` al usuario "Sistema" con el id fijo que ya tienen todos los
  registros previos (`00000000-0000-7000-8000-000000000001`): no actualiza datos.
- Agrega `AuditEntries.AuthorizedBy` (columna nula) y los índices de la bitácora y de las ventas por
  cajero: no reconstruyen tablas.
- **Reconstruye `SaleDrafts`** (la llave primaria pasa de `Slot` a `UserId`). Igual que `Products.UnitCode`,
  la columna nueva entra con un valor por defecto (el id de "Sistema") para que la fila existente se
  copie a la tabla temporal y conserve su venta en curso. EF Core avisa de que una llave no debería
  tener un valor por defecto; el aviso se ignora a propósito en `PosDbContext.OnConfiguring`.
  `SampleDatabaseUpgradeTests` verifica que el borrador de `v0.5.0.db` quede bajo "Sistema" con sus
  líneas y que desaparezca `CK_SaleDrafts_Slot`. Al crear el primer administrador en el asistente, la
  venta conservada de "Sistema" se reasigna a ese administrador.

Desde 0.6.0 (turnos de caja) la base de ejemplo `v0.6.0.db` se generó con el esquema de
`UsersAndRoles`, **antes** de crear la migración `CashShifts`, y trae un administrador, un cajero y las
ventas de ambos (el administrador hizo la 1; el cajero, la 2 y la 3 y el borrador). La migración
`CashShifts` **no reconstruye ninguna tabla**:

- Crea `CashShifts` y `CashMovements` (esta con llave foránea `Restrict` hacia `CashShifts`, porque la
  tabla es nueva) con sus índices, entre ellos el índice único filtrado
  `IX_CashShifts_OpenPerRegister ... WHERE "Status" = 'OPEN'` (un solo turno abierto por caja).
- Agrega `Sales.CashShiftId` con `ALTER TABLE "Sales" ADD "CashShiftId" TEXT NULL`, **sin llave
  foránea** (una llave foránea obligaría a EF Core a reconstruir `Sales`, la tabla más grande) y el
  índice `IX_Sales_CashShiftId_Status`. Las ventas anteriores quedan con `NULL`.
- No actualiza datos.

`CashShiftsMigrationTests` revisa el SQL y falla si contiene `DROP TABLE` o `ef_temp_`.
`SampleDatabaseUpgradeTests` verifica, tras migrar cada base de ejemplo, que las ventas conservan sus
datos con `CashShiftId` nulo, que `CashShifts` y `CashMovements` están vacías y que el índice único
filtrado existe. Ver [turnos-de-caja.md](turnos-de-caja.md).

Cuando una funcionalidad agregue tablas nuevas, amplía `SampleData` para que la base de ejemplo
de la siguiente versión también tenga datos en ellas, y agrega verificaciones en
`SampleDatabaseUpgradeTests`.

## 0.8.0: devoluciones y notas de crédito (`ReturnsAndCreditNotes`)

Desde 0.8.0 (devoluciones) la base de ejemplo `v0.8.0.db` trae, además de lo anterior, una devolución
parcial de la venta 1 compensada con una nota de crédito. La migración `ReturnsAndCreditNotes`
**no reconstruye ninguna tabla**:

- Crea `SaleReturns`, `SaleReturnLines`, `SaleReturnRefunds`, `CreditNotes` y `CreditNoteMovements` con
  llaves foráneas `Restrict` (las tablas son nuevas) y sus índices, entre ellos el filtrado
  `IX_SaleReturnRefunds_Pending ... WHERE "Status" = 'PENDING_REVERSAL'`.
- Agrega seis columnas con `ALTER TABLE ... ADD`: `Sales.ReturnedCents` y `SaleLines.ReturnedQuantity`
  (`NOT NULL DEFAULT 0`), `SalePayments.CreditNoteId` y `CashShifts.CashRefundsCents`,
  `NonCashRefundsCents` y `CreditNotesIssuedCents` (nulas). `SalePayments.CreditNoteId` va **sin llave
  foránea**, igual que `Sales.CashShiftId`, para no reconstruir la tabla.
- No rellena datos: las ventas ya canceladas siguen sin `SaleReturn` y conservan la regla heredada del
  efectivo cancelado; los turnos ya cerrados quedan con las tres columnas en `NULL` (el corte los muestra
  como 0). `PaymentMethod.CreditNote` usa el código `CREDIT` para caber en `SalePayments.Method` (`TEXT(10)`).

`ReturnsMigrationTests` revisa el SQL y falla si contiene `DROP TABLE` o `ef_temp_`.
`SampleDatabaseUpgradeTests` migra de `v0.1.0.db` a `v0.8.0.db`, verifica que las tablas nuevas existan
(vacías, salvo en `v0.8.0.db`), que los acumulados valgan 0 y que las ventas ya canceladas conserven su
efectivo heredado en los totales del turno. Ver [devoluciones.md](devoluciones.md).

## 0.9.0: clientes y crédito (`CustomersAndCredit`)

La base de ejemplo `v0.9.0.db` trae, además de lo anterior, un cliente con crédito, una venta a crédito
del cajero (la venta 4, del producto sin inventario) y un abono en efectivo, con su borrador conservado.
La migración `CustomersAndCredit` **no reconstruye ninguna tabla**:

- Crea `Customers`, `Receivables`, `ReceivableEntries` y `CustomerPayments` con sus índices, entre ellos
  el único filtrado `IX_Customers_TaxId ... WHERE "TaxId" IS NOT NULL` y los únicos de `Receivables.SaleId`,
  `CustomerPayments.Number` y `CustomerPayments.RequestId`. Las llaves foráneas (`Restrict`) van solo de
  las tablas nuevas hacia `Sales`, `Customers` y `CashShifts`.
- Agrega a `CashShifts` cinco columnas nulas con `ALTER TABLE ... ADD`: `OnAccountSalesCents`,
  `CustomerPaymentsCashCents`, `CustomerPaymentsNonCashCents`, `CustomerPaymentVoidsCashCents` y
  `CustomerPaymentVoidsNonCashCents`. Los turnos ya cerrados quedan en `NULL` y el corte no muestra el
  bloque "Crédito".
- No rellena datos: ninguna venta existente es a crédito. `PaymentMethod.OnAccount` usa el código
  `ACCOUNT` y `RefundStatus.Settled` el código `SETTLED`; ambos caben en las columnas actuales.

`CustomersMigrationTests` revisa el SQL (sin `DROP TABLE`, `ef_temp_`, `INSERT` ni `UPDATE`) y que los
turnos cerrados antes de 0.9.0 queden con el bloque en nulo. `SampleDatabaseUpgradeTests` migra de
`v0.1.0.db` a `v0.9.0.db`, verifica que las tablas nuevas existan (vacías, salvo en `v0.9.0.db`) y que en
toda cuenta el saldo sea igual al original más su libro. Ver [clientes-y-credito.md](clientes-y-credito.md).

## 0.10.0: descuentos y cupones (`DiscountsAndCoupons`)

La base de ejemplo `v0.10.0.db` trae, además de lo anterior, el cupón `MUESTRA10` (10 %, 5 usos, 1
usado), dos ventas del cajero con descuento (la 5, con un descuento de línea de $15.00 autorizado por el
administrador, y la 6, con el cupón) y una venta conservada con un descuento de línea del 5 %. La
migración `DiscountsAndCoupons` **no reconstruye ninguna tabla**:

- Crea `Coupons` (índice único `IX_Coupons_Code`), `DiscountApprovals` y `SaleDiscounts`. Las llaves
  foráneas (`Restrict`) van solo de `SaleDiscounts` hacia `Sales` y `Coupons`.
- Agrega cuatro columnas `NOT NULL DEFAULT 0` con `ALTER TABLE ... ADD`: `Sales.DiscountCents`,
  `SaleLines.OriginalAmountCents`, `SaleLines.LineDiscountCents` y `SaleLines.OrderDiscountCents`.
- **Rellena un dato**: `UPDATE "SaleLines" SET "OriginalAmountCents" = "AmountCents"`, agregado a mano
  con `migrationBuilder.Sql` después de las columnas (es la única excepción a "no se editan a mano": EF
  no genera actualizaciones de datos). En las ventas anteriores el importe original es el registrado.

`DiscountsMigrationTests` revisa el SQL (sin `DROP TABLE` ni `ef_temp_`, exactamente cuatro `ALTER
TABLE` y el `UPDATE`) y que al migrar `v0.9.0.db` toda línea quede con `OriginalAmountCents =
AmountCents` y sin descuentos. `SampleDatabaseUpgradeTests` migra de `v0.1.0.db` a `v0.10.0.db`, verifica
que las tablas nuevas existan (vacías, salvo en `v0.10.0.db`) y que en toda venta las líneas sumen el
total y el descontado sea la suma de sus descuentos. Ver [descuentos.md](descuentos.md).

## 0.11.0: categorías de productos (`ProductCategories`)

La base de ejemplo `v0.11.0.db` trae, además de lo anterior, tres categorías: "Bebidas de muestra"
(activa, con el producto sin inventario y el del cupón, ambos vendidos), "Lácteos de muestra" (inactiva, con
el producto inactivo `REF-001`) y "Temporal de muestra" (borrada, sin productos); el resto de los productos
queda sin categoría. La migración `ProductCategories` **no reconstruye ninguna tabla**:

- Crea `Categories` con el índice único filtrado `IX_Categories_NameKey ... WHERE "DeletedAt" IS NULL` y
  `IX_Categories_IsActive_NameKey`.
- Agrega `Products.CategoryId TEXT NULL` con `ALTER TABLE ... ADD` y su índice
  `IX_Products_CategoryId ... WHERE "DeletedAt" IS NULL`. **Sin `FOREIGN KEY`**: agregarla obligaría a
  reconstruir `Products`; la integridad la dan los casos de uso (ver [categorias.md](categorias.md)).
- No rellena datos: todos los productos existentes quedan "Sin categoría".

`ProductCategoriesMigrationTests` revisa el SQL (un solo `ALTER TABLE`, sin `FOREIGN KEY`, `DROP TABLE` ni
`ef_temp_`) y que al migrar `v0.10.0.db` los productos conserven sus datos con `CategoryId` nulo.
`SampleDatabaseUpgradeTests` migra de `v0.1.0.db` a `v0.11.0.db`, verifica que `Categories` exista (vacía,
salvo en `v0.11.0.db`) y que ningún producto no borrado apunte a una categoría borrada o inexistente.

## 0.12.0: Corte X y Corte Z (`ShiftCuts`)

La base de ejemplo `v0.12.0.db` trae, además de lo anterior, tres turnos: `T-000001` cerrado **sin**
Corte Z (como los turnos cerrados antes de la actualización), `T-000002` del cajero con un Corte X
(`X-000001`) y cerrado por el administrador con un Corte Z (`Z-000001`, faltante de $5.00 con
comentario), y `T-000003` abierto. La venta conservada del cajero se vuelve a guardar después del cierre.
La migración `ShiftCuts` **no reconstruye ninguna tabla**:

- Crea `ShiftCuts` con llave foránea `Restrict` hacia `CashShifts` (la tabla es nueva, así que no
  reconstruye nada) y cinco índices: `IX_ShiftCuts_Type_Number` (único: folio por tipo),
  `IX_ShiftCuts_ClosingPerShift` (único filtrado `WHERE "Type" = 'Z'`: un Corte Z por turno),
  `IX_ShiftCuts_ShiftId`, `IX_ShiftCuts_GeneratedAt` e `IX_ShiftCuts_GeneratedBy_GeneratedAt`.
- No toca `CashShifts` ni inserta filas: los turnos cerrados antes de 0.12.0 **no reciben Corte Z
  retroactivo** y no aparecen en el histórico de cortes. El primer Corte Z posterior es `Z-000001`.

`ShiftCutsMigrationTests` revisa el SQL (un solo `CREATE TABLE`, los cinco índices, sin `ALTER TABLE`,
`DROP TABLE`, `INSERT` ni `ef_temp_`) y que al migrar `v0.11.0.db` los turnos conserven sus datos y
`ShiftCuts` quede vacía. `SampleDatabaseUpgradeTests` verifica que `ShiftCuts` esté vacía en las bases
anteriores y que `v0.12.0.db` conserve su Corte X y su Corte Z con sus folios y cifras. Ver
[turnos-de-caja.md](turnos-de-caja.md).

## 0.13.0: auditoría detallada (`AuditTrail`)

La migración `AuditTrail` **no reconstruye ninguna tabla** ni transforma datos:

- Agrega a `AuditEntries` tres columnas nulas con `ALTER TABLE ... ADD`: `EntityName`, `Reason` y `Changes`
  (JSON con la lista de cambios de campo, `OwnsMany(...).ToJson("Changes")`). Las entradas anteriores
  quedan con las tres en `NULL` y siguen mostrando su texto en `Details`.
- Recrea dos índices con más columnas: `IX_AuditEntries_CreatedAt` pasa a `(CreatedAt, Id)` (el orden de
  la página sale del índice, sin ordenar en memoria) e `IX_AuditEntries_CreatedBy` a `(CreatedBy, CreatedAt)`.
- Crea `IX_AuditEntries_AuthorizedBy`, `IX_AuditEntries_Action_CreatedAt` e
  `IX_AuditEntries_EntityType_CreatedAt`. `IX_AuditEntries_Entity` no cambia.

`AuditTrailMigrationTests` revisa el SQL (exactamente tres `ALTER TABLE ... ADD`, los cinco índices, sin
`CREATE TABLE`, `DROP TABLE`, `UPDATE`, `INSERT` en la bitácora ni `ef_temp_`) y que al migrar `v0.12.0.db`
las entradas conserven sus datos con las columnas nuevas en nulo. La base de ejemplo `v0.13.0.db` trae
entradas con `EntityName`, `Reason` y `Changes` (la cancelación, la devolución y las ventas con descuento de
`SampleData`). Ver [auditoria.md](auditoria.md).

## 0.14.0: proveedores y compras (`SuppliersAndPurchases`)

La migración `SuppliersAndPurchases` **no reconstruye ninguna tabla** ni toca datos:

- Crea `Suppliers`, `Purchases` y `PurchaseLines` con llaves foráneas `Restrict` hacia `Suppliers`,
  `Products` e `InventoryMovements` (y `Cascade` de la línea a su compra, que nunca se borra). Como las
  tablas son nuevas, ninguna llave obliga a reconstruir.
- Crea diez índices, entre ellos `IX_Suppliers_TaxId` (único filtrado `WHERE "TaxId" IS NOT NULL`),
  `IX_Purchases_Supplier_InvoiceKey` (único filtrado `WHERE "Status" = 'ACTIVE'`: una factura vigente por
  proveedor), `IX_Purchases_InvoiceDate` (`InvoiceDate`, `CreatedAt`, `Id`) para el reporte y
  `IX_PurchaseLines_MovementId` / `IX_PurchaseLines_VoidMovementId` (únicos) para el enlace con el kárdex.
- Los tipos de movimiento nuevos `PURCHASE` y `PURCH_VOID` son solo valores nuevos de la columna existente
  `InventoryMovements.Type` (`TEXT(12)`): `PURCH_VOID` y no `PURCHASE_VOID` (13 caracteres) para no cambiar
  la longitud y reconstruir la tabla. El enlace movimiento-compra vive en `PurchaseLines` por la misma razón.

`SuppliersAndPurchasesMigrationTests` revisa el SQL (exactamente tres `CREATE TABLE`, diez índices y ningún
`ALTER TABLE`, `DROP TABLE`, `UPDATE`, `INSERT` de datos ni `ef_temp_`) y que al migrar `v0.13.0.db` se
conserven los movimientos de inventario y las existencias con las tablas nuevas vacías.

La base `v0.13.0.db` no se había versionado al publicar 0.13.0; se generó con el código de 0.13.0 antes de
agregar esta migración. Al incorporarla, `SampleDatabaseUpgradeTests` espera para 0.13.0 la entrada
`SALE_DISCOUNTS_APPLIED` autorizada (formato de 018) en lugar de `DISCOUNT_APPLIED_AUTHORIZED`.

La base de ejemplo `v0.14.0.db` trae, además de lo anterior, tres proveedores (uno activo a crédito con
RUC, uno inactivo y uno sin RUC), la compra vigente `FAC-0001` (2.5 kg del producto por kilo y 1 pieza
bonificada del producto en negativo, con impuestos) y la compra anulada `FAC-0002` con su `PURCH_VOID`.
`SampleDatabaseUpgradeTests` verifica que las tres tablas estén vacías en las bases anteriores y que
`v0.14.0.db` conserve proveedores, compras, líneas, importes y los enlaces a sus movimientos. Ver
[compras.md](compras.md).

## 0.15.0: formatos del escáner (`ScannerBarcodeFormats`)

La migración `ScannerBarcodeFormats` **no genera SQL**: solo actualiza el snapshot porque
`Products.Barcode` pasa de `HasMaxLength(14)` a `HasMaxLength(48)` (CODE128/CODE39, ver
[escaner.md](escaner.md)). SQLite no guarda el largo de `TEXT`, así que no hay reconstrucción de
`Products` ni cambio de datos; el índice `IX_Products_Barcode` no cambia. Los códigos existentes (8 a 14
dígitos) cumplen la regla nueva y no cambian al normalizarse (mayúsculas, sin `*…*`).

`ScannerBarcodeFormatsMigrationTests` revisa que el script solo registre la migración en
`__EFMigrationsHistory`. La base de ejemplo `v0.15.0.db` agrega un producto activo `ALF-001` con código
`PROD-0042`; `SampleDatabaseUpgradeTests` verifica que lo conserve y que los códigos numéricos de las
bases anteriores no cambien.
