# Contrato: pantalla de inicio y tarjetas

## DashboardCard

```text
string Title, Icon
int Order
DashboardCardKind Kind            // Metric | Chart
DashboardCardState State          // Loading | Ready | Empty | Error
string? Value                     // en Ready
string? EmptyMessage              // en Empty
string? NavigateTo                // id de NavigationEntry
Task LoadAsync()                  // la invoca HomeViewModel; no debe lanzar
```

Un módulo agrega una tarjeta con `services.AddDashboardCard<TCard>()`, sin modificar
`HomeView` ni `HomeViewModel`.

## Tarjetas de esta fase

| Tarjeta | Tipo | Estado | Valor o mensaje | Destino |
|---|---|---|---|---|
| Productos activos | Metric | Ready | Conteo real (`CountActiveProducts`) | `catalogs.products` |
| Existencia baja | Metric | Empty | "Disponible con el módulo de Inventario" | — |
| Sin existencia | Metric | Empty | "Disponible con el módulo de Inventario" | — |
| Ventas del día | Chart | Empty | "Disponible con el módulo de Ventas" | — |
| Ventas de los últimos 7 días | Chart | Empty | "Disponible con el módulo de Ventas" | — |
| Productos más vendidos | Chart | Empty | "Disponible con el módulo de Ventas" | — |

Ninguna tarjeta muestra datos de ejemplo. Una tarjeta vacía se ve atenuada, con su ícono y el
mensaje; no se muestran ejes ni barras simuladas.

## HomeViewModel

- `OnActivatedAsync()`: pone todas las tarjetas en `Loading` y ejecuta `LoadAsync()` de todas en
  paralelo.
- Error en una tarjeta: `State = Error`, texto "No disponible", registro con la operación
  `CargarTarjeta` y el título de la tarjeta. No hay diálogo modal y las demás tarjetas no se
  afectan.
- Activar una tarjeta con `NavigateTo` y en estado `Ready` llama a `Navigator.NavigateAsync`.
