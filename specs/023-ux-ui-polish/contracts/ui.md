# Contrato de UI: Mejoras de UX/UI y comportamiento

**Feature**: 023-ux-ui-polish | **Fecha**: 2026-10-02

Este documento describe lo que el operador ve y el comportamiento que no puede cambiar sin
actualizar la spec. Las decisiones de implementación están en [research.md](../research.md).

## Ventana principal (US1, US2)

| Aspecto | Contrato |
|---|---|
| Primer arranque | Maximizada (FR-001). |
| Arranques siguientes | Último estado no minimizado; en estado normal, el último tamaño y la última posición (FR-002, FR-003). |
| Posición fuera de pantalla | Centrada en la pantalla principal (FR-004). |
| Mínimo | 1024×768 de área cliente; en pantallas menores, el área de trabajo disponible (FR-005, FR-006). |
| Desplazamiento | Si el área cliente es menor que 1024×768, aparecen barras de desplazamiento globales; nunca con la ventana en el mínimo o mayor. |
| Preferencia | `<datos>/preferences/window.json`, por equipo. Si está dañada → maximizada, con aviso en el log y sin mensaje al operador (FR-032). |

## Pantalla de carga (US3)

| Arranque | Tiempo visible |
|---|---|
| Termina en menos de 2 s | 2,0 a 2,3 s (SC-004) |
| Termina en 2 s o más | Lo que dure el arranque, más un máximo de 0,3 s |
| Falla | El diálogo de error aparece sin esperar a los 2 s (FR-010) |

## Menú lateral (US4, US5, US6)

### Estado de los grupos

- Usuario sin estado guardado: todos los grupos colapsados y el menú expandido (FR-011).
- Expandir o colapsar un grupo se guarda al momento, por usuario
  (`preferences/navigation.{userId}.json`, FR-013).
- Un grupo que no está en el estado guardado, por ejemplo uno nuevo, aparece colapsado.
- Contraer el menú a solo iconos y volver a expandirlo no altera los grupos (escenario 4 de US4).
- La opción actual dentro de un grupo colapsado no lo abre; el grupo conserva su marca lateral
  (`currentGroup`).

### Botón hamburguesa

- Es el primer elemento del menú y está alineado a la izquierda, en x = 16 px del borde del menú.
- Tiene la misma posición con el menú expandido (240 px) y contraído (56 px) (FR-014).
- Los iconos de primer nivel comparten esa columna en ambos modos.
- Tooltip "Mostrar u ocultar menú (Ctrl+B)", como hoy.

### Iconos del menú

Material Design Icons, 24×24, dibujados a 20×20 con el estilo `menuIcon` (FR-016). La tabla
siguiente es la asignación completa: 41 elementos y ningún icono repetido (FR-015, SC-005). Una
clave marcada como *nueva* se agrega a `Resources/Icons.axaml` con la ruta del icono MDI indicado.

| Elemento | Id | Clave | Icono MDI |
|---|---|---|---|
| **Inicio** | `home` | `Icon.Home` | home |
| **Grupo Ventas** | `sales` | `Icon.Sales` | cart |
| Punto de venta | `sales.pos` | `Icon.CashRegister` *(nueva)* | cash-register |
| Historial de ventas | `sales.history` | `Icon.ReceiptHistory` *(nueva)* | receipt-text-clock |
| Mi turno | `sales.my-shift`¹ | `Icon.AccountClock` *(nueva)* | account-clock |
| Turnos | `sales.shifts`¹ | `Icon.CalendarClock` *(nueva)* | calendar-clock |
| Devoluciones | `sales.returns` | `Icon.CashRefund` *(nueva)* | cash-refund |
| **Grupo Caja** | `cash`¹ | `Icon.CashMultiple` *(nueva)* | cash-multiple |
| Corte X | `cash.readout`¹ | `Icon.ReceiptText` *(nueva)* | receipt-text |
| Cierre de turno | `cash.closing`¹ | `Icon.CashLock` *(nueva)* | cash-lock |
| Cortes | `cash.cuts`¹ | `Icon.Archive` *(nueva)* | archive |
| **Grupo Clientes** | `customers`¹ | `Icon.Users` | account-group |
| Clientes | `customers.list`¹ | `Icon.CardAccount` *(nueva)* | card-account-details |
| **Grupo Descuentos** | `discounts`¹ | `Icon.TagMultiple` *(nueva)* | tag-multiple |
| Cupones | `discounts.coupons`¹ | `Icon.TicketPercent` *(nueva)* | ticket-percent |
| Configuración de descuentos | `discounts.settings`¹ | `Icon.Percent` *(nueva)* | percent |
| Reporte de descuentos | `discounts.report`¹ | `Icon.ChartPie` *(nueva)* | chart-pie |
| **Grupo Reportes** | `reports`¹ | `Icon.Chart` | chart-bar |
| Ventas | `reports.sales`¹ | `Icon.ChartLine` *(nueva)* | chart-line |
| Corte de caja | `reports.cash-count`¹ | `Icon.CashCheck` *(nueva)* | cash-check |
| Inventario | `reports.inventory`¹ | `Icon.ClipboardList` *(nueva)* | clipboard-list |
| Compras | `reports.purchases`¹ | `Icon.TruckCheck` *(nueva)* | truck-check |
| Cuentas por cobrar | `reports.receivables`¹ | `Icon.AccountCash` *(nueva)* | account-cash |
| **Grupo Catálogos** | `catalogs` | `Icon.Catalog` | folder |
| Productos | `catalogs.products` | `Icon.Product` | tag |
| Categorías | `catalogs.categories`¹ | `Icon.Shape` *(nueva)* | shape |
| **Grupo Inventario** | `inventory` | `Icon.Inventory` | package-variant |
| Existencias | `inventory.stock` | `Icon.Stock` | format-list-bulleted-square |
| Movimientos | `inventory.movements` | `Icon.Movements` | swap-horizontal |
| Entrada de compra | `inventory.purchase-entry`¹ | `Icon.TruckDelivery` *(nueva)* | truck-delivery |
| Proveedores | `inventory.suppliers`¹ | `Icon.Factory` *(nueva)* | factory |
| **Grupo Administración** | `administration` | `Icon.Shield` | shield-lock |
| Usuarios | `administration.users` | `Icon.AccountKey` *(nueva)* | account-key |
| Bitácora | `administration.audit` | `Icon.TextSearch` *(nueva)* | text-box-search |
| **Grupo Configuración** | `settings` | `Icon.Cog` *(nueva)* | cog |
| Datos del negocio | `settings.business` | `Icon.Store` *(nueva)* | store |
| Impresora | `settings.printer` | `Icon.Printer` *(nueva)* | printer |
| Seguridad | `settings.security` | `Icon.Lock` | lock |
| Probar escáner | `settings.scanner-test` | `Icon.BarcodeScan` *(nueva)* | barcode-scan |
| **Grupo Ayuda** | `help` | `Icon.Help` | help-circle |
| Acerca de | `help.about` | `Icon.Info` | information |

¹ Ids ilustrativos: se usa la constante `*PageId`/`GroupId` que ya existe en cada módulo. La
asignación se hace por elemento, no por id.

- Las claves existentes no cambian de geometría; las tarjetas de Inicio las siguen usando.
- Con el menú contraído, cada icono muestra el nombre de su opción en el tooltip (FR-017, ya
  existe).
- El menú flotante de un grupo contraído muestra los títulos de sus opciones.

## Botones de acción principal (US7)

Estilos globales en `Resources/Styles.axaml`:

| Selector | Contrato |
|---|---|
| `Button` | Contenido centrado horizontal y verticalmente, salvo que la vista declare otra alineación (FR-018). |
| `Button.action`, `Button.touch` | Además, el texto se ajusta en varias líneas centradas y nunca sale del botón (FR-019). |

La clase `action` se aplica a:

- **Punto de venta**:
  - Ingreso, Retiro y Cerrar turno de la barra de turno;
  - Abrir turno y Cerrar turno de otro (ya llevan `touch`).
  - Cobrar y los botones laterales ya llevan `touch`.
- **Cobro**: todos ya llevan `touch`.
- **Diálogos de caja**:
  - `OpenShiftView`;
  - `CashMovementView` (Guardar, Cancelar, Imprimir, Listo);
  - `CloseShiftView` (los 6 botones).
- **Caja**: "Generar corte X" (`ShiftReadoutView`) y "Cerrar turno" (`ShiftClosingView`).
- **Inicio**: las tarjetas no son botones de acción; se rigen por la sección de tarjetas.

## Tarjetas de indicadores de Inicio (US9)

| Aspecto | Contrato |
|---|---|
| Tamaño | 240×150 fijo en todas las tarjetas de indicadores; la fila queda alineada (FR-026). |
| Valor numérico | 34 pt en negrita (como hoy). |
| Valor de texto ("30 días restantes", "Activa") | 22 pt en negrita, una línea, recortado con "…" (FR-027). |
| Mensaje | Máximo 2 líneas, recortado con "…". |
| Tooltip | Título, valor y mensaje completos (FR-028). |

## Configuración y Acerca de (US10)

**Grupo Configuración** (orden 80), en este orden:

| Opción | Permiso |
|---|---|
| Datos del negocio | `ManageSettings` |
| Impresora | `ManageSettings` |
| Seguridad | `ManageSettings` |
| Probar escáner | ninguno (todos los roles) |

Un usuario sin `ManageSettings` ve el grupo con solo "Probar escáner" (FR-031).

**Grupo Ayuda** (orden 90): solo "Acerca de" (FR-029).

**Pantalla "Acerca de"** (FR-030), de arriba abajo:

1. Producto ("Octopus punto de venta"), desarrollador ("Omar Ceron Ochoa") y email
   (omarbs13@gmail.com), en texto seleccionable, para todos los roles.
2. Versión (texto seleccionable).
3. ID de máquina (texto seleccionable), para todos los roles.
4. Carpeta de datos (texto seleccionable) con "Copiar ruta", para todos los roles.
5. Exportar diagnóstico (con "Incluir base de datos"), solo con `ExportDiagnostics`.
6. Administración de licencia (resumen, Importar y Exportar solicitud), solo con `ManageLicense`.

Ya no aparecen: sistema operativo ni "Probar escáner".

## Encabezado del negocio (US8)

El orden canónico, igual en todos los formatos (FR-024), lo define `BusinessHeader.Lines`
([data-model.md](../data-model.md#businessheader-application-presentación)):

```text
[logo]  Nombre comercial            ← negrita
        Dirección (ajustada en varias líneas)
        Tel. 555-1234
        RFC: XAXX010101000           ← solo si existe
```

| Formato | Contrato |
|---|---|
| PDF | Solo en la página 1 (clarificación). Logo a la izquierda en una caja de 120×48 pt, escalado sin deformar. Si no hay logo, el texto empieza en el margen. Línea separadora debajo. Las páginas 2 y siguientes empiezan en el margen superior, sin encabezado de negocio. El pie (fecha, usuario y página) sigue en todas. |
| PDF sin datos del negocio | Página 1: "Datos del negocio no capturados" en gris, en lugar del bloque (FR-025). |
| XLSX | Hoja "Resumen", filas 1…n: las líneas del encabezado (nombre en negrita), sin logo; después, una fila vacía y el título del reporte. Sin datos del negocio: "Datos del negocio no capturados" en la fila 1. |
| Ticket (venta, nota de crédito, abono, corte X, corte Z/turno, movimiento de caja) | Logo (si la impresora puede y existe), seguido de las líneas centradas y ajustadas al ancho de 32 o 48 columnas, con el nombre en negrita. Sin datos del negocio, el ticket empieza en el título, como hoy. |

Aplica a todos los reportes exportables: Ventas, Corte de caja, Inventario, Mi turno, Descuentos y
Bitácora.
