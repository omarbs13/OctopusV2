# POS

Punto de venta de escritorio para Windows y Linux, en .NET 10 con Avalonia y SQLite local.
Funciona sin conexión a internet. Las reglas del proyecto están en la
[constitución](.specify/memory/constitution.md).

Versión actual: **0.8.0**. Incluye catálogo de productos, control de inventario, el módulo de
ventas (Punto de venta, cobro, ventas realizadas, cancelación y datos reales en Inicio), turnos de caja,
usuarios y roles, y reportes de ventas, arqueo e inventario exportables a PDF y Excel.

## Prerrequisitos

| | Windows | Linux |
|---|---|---|
| SDK de .NET | 10.0.1xx (la versión exacta está fijada en `global.json`) | Igual |
| Entorno gráfico | Windows 10 o posterior | X11, o Wayland con XWayland |
| Otros | — | `libicu`, necesaria para el formato de moneda es-MX (en la mayoría de las distribuciones ya viene instalada) |

Comprueba el SDK con `dotnet --list-sdks`.

## Compilar, probar y ejecutar

Todos los comandos se ejecutan desde la raíz del repositorio y funcionan igual en PowerShell y
en bash.

```bash
dotnet tool restore        # herramientas locales (dotnet-ef)
dotnet build               # compila toda la solución; las advertencias son errores
dotnet test                # ejecuta todas las pruebas
dotnet run --project src/Pos.Desktop
```

`dotnet test` usa Microsoft.Testing.Platform con xUnit v3 (configurado en `global.json`). Para
ejecutar solo un proyecto o una clase de pruebas:

```bash
dotnet test --project tests/Pos.Domain.Tests
dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.Products.ProductSearchTests"
```

### Usar otra carpeta de datos

La aplicación guarda sus datos en la carpeta del usuario (ver
[docs/carpeta-de-datos.md](docs/carpeta-de-datos.md)). Para probar sin tocar esos datos,
define `POS_DATA_DIR`:

```bash
# Linux
POS_DATA_DIR=/tmp/pos-prueba dotnet run --project src/Pos.Desktop
```

```powershell
# Windows (PowerShell)
$env:POS_DATA_DIR = "$env:TEMP\pos-prueba"; dotnet run --project src/Pos.Desktop
```

## Estructura

```text
Pos.slnx                    Solución (en la raíz)
src/
  Pos.Domain/               Entidades y reglas puras (Product, Money)
  Pos.Application/          Casos de uso, validación y puertos (interfaces)
  Pos.Infrastructure/       EF Core + SQLite, respaldos, diagnóstico, plataforma
  Pos.Desktop/              UI Avalonia (MVVM); Composition/ es la raíz de composición
tests/
  Pos.<Capa>.Tests/         Pruebas por capa
  Pos.ArchitectureTests/    Reglas de dependencia entre capas
docs/                       Documentación para desarrollo y soporte
specs/                      Especificaciones de Spec Kit por funcionalidad
```

Las dependencias van hacia el centro: Desktop → Application → Domain. Infrastructure implementa
los puertos de Application. Las pruebas de `Pos.ArchitectureTests` hacen fallar la compilación
de la suite si se rompe una regla.

## Documentación

- [Carpeta de datos, respaldos y logs](docs/carpeta-de-datos.md)
- [Migraciones de la base de datos](docs/migraciones.md)
- [Cómo agregar una funcionalidad](docs/agregar-funcionalidad.md)
- [Módulo de ventas: atajos, folio, recuperación y cancelación](docs/ventas.md)
- [Reportes y análisis: cifras, permisos, umbral de alerta y exportación](docs/reportes.md)

## Integración continua

`.github/workflows/ci.yml` compila y prueba en Ubuntu y en Windows en cada push a `main` o
`Develop` y en cada pull request.
