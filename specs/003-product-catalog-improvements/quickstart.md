# Quickstart: validar las mejoras al catálogo de Productos

Guía para comprobar la funcionalidad de punta a punta. Los detalles de diseño están en
[data-model.md](data-model.md) y [contracts/](contracts/).

## Prerrequisitos

- SDK de .NET 10 (ver `global.json`) en Windows o Linux.
- Una carpeta de datos desechable para no tocar la real:

```bash
export POS_DATA_DIR="$(mktemp -d)"          # Linux
# $env:POS_DATA_DIR = "$env:TEMP\pos-003"   # Windows PowerShell
```

## 1. Compilar y probar

```bash
dotnet build            # 0 errores y 0 advertencias
dotnet test             # toda la suite en verde
```

Pruebas clave de esta funcionalidad:

| Qué verifica | Dónde |
|---|---|
| Reproducción del defecto de inactivos (falla sin la corrección) | `tests/Pos.Desktop.Tests/Products/ProductsViewModelInactiveFilterTests` |
| Filtro, paginación, orden estable, página fuera de rango, localizar producto | `tests/Pos.Infrastructure.Tests/Products/ProductPagingTests` |
| 10,000 productos: páginas, búsqueda y filtro en menos de 1 s | `tests/Pos.Infrastructure.Tests/Products/ProductPerformanceTests` |
| Precio: 0, 0.01, 999,999.99, 1,000,000, 3 decimales y formatos con y sin separador de miles | `tests/Pos.Domain.Tests/Common/MoneyTests`, `tests/Pos.Application.Tests/Products/*ValidatorTests` |
| Imagen: formatos, 5 MB, dañada, extensión engañosa, EXIF, transparencia, tamaños | `tests/Pos.Infrastructure.Tests/Products/SkiaImageProcessorTests` |
| Imagen guardada, reemplazada y quitada en una transacción; `Version` incrementa | `tests/Pos.Infrastructure.Tests/Products/ProductImagePersistenceTests` |
| Imagen en respaldo y restauración; ausente del diagnóstico | `tests/Pos.Infrastructure.Tests/Startup/SqliteBackupServiceTests`, `Diagnostics/ZipDiagnosticsExporterTests` |
| Caso de uso de preparación de imagen | `tests/Pos.Application.Tests/Products/PrepareProductImageHandlerTests` |
| Migración desde `v0.1.0.db` y `v0.2.0.db` (unidad Pieza, imágenes conservadas, sin pérdida) | `tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests` |

## 2. Datos de prueba de 10,000 productos

La prueba de rendimiento (`ProductPerformanceTests`) genera su propio catálogo de 10,000
productos, con 500 inactivos, y mide la primera página, la última página, la búsqueda y el cambio
de filtro (SC-002):

```bash
dotnet test tests/Pos.Infrastructure.Tests --filter "FullyQualifiedName~ProductPerformanceTests"
```

No hay un generador de catálogo grande para la aplicación; la validación manual (§3) usa unos
cientos de productos capturados o importados a mano.

## 3. Validación manual en la aplicación

```bash
dotnet run --project src/Pos.Desktop
```

1. **Inactivos (H1)**:
   1. Con la casilla desmarcada, solo aparecen activos.
   2. Márcala: aparecen los inactivos atenuados y con "Inactivo", y la columna Estado se ve.
   3. Borra un producto: no aparece con ninguna combinación de filtro y búsqueda.
2. **Paginación (H2)**:
   1. Con más de 100 productos, verifica "N registros · Página 1 de M".
   2. Navega con los cuatro botones y con Alt+AvPág y Alt+Fin.
   3. Estando en la página 3, busca algo: vuelve a la página 1.
   4. Cambia el filtro: vuelve a la página 1.
3. **Validaciones (H3)**:
   1. Abre "Nuevo producto": nombre, SKU, precio y unidad tienen asterisco.
   2. Prueba los precios `0` (rechazado), `0.01` (aceptado), `1,234.50` (se guarda 1234.50),
      `12,50` (rechazado, formato), `999999.991` (rechazado, decimales) y `1000000` (rechazado,
      máximo).
4. **Imagen (H4)**:
   1. Asigna un JPG, guarda y comprueba la miniatura en el listado.
   2. Cambia la imagen por un PNG con transparencia y luego quítala.
   3. Intenta cargar un archivo de más de 5 MB, un `.txt` renombrado a `.jpg` y un GIF: cada uno
      se rechaza con su mensaje.
   4. Cancela un cambio de imagen: la imagen anterior se conserva.
5. **Respaldo e imagen**:
   1. Con un producto con imagen, cierra la app.
   2. Borra los respaldos de `$POS_DATA_DIR/backups/auto` y fuerza un respaldo nuevo (se crea al
      cerrar si el último tiene más de 24 h, o al arrancar si no hay ninguno).
   3. Quita la imagen del producto.
   4. Restaura el respaldo según `docs/carpeta-de-datos.md`: la imagen vuelve.
6. **Diagnóstico**:
   1. Acerca de → Exportar diagnóstico.
   2. Abre el `pos.db` del zip: `SELECT COUNT(*) FROM ProductImages` devuelve 0 y los productos
      están completos.

## Resultado esperado

Todos los pasos coinciden con los escenarios de aceptación de [spec.md](spec.md) y los criterios
SC-001 a SC-007.
