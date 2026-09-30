# Quickstart: validar el manejo de inventario

Guía para comprobar la funcionalidad de punta a punta. Los detalles del modelo están en
[data-model.md](data-model.md) y los contratos, en [contracts/](contracts/).

## Requisitos

- .NET 10 SDK.
- Repositorio en la rama de la funcionalidad.
- Una base de datos existente de 003 (opcional, para ver la migración con datos reales).

## 1. Compilar y probar

```bash
dotnet build -v q
dotnet test --verbosity quiet
```

**Esperado**: 0 errores, 0 advertencias y todas las pruebas en verde. Al implementar basta con
ejecutar las pruebas del proyecto modificado (Principio VI); por ejemplo:
`dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet`.

## 2. Pruebas obligatorias de esta funcionalidad

| Prueba | Proyecto | Qué demuestra |
|---|---|---|
| Consistencia de inventario | `Pos.Infrastructure.Tests/Inventory` | Tras una secuencia aleatoria reproducible (semilla fija) de movimientos válidos e inválidos sobre varios productos y unidades, en SQLite real, se cumplen los 6 invariantes de [data-model.md](data-model.md#invariantes-de-consistencia-prueba-obligatoria-principio-vi) (criterio 2, SC-001, SC-007) |
| Atomicidad | `Pos.Infrastructure.Tests/Inventory` | Una falla forzada después de agregar el movimiento y antes de confirmar deja sin cambios tanto `ProductStocks` como `InventoryMovements` (criterio 1) |
| Escritura serializada | `Pos.Infrastructure.Tests/Inventory` | Dos ajustes negativos concurrentes sobre una existencia que solo alcanza para uno: uno se guarda y el otro se rechaza; la existencia nunca queda negativa (research §5) |
| Inmutabilidad | `Pos.Infrastructure.Tests/Inventory` | Modificar o borrar un `InventoryMovement` por el contexto lanza excepción (criterio 5) |
| Ajuste negativo | `Pos.Domain.Tests/Inventory` | Con existencia 3, un `AdjustOut` de 3 deja 0 y uno de 3.001 (kg) se rechaza (criterio 3) |
| Decimales por unidad | `Pos.Domain.Tests/Common` | `Quantity.Parse("1.5", 0)` da `TooManyDecimals`; `Parse("1.250", 3)` es válido y `Parse("1.2505", 3)` no (criterio 4) |
| Estado de existencia | `Pos.Infrastructure.Tests/Inventory` | El filtro y el conteo en SQL coinciden con `StockStatusRule.Evaluate` en los casos frontera: igual al mínimo, 0 con mínimo, sin mínimo y sin fila (criterio 6, SC-006) |
| Bloqueo de unidad | `Pos.Application.Tests/Products` | `UpdateProduct` rechaza el cambio de unidad o desactivar el inventario si hay movimientos (FR-006) |
| Migración | `Pos.Infrastructure.Tests/SampleDatabases` | `v0.1.0.db`, `v0.2.0.db` y `v0.3.0.db` migran a la versión actual; los productos existentes quedan con `TracksInventory = 0` (FR-023) |
| Rendimiento | `Pos.Infrastructure.Tests/Inventory` | Con 10,000 productos y 100,000 movimientos, cada página de Existencias y de Movimientos, con y sin filtros, tarda menos de 2 s (SC-005) |
| Arquitectura | `Pos.ArchitectureTests` | Las reglas existentes siguen pasando con los espacios `Inventory` nuevos |

## 3. Recorrido manual

Ejecutar la aplicación con `dotnet run --project src/Pos.Desktop`.

1. **Migración**: abrir con una base de 003. La aplicación respalda y migra. En Productos, la
   columna "Existencia" muestra "—" en todos los productos.
2. **Configurar (H1)**:
   - Editar un producto en Kilogramo: marcar "Controla inventario" y poner como mínimo `5`.
     Guarda, y la existencia actual se muestra en 0, solo lectura.
   - Probar un mínimo `1.5` en un producto en Pieza: se rechaza con mensaje.
3. **Existencias (H3)**:
   - Menú Inventario > Existencias: el producto aparece "Sin existencia".
   - Buscar por SKU y por código de barras.
4. **Movimientos (H2)**, desde Existencias, con el producto seleccionado > Registrar movimiento:
   - El tipo por omisión es "Inventario inicial", cantidad `10.500`: la existencia queda en
     10.500 y el estado en "Normal".
   - Volver a abrir el formulario: "Inventario inicial" ya no aparece.
   - Ajuste negativo `20` con motivo: se rechaza con el mensaje de existencia insuficiente y los
     datos no cambian.
   - Ajuste negativo `6` sin motivo: se rechaza pidiendo el motivo. Con motivo, la existencia
     queda en 4.500 y el estado en "Existencia baja".
   - Entrada `2` con referencia `F-1234`: la existencia queda en 6.500.
5. **Kárdex (H4)**:
   - En Productos > Ver movimientos: la pantalla Movimientos ya viene filtrada por el producto,
     con 3 movimientos del más reciente al más antiguo, la existencia resultante de cada uno, la
     referencia y el usuario "Sistema".
   - Filtrar por tipo y por rango de fechas.
   - Comprobar que no existe ninguna opción para editar ni borrar un movimiento.
6. **Bloqueo de unidad**: editar el producto. La unidad y "Controla inventario" están
   deshabilitadas, con una nota.
7. **Alertas (H5)**:
   - En Inicio, "Existencia baja" y "Sin existencia" muestran conteos reales.
   - Al hacer clic se abre Existencias con el filtro aplicado, y el número de filas coincide con
     la tarjeta.
   - Desactivar el producto en baja: deja de contar en la tarjeta y solo aparece en Existencias
     con "Incluir inactivos".
8. **Producto sin inventario**: un servicio no aparece en Existencias ni en el selector de
   producto del formulario de movimiento.

## 4. Revisión de la migración

```bash
dotnet ef migrations script --idempotent -p src/Pos.Infrastructure -s src/Pos.Infrastructure
```

Confirmar que la migración `InventoryManagement` no reconstruye `Products`: solo usa `ADD COLUMN`,
`CREATE TABLE`, `CREATE INDEX` y `UPDATE` sobre `UnitsOfMeasure`.
