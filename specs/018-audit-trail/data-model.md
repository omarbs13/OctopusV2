# Data Model: Auditoría detallada de cambios

Cambios al modelo para [spec.md](spec.md). Decisiones en [research.md](research.md).

## AuditEntry (existente, spec 007; se amplía)

Tabla `AuditEntries`. Entidad inmutable: no tiene `UpdatedAt`, `Version` ni `DeletedAt`, y está
justificada en el Complexity Tracking de la spec 007.

| Campo | Tipo | Nuevo | Reglas |
|---|---|---|---|
| `Id` | GUID v7 | | Lo genera `AuditEntry.Create`. |
| `Action` | texto ≤ 40 | | Código del catálogo `AuditActions`. Obligatorio. |
| `EntityType` | texto ≤ 40 | | Código de la entidad (`Product`, `Sale`, `User`…). Obligatorio. |
| `EntityId` | GUID | | Registro afectado. `Guid.Empty` en los intentos de acceso con un usuario inexistente. |
| `EntityName` | texto ≤ 200, nulo | ✅ | Nombre legible del registro en ese momento ("Coca-Cola 600 ml", "Venta V-000123", "ana"). Nulo en las entradas anteriores. Se recorta a 200 caracteres. |
| `Details` | texto ≤ 500, nulo | | Resumen libre. Se conserva para las entradas anteriores y para los eventos sin cambios de campo. |
| `Reason` | texto ≤ 250, nulo | ✅ | Motivo capturado por el usuario (cancelación, devolución, cajón). |
| `Changes` | JSON, nulo | ✅ | Lista ordenada de `AuditFieldChange`. Nula o vacía en los eventos sin cambios de campo y en las entradas anteriores. |
| `CreatedAt` | fecha UTC | | Lo asigna `AuditingInterceptor`. Obligatorio. |
| `CreatedBy` | GUID | | Autor. Lo asigna `AuditingInterceptor` desde `ICurrentUser`; sin sesión es `SystemUser.Id`. Obligatorio y nunca `Guid.Empty` (FR-010): si el usuario actual es `Guid.Empty`, el interceptor usa `SystemUser.Id` y escribe una advertencia en el log. |
| `AuthorizedBy` | GUID, nulo | | Administrador que autorizó, si lo hubo. |

**Validaciones** en `AuditEntry.Create`:

- `Action` y `EntityType` son obligatorios y respetan su longitud. Si no, lanza `DomainException`,
  como hoy.
- `EntityName` y `Reason` se recortan a su longitud máxima. Los valores vacíos se guardan como
  nulos. Los límites coinciden con los de captura (nombre de producto 200, motivos 250 o menos),
  así que en la práctica no se recortan.
- `Create` no recibe el autor: `CreatedAt` y `CreatedBy` los asigna la persistencia.
- Cada cambio de `Changes` es válido (ver abajo). La lista se guarda en el orden recibido.

**Inmutabilidad**:

- `PosDbContext.RejectImmutableChanges` rechaza `Modified` y `Deleted`.
- Una prueba de arquitectura prohíbe `ExecuteUpdate` y `ExecuteDelete` sobre `AuditEntries`
  (research §11).

## AuditFieldChange (nuevo, value object en Domain)

Se guarda dentro de `AuditEntries.Changes` (`OwnsMany(...).ToJson("Changes")`).

| Campo | Tipo | Reglas |
|---|---|---|
| `Field` | texto ≤ 80 | Nombre legible en español ("Precio", "Categoría", "Rol"). Obligatorio. |
| `Before` | texto ≤ 2000, nulo | Valor anterior ya formateado. Nulo en las altas. |
| `After` | texto ≤ 2000, nulo | Valor nuevo ya formateado. Nulo en las eliminaciones. |

- Si `Before` y `After` son iguales, el cambio no es válido: el comparador nunca lo produce.
- Un valor de más de 2000 caracteres se recorta con "…". En la práctica, ningún campo auditado
  llega a ese tamaño.

## Usuario "Sistema" (existente)

`SystemUser.Id` (`00000000-0000-7000-8000-000000000001`), nombre "Sistema", sin sesión posible. Es
el autor de los eventos sin una persona identificada. No cambia.

## Instantáneas de auditoría (Application, sin persistencia)

Cada entidad auditada define qué campos se registran y cómo se formatean. Cada instantánea es una
lista ordenada de pares `(Field, Value)`.

| Instantánea | Campos |
|---|---|
| `ProductAuditFields` | SKU, Código de barras, Nombre, Precio, Unidad de medida, Categoría (nombre o "Sin categoría"), Maneja inventario (Sí/No), Existencia mínima (con los decimales de la unidad), Crítico (Sí/No), Estado (Activo/Inactivo), Imagen ("Sin imagen" o "Con imagen") |
| `UserAuditFields` | Nombre completo, Usuario, Rol, Estado. **Nunca** contraseña ni hash. |
| `CategoryAuditFields` | Nombre, Estado |
| `CustomerAuditFields` | Los campos editables del formulario de cliente, incluidos el límite y la modalidad de crédito |
| `CouponAuditFields` | Los campos editables del formulario de cupón |
| Configuración | Cada pantalla de configuración auditada (devoluciones, crédito, descuentos, reportes) define sus campos |

**Imagen reemplazada**: la instantánea no la distingue, porque antes y después valen "Con
imagen". `UpdateProduct` agrega a mano el cambio ("Imagen", "Con imagen", "Imagen reemplazada")
cuando recibe `ProductImageChange.Replace` y el producto ya tenía imagen (research §5).

**Cancelaciones y devoluciones**: no tienen instantánea. Registran un cambio por producto
afectado y uno de "Importe" (research §8).

`AuditChanges` compara dos instantáneas:

- `Compare(before, after)`: solo los campos con valor distinto, en el orden de la instantánea.
- `Created(after)`: todos los campos, con el valor anterior nulo.
- `Removed(before)`: todos los campos, con el valor nuevo nulo.

## Catálogo de eventos (`AuditActions`): agregados

| Código | Texto | Entidad |
|---|---|---|
| `PRODUCT_CREATED` | Producto creado | `Product` |
| `PRODUCT_UPDATED` | Producto modificado | `Product` |
| `PRODUCT_DELETED` | Producto eliminado | `Product` |
| `SALE_DISCOUNTS_APPLIED` | Venta con descuento | `Sale` |
| `AUDIT_EXPORTED` | Bitácora exportada | `AuditLog` |

- `DISCOUNT_APPLIED_AUTHORIZED` deja de emitirse, pero se conserva en el catálogo para las entradas
  anteriores.
- Se agregan las constantes de entidad `ProductEntity = "Product"` y `AuditLogEntity = "AuditLog"`.

## Grupos de entidad del filtro (`AuditEntityGroup`)

Producto, Venta, Usuario, Sesión, Categoría, Cliente, Cupón, Caja/Turno, Configuración, y Reportes
y exportaciones. La correspondencia con `EntityType` y `Action` está en research §9.
`REPORT_SETTINGS_CHANGED` pertenece a Configuración aunque su `EntityType` es `Report`.

## Índices

| Índice | Columnas | Estado |
|---|---|---|
| `IX_AuditEntries_CreatedAt` | `CreatedAt` | existe |
| `IX_AuditEntries_Entity` | `EntityType, EntityId` | existe |
| `IX_AuditEntries_CreatedBy` | `CreatedBy` → `CreatedBy, CreatedAt` | se reemplaza |
| `IX_AuditEntries_AuthorizedBy` | `AuthorizedBy` | nuevo |
| `IX_AuditEntries_Action_CreatedAt` | `Action, CreatedAt` | nuevo |
| `IX_AuditEntries_EntityType_CreatedAt` | `EntityType, CreatedAt` | nuevo |

## Migración `AuditTrail` (versión 0.12.0 → 0.13.0)

- `ALTER TABLE AuditEntries ADD COLUMN EntityName TEXT NULL`, `Reason TEXT NULL` y `Changes TEXT NULL`.
- Borra y vuelve a crear `IX_AuditEntries_CreatedBy`, y crea los tres índices nuevos.
- **No reconstruye ninguna tabla**: se verifica en el SQL generado (docs/migraciones.md).
- No transforma datos. Las entradas anteriores quedan con las columnas nuevas en nulo.
- Base de ejemplo `v0.13.0.db` según docs/migraciones.md.
