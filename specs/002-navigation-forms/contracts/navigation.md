# Contrato: registro de módulos y navegación

## Registro (DI, en cada módulo)

```text
services.AddNavigationGroup(id, title, icon, order)
services.AddPage<TViewModel, TView>(id, title, icon, order, groupId?)
services.AddComingSoonPage(id, title, icon, order, groupId)
services.AddDashboardCard<TCard>()
```

Cada módulo agrupa sus registros en `Add<Modulo>Module()`:

- `AddHomeModule()`: Inicio y sus tarjetas propias.
- `AddProductsModule()`: grupo Catálogos, pantalla Productos y la tarjeta de productos activos.
- `AddInventoryModule()`: grupo Inventario, pantallas "disponible más adelante" y las tarjetas
  vacías de existencias.
- `AddSalesPlaceholders()`: las tres gráficas vacías de ventas.
- `AddHelpModule()`: grupo Ayuda y Acerca de.

`HostBuilder` solo invoca esos métodos. Agregar un módulo **no modifica** vistas, `App.axaml`,
`MenuView` ni `HomeView` (SC-007).

## Navigator

```text
string CurrentEntryId
PageViewModel CurrentPage
Task<bool> NavigateAsync(string entryId)      // falso si la pantalla actual no permite salir
Task<bool> CanLeaveCurrentAsync()             // se usa al cerrar la aplicación
event CurrentChanged
```

1. Si `entryId` es la opción actual, no hace nada y devuelve verdadero.
2. Si la pantalla actual implementa `ILeaveGuard` y `CanLeaveAsync()` devuelve falso, no navega.
3. Cambia `CurrentPage` (el singleton registrado) y luego invoca `OnActivatedAsync()`, que
   refresca los datos conservando la búsqueda, los filtros y la selección.
4. Una opción inexistente es un error de programación: se registra y no se navega.

La primera navegación, al abrir la ventana principal, es a `home`.

## Menú (comportamiento)

| Acción | Resultado |
|---|---|
| Clic o Enter en una opción | `NavigateAsync(id)`; si es exitosa, la opción queda marcada como actual y su grupo como grupo actual |
| Clic en un grupo (expandido) | Alterna `IsExpanded` y guarda la preferencia |
| Clic en un grupo (contraído) | Abre un menú flotante con sus opciones; elegir una navega y cierra el menú flotante |
| Botón ☰ o Ctrl+B | Alterna `IsUserCollapsed` y guarda la preferencia |
| Ancho de ventana < 1000 | `IsAutoCollapsed = true` (no se guarda) |
| Ancho ≥ 1000 | `IsAutoCollapsed = false`; se vuelve al estado elegido por el operador |
| Puntero sobre un ícono (contraído) | Tooltip con el nombre |

Áreas de toque de al menos 44 × 44. La opción actual tiene fondo de acento; su grupo, texto en
negrita y una marca lateral en ambos estados.
