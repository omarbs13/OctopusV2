# Research: Auditoría detallada de cambios

Decisiones de diseño para la spec [018-audit-trail](spec.md). Todas parten de la bitácora existente
(spec 007): `AuditEntry` en Domain, `IAuditLog`/`IAuditLogReader` en Application, `AuditLog`/
`AuditLogReader` en Infrastructure y `AuditLogViewModel` en Desktop.

## §1 Dónde guardar el antes y después

- **Decisión**: agregar a `AuditEntries` una columna JSON `Changes` con la lista de cambios de campo
  (`OwnsMany(...).ToJson()` de EF Core sobre SQLite). Cada cambio tiene el nombre legible del
  campo, el valor anterior y el valor nuevo, ya formateados como texto.
- **Por qué**:
  - La entrada y sus cambios se escriben en una sola fila. Así se mantienen la atomicidad y la
    inmutabilidad existentes (`RejectImmutableChanges`) sin agregar reglas para otra tabla.
  - Ninguna búsqueda filtra por el valor de un campo. Las búsquedas filtran por evento, entidad,
    registro, usuario y fecha, y todo eso ya son columnas.
  - Guardar el texto formateado (por ejemplo, "$28.50" o el nombre de la categoría) congela cómo
    se veía el valor en ese momento (FR-005). No depende de que el registro siga existiendo.
- **Alternativas descartadas**:
  - Tabla hija `AuditFieldChanges`: agrega un join a cada página y otra tabla que proteger, sin
    ningún beneficio de consulta.
  - Seguir usando el texto libre `Details` (500 caracteres): no es estructurado y no sirve para
    la exportación a Excel de una fila por campo (FR-024).

## §2 Cómo se calcula el antes y después

- **Decisión**: cada entidad auditada tiene una **instantánea de auditoría** en Application. Es una
  función pura que convierte el registro en una lista ordenada de pares (campo, valor formateado),
  por ejemplo `ProductAuditFields.Snapshot(product, categoryName, unit)`.
  - Un comparador común, `AuditChanges.Compare(before, after)`, produce solo los campos distintos.
  - `AuditChanges.Created(after)` produce todos los campos con el valor anterior vacío.
  - `AuditChanges.Removed(before)` produce todos los campos con el valor nuevo vacío.
  - El caso de uso toma la instantánea antes de modificar la entidad y otra después, y llama a
    `IAuditLog.Add` antes de guardar. Si la lista resulta vacía, no agrega la modificación (FR-002).
- **Por qué**:
  - Es explícito y se puede probar sin base de datos.
  - Usa los textos en español y el formato de la interfaz (dinero, unidades, Sí/No).
  - No registra campos técnicos (`Version`, `UpdatedAt`, `NameSearch`).
- **Alternativas descartadas**:
  - Un interceptor de EF que compare `OriginalValue` y `CurrentValue`: produce valores crudos
    (centavos, GUID de la categoría) en lugar de valores legibles. Tampoco sabe el motivo ni quién
    autorizó, y metería en Infrastructure la decisión de qué campos se auditan y cómo se llaman.
  - Una librería de auditoría externa: viola el Principio VII y tiene el mismo problema de
    valores crudos.

## §3 Ampliación de `IAuditLog` sin romper a los llamadores existentes

- **Decisión**:
  - Se agrega la sobrecarga `Add(AuditRecord record)`, donde `AuditRecord` lleva los campos de
    hoy más `EntityName`, `Reason` y `Changes`.
  - La firma actual `Add(action, entityType, entityId, details, authorizedBy)` se conserva y crea
    un `AuditRecord` sin cambios.
  - `AuditEntry.Create` recibe los campos nuevos.
- **Por qué**: hay más de 40 llamadores. Solo se migran los que la spec obliga a tener antes y
  después o motivo (§5–§8). Los demás siguen igual.

## §4 Autor obligatorio

- **Decisión**:
  - `CreatedBy` ya es `NOT NULL` y el `AuditingInterceptor` lo llena con
    `ICurrentUser.UserId`, que sin sesión devuelve `SystemUser.Id` ("Sistema").
  - `AuditEntry.Create` no recibe el autor: lo asigna la persistencia. Por eso la garantía va en
    el `AuditingInterceptor`: si al agregar una `AuditEntry` el usuario actual es `Guid.Empty`,
    usa `SystemUser.Id` y escribe una advertencia en Serilog.
  - No se rechaza el guardado, porque eso detendría la operación (por ejemplo, una venta) por un
    defecto de sesión (Principio I).
  - Los intentos de acceso con un nombre de usuario que no existe ya guardan el nombre capturado
    en `Details` y quedan con autor "Sistema".
- **Por qué**: FR-010 ya se cumple en la práctica. Solo falta dejarlo garantizado y probado
  (`AuditAuthorTests`, sobre SQLite real).

## §5 Productos

- **Decisión**:
  - Tres eventos nuevos: `PRODUCT_CREATED`, `PRODUCT_UPDATED` y `PRODUCT_DELETED`, con la entidad
    `Product`.
  - Los registran `CreateProduct`, `UpdateProduct` y `DeleteProduct` en la misma operación de
    guardado.
  - La desactivación (`IsActive`) es un cambio de campo dentro de `PRODUCT_UPDATED`.
  - Campos auditados (FR-003): SKU, código de barras, nombre, precio, unidad de medida, categoría
    (por nombre), maneja inventario, existencia mínima, crítico, estado e imagen.
  - La imagen se representa en la instantánea como "Sin imagen" o "Con imagen", sin su
    contenido.
  - **Imagen reemplazada**: la instantánea no la puede detectar, porque antes y después valen
    "Con imagen". `UpdateProduct` agrega a mano el cambio ("Imagen", "Con imagen", "Imagen
    reemplazada") cuando recibe `ProductImageChange.Replace` y el producto ya tenía imagen.
- **Otros casos de uso que modifican productos**: `SetProductCritical` (reportes, spec 009)
  cambia el campo "Crítico". Registra `PRODUCT_UPDATED` con ese único cambio en la misma
  operación de guardado. Ningún otro caso de uso modifica campos auditados del producto. Los
  movimientos de inventario no se auditan aquí (ver Supuestos de la spec).
- **Cambio de precio, costo o categoría**: no hay un evento aparte. Son cambios de campo de
  `PRODUCT_UPDATED`, y el historial del registro (§9) los muestra juntos. El costo queda fuera
  (FR-003a).
- **Borrado de una categoría**: `DeleteCategory` rechaza el borrado si la categoría tiene
  productos (`CategoryInUse`). `ClearFromDeletedProductsAsync` solo quita la referencia de los
  productos **ya eliminados**, que tienen su `PRODUCT_DELETED` con el nombre de la categoría de
  ese momento.
  - Por eso no hay productos activos que cambien de categoría y no se agrega ninguna entrada
    por producto.
  - `CATEGORY_DELETED` pasa a registrar los últimos valores de la categoría
    (`Removed(snapshot)`, FR-004).

## §6 Ediciones que ya se registran (FR-006)

- **Decisión**: los casos de uso que hoy registran una edición con texto libre pasan a registrar
  sus cambios de campo con su instantánea:
  - `UpdateUser`: nombre completo, usuario, rol y estado.
  - `UpdateCategory`, `SetCategoryActive`.
  - `UpdateCustomer`: incluido el crédito.
  - `UpdateCoupon`, `SetCouponActive`.
  - Configuración: plazo de devoluciones, plazo de crédito, límite de descuento y configuración
    de reportes.
  - Los altas registran sus valores iniciales y las desactivaciones, los últimos valores (FR-004).
- **Contraseñas**: `ResetUserPassword` y `ChangeOwnPassword` siguen registrando solo el evento.
  Ninguna instantánea incluye la contraseña ni su hash. Una prueba de dominio lo verifica sobre
  `UserAuditFields`.

## §7 Descuentos aplicados (FR-008)

- **Decisión**:
  - `ConfirmSale` deja de emitir `DISCOUNT_APPLIED_AUTHORIZED` por cada descuento autorizado.
  - En su lugar emite **una** entrada `SALE_DISCOUNTS_APPLIED` ("Venta con descuento") por cada
    venta con al menos un descuento, autorizado o no.
  - Entidad `Sale`, `EntityName` = "Venta {folio}".
  - Hay un cambio de campo por descuento:
    - **Campo**: "Producto {nombre}" o "Total de la venta".
    - **Valor anterior**: el importe antes del descuento.
    - **Valor nuevo**: el importe con descuento, seguido de la descripción
      (`DiscountTexts.Describe`), el cupón y quién autorizó.
  - `AuthorizedBy` = el autorizador cuando todos los descuentos autorizados los autorizó la misma
    persona; si fueron varias, el primero. Los demás aparecen en el texto.
  - `DISCOUNT_AUTHORIZED` (el momento de la autorización, spec 015) no cambia.
  - `DISCOUNT_APPLIED_AUTHORIZED` se queda en el catálogo para mostrar las entradas anteriores.
  - **Pruebas existentes afectadas**: `DiscountTestBase.AppliedAuthorizedCountAsync` cuenta
    `DISCOUNT_APPLIED_AUTHORIZED`, y `ConfirmSaleApprovalTests` lo usa. Pasa a contar las
    entradas `SALE_DISCOUNTS_APPLIED` con `AuthorizedBy` no nulo, de modo que las aserciones
    conservan su significado.
- **Por qué**: la spec pide una entrada por venta, y así la exportación a Excel da una fila por
  descuento.
- **Riesgo aceptado**: si dos Administradores distintos autorizan descuentos en la misma venta,
  el filtro por usuario solo encuentra al primero por columna. Con una sola caja, es un caso raro.

## §8 Motivo y nombre legible del registro

- **Decisión**:
  - Columnas nuevas `Reason` (hasta 250 caracteres, como `SaleReturn.ReasonMaxLength`) y
    `EntityName` (hasta 200 caracteres).
  - Cancelaciones y devoluciones guardan su motivo en `Reason` y "Venta {folio}" en `EntityName`.
  - Los productos y el importe afectados (FR-007) van en `Changes`, no en `Details` (500
    caracteres), para que no se recorten en una venta con muchos productos:
    - Un cambio por producto. **Campo**: "Producto {nombre}". **Antes**: "{cantidad} ×
      {precio}". **Después**: "Cancelado" o "Devuelto: {cantidad}".
    - Un último cambio "Importe" con el importe afectado, antes y después de la operación.
    - `Details` conserva solo el resumen (forma de pago y reembolso).
  - Los límites de `EntityName` (200) y `Reason` (250) coinciden con los de captura (nombre de
    producto 200, motivos 250 o menos), así que en la práctica no se recortan (caso límite
    "Valores largos").
  - La apertura del cajón guarda su motivo en `Reason`.
  - Las entradas anteriores tienen ambas columnas nulas. Su motivo sigue visible en `Details`
    (FR-021).

## §9 Filtro por entidad e historial del registro

- **Decisión**:
  - `AuditEntityGroup`, en Application, define los grupos del filtro (FR-017). Cada grupo
    corresponde a un conjunto de `EntityType` y, en el caso de Sesión, a un conjunto de eventos:

    | Grupo | Criterio |
    |---|---|
    | Producto | `Product` |
    | Venta | `Sale`, `SaleReturn`, `CreditNote`, `DiscountApproval` |
    | Usuario | `User`, excepto los eventos de sesión |
    | Sesión | eventos `LOGIN_*`, `USER_LOCKED_OUT`, `LOGOUT` |
    | Categoría | `Category` |
    | Cliente | `Customer`, `CustomerPayment` |
    | Cupón | `Coupon` |
    | Caja/Turno | `CashShift`, `ShiftCut`, `CashDrawer` |
    | Configuración | `ReturnSettings`, `ReceivablesSettings`, `DiscountSettings`, `License`, y el evento `REPORT_SETTINGS_CHANGED` |
    | Reportes y exportaciones | `Report` excepto el evento `REPORT_SETTINGS_CHANGED`, y `AuditLog` |

  - `REPORT_SETTINGS_CHANGED` se registra con `EntityType = Report`. Por eso esos dos grupos se
    definen también por evento, y no solo por `EntityType`.

  - Como las entradas anteriores ya tienen `EntityType`, el filtro las incluye sin migrar datos
    (FR-021).
  - El historial de un registro es el filtro `(EntityType, EntityId)`, que ya tiene índice
    (`IX_AuditEntries_Entity`). Se ordena cronológicamente de la más antigua a la más reciente
    (Historia 2, escenario 5).

## §10 Rendimiento: menos de 1 segundo con 1,000,000 de entradas (SC-002)

- **Decisión**: índices compuestos que siguen el patrón "filtro de igualdad + orden por fecha", y
  conteo sobre los mismos índices:
  - `IX_AuditEntries_CreatedAt` (existe).
  - `IX_AuditEntries_CreatedBy` se reemplaza por `(CreatedBy, CreatedAt)`.
  - Nuevos: `(AuthorizedBy)`, `(Action, CreatedAt)` y `(EntityType, CreatedAt)`.
  - `IX_AuditEntries_Entity (EntityType, EntityId)` se conserva para el historial y el usuario
    afectado.
  - El filtro de usuario involucrado (autor, autorizador o afectado) es un `OR` sobre tres
    columnas indexadas. SQLite lo resuelve con su optimización de `OR` por índices.
  - La columna `Changes` se lee solo para las 100 filas de la página.
- **Verificación**: una prueba de rendimiento explícita (`Explicit = true` de xUnit v3, no corre en
  la suite normal):
  - Siembra 1,000,000 de entradas sobre SQLite en archivo.
  - Mide la primera página con cada combinación de filtros.
  - Exige menos de 1 s. La meta interna es 300 ms.
  - Se ejecuta antes de publicar (quickstart §6).
- **Alternativas descartadas**:
  - FTS5 sobre `Details`: la spec no pide búsqueda por texto.
  - Paginación por cursor: el conteo total y la navegación por número de página ya existen y son
    rápidos con índices.

## §11 Inmutabilidad (FR-013, FR-014)

- **Decisión**: se mantiene lo existente.
  - `PosDbContext.RejectImmutableChanges` rechaza `Modified` y `Deleted` sobre `AuditEntry`.
  - La interfaz no ofrece ninguna forma de editar ni de borrar.
- **Agregado**: una prueba de arquitectura verifica que ningún código de Infrastructure use
  `ExecuteUpdate` ni `ExecuteDelete` sobre `AuditEntries`, porque esas llamadas saltan el
  `ChangeTracker`.
  - NetArchTest solo ve dependencias entre tipos, no qué `DbSet` recibe una llamada. Por eso la
    prueba **revisa el código fuente**: recorre los `.cs` de `src/Pos.Infrastructure` (sin
    `Migrations/`), localizando la raíz por `Pos.slnx`.
  - Falla si un archivo menciona `AuditEntries` o `AuditEntry` y también `ExecuteUpdate` o
    `ExecuteDelete`.
  - Es una regla conservadora: hoy ningún archivo cumple las dos condiciones.
- **Fuera de alcance** (aclaración P2): triggers de base de datos y cadena de hashes.
- **Migración**: solo `ALTER TABLE ADD COLUMN` nulos e índices. No reconstruye `AuditEntries`, así
  que no hay riesgo de perder o reescribir entradas.

## §12 Exportación (Historia 3)

- **Decisión**:
  - Nuevo caso de uso `ExportAuditLog` con permiso `ViewAuditLog`. Reutiliza `ReportDocument`,
    `IPdfReportWriter` e `IXlsxReportWriter` de la spec 009.
  - **PDF**: una fila por entrada con las columnas fecha, evento, entidad, registro, usuario,
    autorizó, motivo y cambios. Los cambios van en varias líneas con el formato
    "Campo: antes → después".
  - **XLSX**: una fila por cambio de campo (FR-024), con fecha y hora como valor de fecha.
  - El rango de fechas es obligatorio. Los filtros son los mismos de la pantalla. Se exporta
    todo lo filtrado, no solo la página.
  - El escritor XLSX omite la hoja "Gráficas" cuando el documento no tiene gráficas. Es un ajuste
    menor, compatible con los reportes existentes.
  - La exportación se registra como `AUDIT_EXPORTED` ("Bitácora exportada"), con entidad
    `AuditLog`, formato, rango, filtros y número de entradas (FR-025).
  - El registro se hace en dos pasos, porque la interfaz es la que escribe el archivo en disco:
    1. `ExportAuditLog` genera el archivo y lo devuelve sin registrar nada.
    2. Después de guardar el archivo bien, la interfaz llama a `ConfirmAuditExport`, que agrega
       la entrada.
  - Si la escritura en disco falla, se muestra el error y no queda entrada (Historia 3,
    escenario 4).
- **Diferencia con `ExportReportHandler`**: ese caso de uso registra al generar el archivo. Aquí se
  separa porque la spec exige que una exportación fallida no quede como exitosa.
- **Alternativa descartada**: pasar la ruta del archivo al caso de uso para que lo escriba él.
  Haría falta un puerto de sistema de archivos nuevo y no seguiría el patrón de los reportes.
- **Volumen**: 10,000 entradas en menos de 30 s (SC-006).
  - La lectura es una sola consulta ordenada.
  - Los escritores ya manejan tablas grandes (reporte de inventario completo).
  - No se fija un tope de filas, como pide la spec.

## §13 Pantalla

- **Decisión**: se amplía `AuditLogView`; no se crea otra pantalla.
  - **Filtros**: se agrega el filtro "Entidad". El filtro de fechas inicia en hoy (FR-018).
  - **Columna "Registro"**: muestra `EntityName`, o los detalles si se trata de una entrada
    anterior.
  - **Panel de detalle de la fila seleccionada**:
    - Tabla de cambios (campo, antes, después).
    - Motivo, autorizador y texto completo de los detalles.
    - Botón "Ver historial del registro".
  - **Botones**: "Exportar PDF" y "Exportar Excel".
  - Cuando se activa el historial de un registro, un chip "Historial de: {registro}" muestra el
    filtro y permite quitarlo.

## §14 Pruebas (constitución v1.2.0, política mínima)

- **Domain.Tests**:
  - `AuditFieldChangeTests`: un cambio con antes y después iguales se rechaza.
- **Application.Tests**:
  - `AuditChangesTests`: solo los campos distintos, alta con todos los campos y sin cambios →
    vacío.
  - `UserAuditFieldsTests`: la instantánea nunca contiene contraseña ni hash.
- **Infrastructure.Tests**, sobre SQLite real:
  - `ProductAuditTests`:
    - La edición de precio y categoría crea una entrada con sus dos cambios.
    - Un conflicto de versión no deja entrada.
    - Marcar un producto como crítico registra un `PRODUCT_UPDATED` con el cambio "Crítico".
  - `AuditAuthorTests`: una entrada guardada con un usuario actual `Guid.Empty` queda con autor
    `SystemUser.Id` (FR-010).
  - `SaleDiscountAuditTests`: una venta con descuento sin autorizar crea `SALE_DISCOUNTS_APPLIED`
    (SC-007).
  - `AuditLogReaderTests` (se amplía): filtro por entidad, historial del registro, usuario
    involucrado como autorizador y entradas anteriores sin `Changes`.
  - `ExportAuditLogTests`: más de 100 entradas se exportan completas y la exportación queda en la
    bitácora.
  - `AuditLogPerformanceTests` (explícita): SC-002.
- **Migración**: `AuditTrailMigrationTests` y la base de ejemplo `v0.13.0.db`.
- **Arquitectura**: la regla nueva de §11 más las existentes.
- **No se prueban**: ViewModels, vistas, mapeos ni formato del PDF.
