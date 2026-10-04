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

## Generar los instaladores (Windows y Linux)

Todos los paquetes son *self-contained* y de un solo archivo: el equipo del usuario no necesita
tener .NET instalado. La versión sale de `<Version>` en `Directory.Build.props` y todo queda en
`artifacts/installers/`.

### Desde Linux

Requisitos: SDK de .NET 10, `curl`, `ar` (binutils) y ImageMagick. `appimagetool` se descarga
solo a `.tools/` la primera vez. Para el `setup.exe` de Windows hace falta además Inno Setup 6
instalado bajo Wine (o `iscc` en el PATH); si no está, el script avisa y genera el resto.

```bash
scripts/make-icons.sh                 # solo si no existe src/Pos.Desktop/Assets/pos.png
scripts/build-installers.sh           # all: Windows y Linux
scripts/build-installers.sh linux     # solo AppImage y .deb
scripts/build-installers.sh win       # solo el portable (y el setup.exe si hay Inno Setup)
```

### Desde Windows

Requisitos: SDK de .NET 10 e [Inno Setup 6](https://jrsoftware.org/isdl.php). El script busca
`ISCC.exe` en el PATH, en `Program Files (x86)`, en `Program Files` y en
`%LOCALAPPDATA%\Programs\Inno Setup 6`.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-installers.ps1
```

### Qué se genera

| Archivo | Sistema | Uso |
|---|---|---|
| `Pos-<versión>-win-x64-setup.exe` | Windows 10/11 x64 | Instalador con acceso directo en el menú Inicio y, opcional, en el escritorio. Por omisión instala solo para el usuario actual, sin permisos de administrador |
| `Pos-<versión>-win-x64-portable.exe` | Windows 10/11 x64 | Ejecutable suelto, sin instalación |
| `Pos-<versión>-x86_64.AppImage` | Cualquier Linux x86_64 | Se marca como ejecutable (`chmod +x`) y se abre; no se instala |
| `pos_<versión>_amd64.deb` | Debian, Ubuntu, Linux Mint y derivadas | Instala en `/opt/pos/`, el comando `pos` y la entrada en el menú de aplicaciones |

Para instalar el `.deb` (resuelve las dependencias, como `libicu`):

```bash
sudo apt install ./pos_<versión>_amd64.deb
```

### Datos del usuario

La aplicación nunca escribe junto al ejecutable: sus datos (base de datos, respaldos, logs,
licencia y preferencias) están en `%LOCALAPPDATA%\Pos` en Windows y en `~/.local/share/Pos` en
Linux (ver [docs/carpeta-de-datos.md](docs/carpeta-de-datos.md)). Instalar una versión nueva,
actualizar o desinstalar (con el desinstalador de Windows o `sudo apt remove pos`) no toca esa
carpeta.

### Cambiar el ícono

El ícono es `src/Pos.Desktop/Assets/pos.ico` (ejecutable de Windows, ventanas e instalador).
Linux usa `src/Pos.Desktop/Assets/pos.png`, que se obtiene del frame más grande del `.ico`. Si
cambias `pos.ico`, regenera el PNG con `scripts/make-icons.sh`; conviene que el `.ico` incluya un
frame de 256 px.

### AppId del instalador de Windows

`packaging/windows/Pos.iss` tiene un `AppId` fijo. **No lo cambies**: es lo que permite que una
versión nueva reemplace a la instalada. Con otro `AppId`, Windows trataría la nueva versión como
otra aplicación y quedarían las dos instaladas.

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
