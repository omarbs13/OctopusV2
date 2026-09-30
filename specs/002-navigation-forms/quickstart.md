# Quickstart: validar la navegación y los formularios

Guía para comprobar de punta a punta que la funcionalidad cumple [spec.md](spec.md). El
comportamiento detallado está en [contracts/](contracts/).

## 1. Pruebas automáticas

```bash
dotnet build
dotnet test
```

**Resultado esperado**: 0 advertencias y todas las pruebas aprobadas, en particular:

| Suite / clase | Qué demuestra |
|---|---|
| `Pos.Application.Tests` · `DatabaseStartupProgressTests` | Los pasos de arranque se informan en orden (pantalla de carga) |
| `Pos.Application.Tests` · `CountActiveProductsHandlerTests` / `Pos.Infrastructure.Tests` | Conteo real de productos activos (SC-006) |
| `Pos.Infrastructure.Tests` · `JsonFilePreferencesStoreTests` | Preferencias persistentes, archivo dañado o inexistente |
| `Pos.Desktop.Tests` · `MenuViewModelTests` | Expandir, contraer, grupos, auto-contracción y persistencia (SC-003) |
| `Pos.Desktop.Tests` · `NavigatorTests` | Navegación, protección de salida y conservación de estado |
| `Pos.Desktop.Tests` · `ModuleRegistrationTests` | Una opción y una tarjeta de prueba aparecen sin modificar vistas (SC-007) |
| `Pos.Desktop.Tests` · `HomeViewModelTests` | Datos reales, estados vacíos y aislamiento de errores |
| `Pos.Desktop.Tests` · `FormViewModelTests` y `LargeFormTests` | Patrón corto y grande (SC-004) |
| `Pos.Desktop.Tests` · `UnsavedChangesTests` | Cancelar, navegar, cerrar el panel y cerrar la aplicación con las tres opciones; sin pregunta si no hubo cambios o si se revirtieron (SC-005) |

## 2. Recorrido manual

```bash
POS_DATA_DIR=/tmp/pos-nav dotnet run --project src/Pos.Desktop
```

| # | Pasos | Resultado esperado |
|---|---|---|
| 1 | Abrir la aplicación | Pantalla de carga con logotipo, nombre, versión y texto del paso; luego Inicio |
| 2 | Observar Inicio | "Productos activos" con el número real; existencias y ventas en estado vacío, sin números |
| 3 | Clic en "Productos activos" | Se abre Productos; en el menú se marcan Productos y Catálogos |
| 4 | Ctrl+B | El menú se contrae a íconos; tooltips al pasar el puntero |
| 5 | Contraído: clic en Inventario | Menú flotante con Existencias y Movimientos; elegir Existencias abre "disponible más adelante" |
| 6 | Expandir el menú, cerrar Catálogos, abrir Inventario; cerrar y reabrir la aplicación | El menú conserva el estado y los grupos |
| 7 | Reducir la ventana a menos de 1000 px y luego ampliarla | Se contrae sola y vuelve al estado elegido |
| 8 | Productos: buscar "leche", activar "Mostrar inactivos", ir a Inicio y volver | Se conservan búsqueda, filtro y selección |
| 9 | Nuevo producto, escribir un nombre, clic en Inicio en el menú | Pregunta Guardar, Descartar o Seguir editando; probar las tres |
| 10 | Editar un producto, cambiar el nombre y volver a escribir el original, Cancelar | Se cierra sin preguntar |
| 11 | Nuevo producto con cambios y cerrar la ventana; elegir Seguir editando | La aplicación no se cierra |
| 12 | Guardar con campos vacíos | Todos los errores juntos y el foco en el primero; salir de un campo no muestra errores |
| 13 | Copiar un `logo.png` a la carpeta de datos y reabrir | La pantalla de carga muestra el nuevo logotipo |

Repetir en Windows (SC-001).
