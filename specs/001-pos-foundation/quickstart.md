# Quickstart: validar la Fundación del POS

Guía para comprobar de punta a punta que la funcionalidad cumple [spec.md](spec.md). Los detalles
de comportamiento están en [contracts/](contracts/) y en [data-model.md](data-model.md).

## Prerrequisitos

- .NET SDK 10.0.1xx (la versión exacta está fijada en `global.json`).
- **Linux**: `libicu` instalado (formato de moneda es-MX) y un entorno gráfico X11 o Wayland con
  XWayland.
- **Windows**: Windows 10 o posterior.

## 1. Compilar y probar desde la raíz (SC-001, SC-002, SC-004 a SC-007)

```bash
dotnet tool restore
dotnet build
dotnet test
```

**Resultado esperado**: 0 errores y 0 advertencias. Todas las pruebas pasan, incluidas estas
suites:

| Suite | Qué demuestra |
|---|---|
| `Pos.ArchitectureTests` | Reglas de dependencia; ni vistas ni ViewModels acceden a la base (SC-002) |
| `Pos.Domain.Tests` | Reglas de Product y Money con sus casos límite (SC-004) |
| `Pos.Application.Tests` | Casos de uso y orden de la secuencia de arranque |
| `Pos.Infrastructure.Tests` | Repositorio contra SQLite real, concurrencia (SC-005), arranque (SC-006), bases de ejemplo (SC-007), rendimiento con 10,000 productos (SC-011) |
| `Pos.Desktop.Tests` | ViewModels: doble clic, errores por campo, errores inesperados (SC-008) |

Repite los mismos comandos en el otro sistema operativo, o revisa que la CI esté en verde en
ambos.

## 2. Ejecutar con una carpeta de datos aislada

```bash
# Linux
POS_DATA_DIR=/tmp/pos-qa dotnet run --project src/Pos.Desktop
# Windows (PowerShell)
$env:POS_DATA_DIR="$env:TEMP\pos-qa"; dotnet run --project src/Pos.Desktop
```

**Resultado esperado**: se crea `data/pos.db` y la ventana principal abre en la pantalla
Productos en menos de 5 s (SC-012).

## 3. Escenarios manuales

Hazlos con la red desconectada (SC-003).

| # | Pasos | Resultado esperado |
|---|---|---|
| 1 | Crea "Café Molido", SKU `caf-001`, precio `89.5` | Aparece en la lista con SKU `CAF-001` y precio `$89.50` |
| 2 | Busca `cafe molido` | Aparece "Café Molido" |
| 3 | Crea otro producto con SKU `CAF-001` | Mensaje "Ya existe un producto con este SKU." junto al campo SKU; lo capturado se conserva |
| 4 | Precio `1,234.50`, luego `12.345` | Ambos se rechazan con un mensaje de formato; nada se redondea |
| 5 | Haz doble clic rápido en Guardar con datos válidos | Se registra un solo producto |
| 6 | Crea productos con los códigos de barras `7501234567890` y `7501234567891`; busca `7501234567890` y luego `7501` | Primero aparece uno solo; después aparecen ambos |
| 7 | Edita un producto, desmárcalo como Activo y guarda | Desaparece de la lista; reaparece con "Mostrar inactivos" |
| 8 | Borra un producto y confirma; crea otro con el mismo SKU | El primero desaparece; el segundo se acepta |
| 9 | Con la aplicación abierta, ejecútala otra vez | No abre una segunda ventana; la existente pasa al frente |
| 10 | En Acerca de, exporta el diagnóstico | Se crea el zip; contiene `info.json`, `logs/` y `pos.db`, que se puede abrir con `sqlite3 pos.db "PRAGMA quick_check;"` y responde `ok` |

## 4. Escenarios de arranque

Los cubren las pruebas automáticas. Esta sección es para una verificación manual opcional.

| Escenario | Cómo provocarlo | Resultado esperado |
|---|---|---|
| Base más nueva | `sqlite3 data/pos.db "INSERT INTO __EFMigrationsHistory VALUES ('99990101000000_Future','10.0.0');"` y abrir | Mensaje de versión más reciente; la base no cambia |
| Base dañada con respaldo | Cerrar, sobrescribir los primeros 100 bytes de `pos.db` y abrir | Se ofrece restaurar el respaldo, con su fecha; al aceptar abre con los datos del respaldo y la base dañada queda en `backups/corrupt/` |
| Sin permisos | `chmod -w` en la carpeta `data/` (Linux) y abrir | Mensaje de permisos con la ruta; la aplicación no se cierra abruptamente |

## 5. Documentación para desarrolladores (SC-010)

Un desarrollador que no conoce el proyecto sigue `README.md` y `docs/` sin otra ayuda y consigue
compilar, ejecutar, probar y crear y revisar una migración. Revisa que existan:

- `README.md`: prerrequisitos, compilar, ejecutar y probar en Windows y Linux.
- `docs/carpeta-de-datos.md`
- `docs/migraciones.md`: crear una migración, revisar el SQL y generar la base de ejemplo.
- `docs/agregar-funcionalidad.md`: guía paso a paso usando Productos como ejemplo.
