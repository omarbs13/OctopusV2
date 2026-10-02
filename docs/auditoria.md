# Bitácora de auditoría

Qué registra la bitácora, cómo se consulta y cómo se exporta (spec 007, ampliada en la 018, versión
0.13.0). Solo el Administrador la consulta y la exporta (`ViewAuditLog`).

## Qué guarda cada entrada

| Campo | Contenido |
|---|---|
| Evento (`Action`) | Código del catálogo `AuditActions`; la pantalla muestra su texto en español. |
| Entidad (`EntityType`, `EntityId`) | Tipo y registro afectados (`Product`, `Sale`, `User`…). |
| Registro (`EntityName`) | Nombre legible en ese momento: "Coca-Cola 600 ml", "Venta V-000123", "ana". |
| Motivo (`Reason`) | Lo que capturó el usuario en cancelaciones, devoluciones y apertura del cajón. |
| Cambios (`Changes`) | Lista de campo, antes y después, ya formateados (ver abajo). |
| Detalles (`Details`) | Resumen libre de hasta 500 caracteres. |
| Autor (`CreatedBy`), fecha (`CreatedAt`) | Los asigna la persistencia, nunca el caso de uso. |
| Autorizó (`AuthorizedBy`) | Administrador que autorizó la operación, si lo hubo. |

Las entradas anteriores a 0.13.0 no tienen registro, motivo ni cambios: muestran su texto en "Detalles" y
aparecen igual en los filtros por entidad, porque ya tenían `EntityType`.

### Autor "Sistema"

El autor nunca queda vacío. Sin sesión (arranque, intentos de acceso), el autor es "Sistema"
(`SystemUser.Id`). Si un caso de uso llega a guardar una entrada con el usuario actual vacío,
`AuditingInterceptor` usa "Sistema" y escribe en el log:

```
Entrada de auditoría {Action} sobre {EntityType} sin usuario actual; se registra como Sistema
```

Ese aviso indica un defecto de sesión. El guardado no se rechaza para no detener la operación (Principio I).

## Antes y después

Cada entidad auditada define una **instantánea**: la lista ordenada de sus campos legibles con el valor ya
formateado (dinero con `$`, Sí/No, Activo/Inactivo, cantidades con los decimales de la unidad, la categoría
por su nombre). `AuditChanges` compara dos instantáneas:

- **Alta**: todos los campos con valor, con "antes" vacío.
- **Modificación**: solo los campos que cambiaron. Si nada cambió, no se registra nada.
- **Eliminación**: los últimos valores, con "después" vacío.

| Instantánea | Campos |
|---|---|
| Producto | SKU, Código de barras, Nombre, Precio, Unidad de medida, Categoría, Maneja inventario, Existencia mínima, Crítico, Estado, Imagen ("Con imagen"/"Sin imagen"; un reemplazo se registra como "Imagen reemplazada") |
| Usuario | Nombre completo, Usuario, Rol, Estado. **Nunca** la contraseña ni su hash. |
| Categoría | Nombre, Descripción, Estado |
| Cliente | Nombre, Teléfono, Email, RUC, Modalidad de crédito, Límite de crédito, Estado |
| Cupón | Código, Descuento, Desde, Hasta, Límite de usos, Estado |
| Configuración | El valor que cambió: plazo de devoluciones, plazo de pago, límite de descuento o umbral de alerta de arqueo |

El costo del producto no se audita. Los valores se guardan formateados para conservar cómo se veía el dato
en ese momento, aunque el registro cambie o se borre después.

### Cancelaciones y devoluciones

No tienen instantánea. Registran un cambio por producto afectado y uno final de importe:

| Campo | Antes | Después |
|---|---|---|
| Producto Coca-Cola 600 ml | 2 × $25.00 | Cancelado, o Devuelto: 1 |
| Importe | $50.00 | $0.00 (o lo que queda de la venta) |

El motivo va en "Motivo" y el folio en "Registro" ("Venta V-000123"). "Detalles" solo resume el folio de
la devolución, el monto y la compensación.

### Ventas con descuento

Una sola entrada `SALE_DISCOUNTS_APPLIED` por venta con al menos un descuento, autorizado o no, con un
cambio por descuento: "Producto {nombre}" o "Total de la venta", el importe sin descuento y el importe con
descuento seguido de la descripción, el cupón y, si lo hubo, "Autorizado por un Administrador". Si hubo
autorización, `AuthorizedBy` es el primer autorizador. `DISCOUNT_APPLIED_AUTHORIZED` ya no se emite; se
conserva en el catálogo para mostrar las entradas anteriores.

## Eventos agregados en 0.13.0

| Código | Texto | Quién lo registra |
|---|---|---|
| `PRODUCT_CREATED` | Producto creado | `CreateProduct` |
| `PRODUCT_UPDATED` | Producto modificado | `UpdateProduct` (incluida la desactivación) y `SetProductCritical` (Reportes), con el único cambio "Crítico" |
| `PRODUCT_DELETED` | Producto eliminado | `DeleteProduct` |
| `SALE_DISCOUNTS_APPLIED` | Venta con descuento | `ConfirmSale` |
| `AUDIT_EXPORTED` | Bitácora exportada | `ConfirmAuditExport` |

Además, los eventos de usuarios, categorías, clientes, cupones y configuración ya existentes ahora llevan
sus cambios de campo. En usuarios y clientes, los datos van en el evento de modificación y el estado (o el
crédito) en su propio evento (`USER_DEACTIVATED`, `CUSTOMER_CREDIT_CHANGED`…).

## Consulta

**Administración → Bitácora**. Filtros: rango de fechas (inicia en hoy), usuario involucrado (autor,
autorizador o afectado, incluidos los inactivos y "Sistema"), evento y entidad. La tabla muestra fecha,
evento, entidad, registro, usuario, quién autorizó y un resumen ("Precio, Categoría (+1)"). El panel de
detalle muestra el motivo, la tabla Campo | Antes | Después (los vacíos como "—") y los detalles completos.

### Grupos de entidad

| Grupo | Entradas |
|---|---|
| Producto | `Product` |
| Venta | `Sale`, `SaleReturn`, `CreditNote`, `DiscountApproval` |
| Usuario | `User`, sin los eventos de sesión |
| Sesión | `LOGIN_SUCCEEDED`, `LOGIN_FAILED`, `USER_LOCKED_OUT`, `LOGOUT` |
| Categoría | `Category` |
| Cliente | `Customer`, `CustomerPayment` |
| Cupón | `Coupon` |
| Caja/Turno | `CashShift`, `ShiftCut`, `CashDrawer` |
| Configuración | `ReturnSettings`, `ReceivablesSettings`, `DiscountSettings`, `License` y el evento `REPORT_SETTINGS_CHANGED` |
| Reportes y exportaciones | `Report` (sin `REPORT_SETTINGS_CHANGED`) y `AuditLog` |

### Historial de un registro

"Ver historial del registro" filtra por la entidad y el id de la fila seleccionada, quita las fechas y
ordena de la entrada más antigua a la más reciente. El chip "Historial de: {registro}" lo indica; su ✕ lo
quita. El botón se desactiva en las entradas sin registro (intentos de acceso con un usuario inexistente).

### Rendimiento

La primera página de cualquier búsqueda responde en menos de 1 s con 1,000,000 de entradas. Los índices
siguen el patrón "igualdad + fecha" y la consulta pagina antes de unir los nombres de usuario.
`AuditLogPerformanceTests` es una prueba explícita que se ejecuta antes de publicar:

```bash
dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "*AuditLogPerformanceTests" --explicit on
```

## Exportación

Botones "Exportar PDF" y "Exportar Excel". Exportan **todas** las entradas del filtro, no solo la página.
El rango de fechas es obligatorio.

- **PDF**: una fila por entrada; los cambios van en varias líneas como "Campo: antes → después".
- **Excel**: una fila por cambio de campo (una por entrada si no tiene cambios), con fecha y hora como
  valor de fecha. Sin hoja "Gráficas".
- Nombre sugerido: `bitacora_AAAAMMDD-AAAAMMDD.pdf` o `.xlsx`.

El registro se hace en dos pasos para que una exportación fallida no quede como exitosa:

1. `ExportAuditLog` genera el archivo fuera del hilo de la interfaz y lo devuelve sin registrar nada.
2. Si el archivo se guardó, la interfaz llama a `ConfirmAuditExport`, que registra `AUDIT_EXPORTED` con
   formato, rango, filtros y número de entradas.

Si se cancela el diálogo de guardar, no pasa nada. Si falla la escritura, aparece "No se pudo guardar el
archivo: {motivo}." y no se registra nada. El log registra cada exportación:

```
Bitácora exportada. Formato={Format} Entradas={Entries} DuracionMs={ElapsedMs}
Exportación de la bitácora rechazada. Formato={Format} Motivo={Reason}
```

## Inmutabilidad

- La entrada viaja en la misma transacción que el cambio: si el guardado falla (por ejemplo, por un
  conflicto de versión), no queda entrada.
- `PosDbContext.RejectImmutableChanges` rechaza modificar o borrar entradas y sus cambios.
- `AuditImmutabilityTests` (arquitectura) falla si un archivo de `Pos.Infrastructure` usa `ExecuteUpdate`
  o `ExecuteDelete` y menciona la bitácora, porque esas llamadas saltan el `ChangeTracker`.
- La pantalla no tiene ninguna forma de editar ni de borrar.

Quedan fuera de alcance los triggers de base de datos y la cadena de hashes: protegen contra alguien con
acceso directo al archivo, que no es el riesgo que cubre esta funcionalidad.
