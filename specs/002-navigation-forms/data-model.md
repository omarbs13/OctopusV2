# Data Model: Estructura de navegación y formularios

Esta funcionalidad **no cambia el esquema de la base de datos** (no hay migraciones). Sus datos
son registros en memoria y un archivo de preferencias. Las decisiones están en
[research.md](research.md).

## NavigationGroup (registro)

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `string` | Único; en minúsculas con punto (por ejemplo `catalogs`) |
| `Title` | `string` | Texto de `Strings.resx` |
| `Icon` | `string` | Clave de ícono en `Resources/Icons.axaml` |
| `Order` | `int` | Orden en el menú (de menor a mayor) |

## NavigationEntry (registro)

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `string` | Único (por ejemplo `catalogs.products`); es el destino de navegación |
| `Title` | `string` | Texto de `Strings.resx` |
| `Icon` | `string` | Clave de ícono |
| `Order` | `int` | Orden dentro de su grupo o del primer nivel |
| `GroupId` | `string?` | Nulo para las opciones de primer nivel (Inicio). Si no nulo, debe existir el grupo |
| `ViewModelType` | `Type` | Subtipo de `PageViewModel`, singleton |

Reglas: máximo dos niveles; un grupo sin opciones no se muestra. Un `Id` duplicado o un
`GroupId` inexistente es un error de configuración que se detecta al construir el menú (lanza
una excepción al arrancar y lo verifica una prueba).

**Estructura inicial**

| Orden | Grupo | Opción (`Id`) | Ícono |
|---|---|---|---|
| 0 | — | Inicio (`home`) | `Icon.Home` |
| 10 | Catálogos (`catalogs`) | Productos (`catalogs.products`) | `Icon.Catalog` / `Icon.Product` |
| 20 | Inventario (`inventory`) | Existencias (`inventory.stock`), Movimientos (`inventory.movements`) | `Icon.Inventory` / `Icon.Stock`, `Icon.Movements` |
| 90 | Ayuda (`help`) | Acerca de (`help.about`) | `Icon.Help` / `Icon.Info` |

## ViewRegistration (registro)

`ViewModelType` → fábrica de la vista (`Func<Control>`). La usa `RegisteredViewLocator`.

## Preferencias de navegación (archivo)

`<carpeta de datos>/preferences/navigation.json`:

```json
{ "collapsed": false, "expandedGroups": ["catalogs", "inventory"] }
```

- Un archivo inexistente o dañado equivale a `{ collapsed: false, expandedGroups: [] }`, y además
  se abre el grupo de la opción actual.
- Los ids de grupos que ya no existen se ignoran.
- Solo se guarda por una acción del operador (alternar el menú, abrir o cerrar un grupo). La
  contracción automática por ancho no se guarda.

## Estado del menú (en memoria)

```text
IsUserCollapsed   ← preferencia o acción del operador
IsAutoCollapsed   ← ancho de ventana < 1000
IsCollapsed       = IsUserCollapsed || IsAutoCollapsed
CurrentEntryId    ← Navigator
Group.IsExpanded  ← preferencia o acción del operador
Group.IsCurrent   = el grupo contiene CurrentEntryId
```

## DashboardCard (registro y estado)

| Campo | Tipo | Reglas |
|---|---|---|
| `Title`, `Icon` | `string` | Recursos |
| `Order` | `int` | Orden de presentación |
| `Kind` | `Metric` \| `Chart` | Las métricas van en la franja superior y las gráficas debajo |
| `State` | `Loading` \| `Ready` \| `Empty` \| `Error` | Ver transiciones |
| `Value` | `string?` | Solo en `Ready` (ya formateado; por ejemplo, "12") |
| `EmptyMessage` | `string?` | Solo en `Empty` |
| `NavigateTo` | `string?` | Id de `NavigationEntry`; solo activa en `Ready` |

```text
Loading ──ok──► Ready
Loading ──sin módulo──► Empty       (permanente hasta que exista el módulo)
Loading ──excepción──► Error        (se registra; las demás tarjetas no se afectan)
Ready/Error ──al volver a Inicio──► Loading
```

## Estado de un formulario (en memoria)

| Campo | Descripción |
|---|---|
| `OriginalState` | Registro normalizado capturado al abrir, al recargar o después de guardar |
| `CurrentState` | `CaptureState()` en cada consulta |
| `IsDirty` | `!Equals(OriginalState, CurrentState)` |
| `Errors` | Por campo; se asignan al intentar guardar y se limpian en el siguiente intento |
| `FocusField` | Primer campo con error |

**Estado normalizado de Producto**: `(Name.Trim(), Sku.Trim().ToUpperInvariant(),
Barcode vacío → null, precio en centavos si `Money.TryParse` lo acepta o, si no, el texto
recortado, IsActive)`.

## Conteo de productos activos (consulta)

`CountActiveProductsHandler` → `long`: productos con `IsActive = 1` y `DeletedAt IS NULL`.
