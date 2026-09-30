# Data Model: Impresión de ticket, cajón de dinero y datos del negocio

## BusinessProfile (SQLite, `Pos.Domain/Business`)

Datos del negocio que aparecen en el ticket. Una fila por instalación.

| Campo | Tipo | Regla |
|---|---|---|
| Id | GUID v7 | Generado en la aplicación |
| TradeName | texto, máx. 80 | Obligatorio, sin espacios sobrantes |
| Address | texto, máx. 200 | Obligatorio |
| Phone | texto, máx. 30 | Obligatorio |
| TaxId (RFC) | texto, máx. 13, opcional | Sin espacios; en mayúsculas |
| Logo | BLOB opcional | PNG normalizado por `IImageProcessor`, lado máximo 576 px |
| FooterMessage | texto, máx. 200, opcional | Puede tener varias líneas |
| CreatedAt / CreatedBy / UpdatedAt / UpdatedBy / DeletedAt / Version | estándar | Los asigna el interceptor de auditoría (Principio IV) |

- **Una sola fila**: `SaveBusinessProfile` crea la fila la primera vez y la actualiza después;
  no hay borrado. Un índice único parcial no es necesario, porque el handler es la única vía de
  escritura y se ejecuta en una transacción de escritura.
- **Sin `HasData`**: son datos propios de la instalación (Principio IV). Una base sin fila se
  trata como "sin datos": el ticket usa los campos vacíos y la pantalla los pide.
- **Concurrencia**: `Version` como token de concurrencia, como el resto de las entidades.
- **Migración**: `BusinessProfile` solo crea la tabla; sin reconstrucción de tablas existentes.

## PrintingSettings (JSON local, `preferences/printing.json`)

Por máquina; no se migra ni entra en la base.

| Campo | Tipo | Predeterminado |
|---|---|---|
| PrinterName | texto o nulo | nulo (sin configurar) |
| UseVirtualPrinter | bool | false |
| PaperWidth | `Mm58` \| `Mm80` | `Mm80` |
| AutoPrint | bool | false |
| AutoOpenDrawer | bool | true |

Validación: `AutoPrint` sin impresora ni impresora virtual no es un error al guardar; produce el
aviso de "impresora sin configurar" al cobrar (spec Edge Cases).

## Ticket (no persistido)

Se genera a partir de `SaleDetailDto` o de los datos de ejemplo y del `BusinessProfile` vigente.

- `TicketDocument`: `Lines` (texto, alineación, negrita), `Logo` opcional y `Columns` (32 o 48).
- `TicketOptions`: `IsReprint`. "CANCELADA" se deduce de `SaleStatus`.
- Formato en [contracts/ticket-format.md](contracts/ticket-format.md).

## Apertura de cajón sin venta (bitácora existente `AuditEntries`)

Sin tabla nueva. `AuditEntry.Create(action, entityType, entityId, details)`:

| Campo | Valor |
|---|---|
| Action | `DRAWER_OPENED` |
| EntityType | `CashDrawer` |
| EntityId | GUID v7 nuevo por intento |
| Details | `Motivo: <texto>; Resultado: OK` o `Resultado: FALLO` (máx. 500) |
| CreatedAt / CreatedBy | Asignados por el interceptor (fecha UTC y usuario) |

## Relaciones y estados

- `BusinessProfile` no se relaciona por llave con `Sale`; el ticket lee el perfil vigente al
  imprimir, incluso al reimprimir (spec US1 #5).
- No hay transiciones de estado nuevas. Los estados de venta (`Completed`, `Cancelled`) de 005
  solo cambian la leyenda del ticket.
