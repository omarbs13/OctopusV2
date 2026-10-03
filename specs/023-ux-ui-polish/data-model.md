# Data Model: Mejoras de UX/UI y comportamiento de la aplicación

**Feature**: 023-ux-ui-polish | **Fecha**: 2026-10-02

**No hay cambios en la base de datos.** Las entidades nuevas son preferencias locales en JSON,
guardadas con el `IPreferencesStore` existente en `<datos>/preferences/`, y un modelo de
presentación del encabezado del negocio en Application.

## WindowPlacement (preferencia, por equipo)

Ubicación: `Pos.Desktop/Shell/WindowPlacement.cs`. Clave: `window` → `window.json`.

| Campo | Tipo | Regla |
|---|---|---|
| `State` | `string` (`"Maximized"` \| `"Normal"`) | Último estado no minimizado (FR-003). Un valor desconocido se trata como sin preferencia. |
| `X`, `Y` | `int` | Posición en píxeles de pantalla del estado normal. |
| `Width`, `Height` | `double` | Tamaño del área cliente en DIP del estado normal; ≥ mínimo efectivo. |

**Resolución al arrancar** (`WindowPlacementRules.Resolve`, función pura):

| Entrada | Resultado |
|---|---|
| sin archivo, archivo dañado (FR-032) o `State` desconocido | Maximizada; tamaño normal por defecto 1200×800, centrada |
| `Maximized` | Maximizada; el tamaño y la posición guardados son los de la restauración |
| `Normal` y el rectángulo se cruza con el área de trabajo de alguna pantalla | Normal en `X`,`Y` con `Width`×`Height` |
| `Normal` y el rectángulo queda fuera de todas las pantallas (FR-004) | Normal, centrada en la pantalla principal |
| `Width`/`Height` < mínimo efectivo | Se elevan al mínimo efectivo |

Mínimo efectivo = `min(1024×768, área de trabajo de la pantalla en DIP)` (FR-005, FR-006).

**Cuándo se escribe**: una vez, al cerrar la ventana principal (`App.OnMainWindowClosing`).

## NavigationPreferences (preferencia, por usuario)

Ubicación: `Pos.Desktop/Navigation/MenuViewModel.cs` (ya existe; solo cambia la clave).

| Campo | Tipo | Regla |
|---|---|---|
| `Collapsed` | `bool` | Menú contraído a solo iconos por elección del usuario. |
| `ExpandedGroups` | `string[]` | Ids de los grupos expandidos. Un grupo ausente está colapsado (FR-011), incluidos los grupos nuevos. Los ids inexistentes se ignoran. |

- Clave: `navigation.{userId:N}`. Por ejemplo, `navigation.0199a1b2c3d4….json`. Sin sesión
  (pruebas): `navigation`.
- Se escribe cada vez que el usuario expande o colapsa un grupo, o alterna el menú (comportamiento
  actual).
- Sin archivo para el usuario → `Collapsed = false` y todos los grupos colapsados.

## BusinessHeader (Application, presentación)

Ubicación: `Pos.Application/Business/BusinessHeader.cs`. Sustituye a `ReportBusiness` en
`ReportDocument.Business`.

| Campo | Tipo | Regla |
|---|---|---|
| `Name` | `string` | Nombre comercial (obligatorio en `BusinessProfile`). |
| `Address` | `string?` | Nulo si está vacío. |
| `Phone` | `string?` | Nulo si está vacío. |
| `TaxId` | `string?` | RFC; nulo si no existe. |
| `Logo` | `byte[]?` | El logo optimizado de `BusinessProfile`; nulo si no existe. |

- `static BusinessHeader? From(BusinessProfileDto? profile)`: devuelve nulo si no hay perfil.
- `IReadOnlyList<BusinessHeaderLine> Lines`, en orden canónico (FR-024):
  1. `Name` (`IsTitle = true`);
  2. `Address`;
  3. `Tel. {Phone}`;
  4. `RFC: {TaxId}`.

  Los nulos se omiten sin dejar línea vacía (FR-023).
- `const string MissingText = "Datos del negocio no capturados"` (FR-025).

`BusinessHeaderLine(string Text, bool IsTitle)`.

## Datos existentes que solo se consumen

- **BusinessProfile** (006): `TradeName`, `Address`, `Phone`, `TaxId`, `Logo`. Sin cambios.
- **LicenseStatusDto** (011/012): se agrega `string MachineId` para mostrarlo en "Acerca de"
  (FR-030). Viene de `IMachineIdProvider.GetMachineId()`, que ya existe.
- **NavigationEntry / NavigationGroup**: sin cambios de forma. Cambian los valores de `Icon` y el
  registro de "Probar escáner" (`settings.scanner-test`, grupo `settings`, orden 30, sin
  permiso).
