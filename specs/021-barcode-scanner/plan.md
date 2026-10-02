# Implementation Plan: Integración de escáner de código de barras

**Branch**: `021-barcode-scanner` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/021-barcode-scanner/spec.md`

## Summary

Hacer que cualquier lector en modo teclado funcione sin configuración con EAN-13, EAN-8, CODE128 y
CODE39, en el catálogo, en el Punto de venta y en una pantalla de prueba.

1. **Reglas del código en el dominio** (research §1, §2, §4): clase `Barcode` en Domain con la
   normalización (recorte, sin `*…*`, mayúsculas), la regla del catálogo (`[A-Z0-9 .$/+%-]`, 1 a 48)
   y la clasificación de una lectura con el dígito verificador EAN. `Product` delega en ella. Guardar
   en mayúsculas hace la unicidad y la búsqueda insensibles a mayúsculas sin tocar el índice.
2. **Sin cambio de datos** (research §3): la migración `ScannerBarcodeFormats` solo actualiza el
   snapshot (`HasMaxLength` 14 → 48). Se comprobó que EF Core para SQLite no genera SQL. Los códigos
   existentes cumplen la regla nueva.
3. **Lectura en el Punto de venta** (research §5–§9):
   - Con el foco fuera de un campo de texto, la escritura se redirige al campo de captura.
   - `ScanBurstDetector` distingue la ráfaga del lector (≥ 3 caracteres, ≤ 50 ms entre teclas) de la
     escritura manual.
   - Con un diálogo abierto, las ráfagas se ignoran con aviso.
   - El atajo `*` espera 60 ms para no confundirse con el asterisco de CODE39.
4. **Búsqueda** (research §8): `FindProductsForSale` recibe `FromScanner`. Un escaneo no cae a la
   búsqueda por nombre. Sin resultados devuelve el formato para decir "Código no válido" o "Código no
   encontrado", con F2 para la búsqueda manual. El selector de 005 cubre el caso código de barras de
   uno y SKU de otro.
5. **Probar escáner** (research §10): página en "Ayuda" y botón en "Acerca de". Usa el caso de uso de
   solo lectura `InspectScan`, sin permiso de rol. Muestra texto visible, formato, largo, terminador,
   velocidad y producto, con las últimas 10 lecturas en memoria. No toca la venta.

No se agrega ninguna dependencia externa.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia, CommunityToolkit.Mvvm, Hosting, Serilog, EF Core 10
Sqlite, FluentValidation). **No se agrega ninguna.**

**Storage**:

- SQLite, migración `ScannerBarcodeFormats`: solo snapshot (`Products.Barcode` `HasMaxLength(48)`); sin
  SQL ni reconstrucciones.
- `Version` 0.14.0 → 0.15.0 y base de ejemplo `v0.15.0.db` con un producto `PROD-0042`.

**Testing**: xUnit v3, con la política mínima de la constitución v1.2.0 (research §11):

- **Domain**: `BarcodeTests` (regla del catálogo, normalización, dígito verificador EAN-13/EAN-8).
- **Casos de uso sobre SQLite real**:
  - unicidad sin distinguir mayúsculas;
  - búsqueda para vender de un código alfanumérico en minúsculas;
  - un escaneo desconocido no busca por nombre.
- **Migración**: `SampleDatabaseUpgradeTests` con `v0.15.0.db`.
- **Arquitectura**: sin reglas nuevas.
- Sin pruebas de ViewModels, vistas ni del detector.

**Target Platform**: Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Agregar por escaneo en menos de 0.5 s con 10 000 productos (SC-003): igualdad sobre los índices de
  SKU y código de barras.
- Una ráfaga de 20 lecturas sin pérdidas ni mezclas (SC-004): `ScanQueue` con un solo consumidor.

**Constraints**:

- Funciona sin conexión y sin controladores ni configuración del lector (FR-001).
- 0 advertencias.
- La pantalla de prueba no escribe datos (FR-019).
- Un Enter de lector nunca confirma un diálogo del Punto de venta (FR-008).

**Scale/Scope**:

- Catálogo de hasta 10 000 productos; lectores USB o inalámbricos con receptor.
- Casos de uso: 1 nuevo (`InspectScan`); cambian `FindProductsForSale` y las validaciones de
  `CreateProduct`/`UpdateProduct` (a través de `Product` y `ProductRules`).
- Pantallas: Punto de venta (entrada de teclado y avisos), formulario de producto (mensaje), "Acerca de"
  (botón) y la página nueva "Probar escáner".

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó después del diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Todo es local. Las lecturas siguen en `ScanQueue`, que no se detiene ante una lectura fallida. Un Enter del lector ya no puede confirmar un cobro ni pulsar el botón enfocado (research §5, §7). La pantalla de prueba no toca la venta ni el borrador. Los errores pasan por `OperationRunner` (log + mensaje sin detalles técnicos). |
| II. Capas | `Barcode` y `BarcodeFormat` en Domain; `InspectScan` y el cambio de `FindProductsForSale` en Application; `FindByBarcodeAsync` en Infrastructure; el detector y la pantalla solo en Desktop. La carpeta nueva `Application/Scanner/` queda cubierta por las pruebas de arquitectura. |
| III. Lógica en el núcleo | Normalización, regla del catálogo, dígito verificador y clasificación viven en Domain; la decisión de buscar o no por nombre, en el caso de uso. El ViewModel solo elige el texto del aviso según el `Format` devuelto. El detector de ráfagas es lógica de entrada de teclado, propia de la interfaz. |
| IV. Integridad de datos | Sin tablas nuevas. La regla nueva es un superconjunto de la anterior: ningún dato existente queda inválido. La migración no genera SQL (verificado, research §3); se revisa el script y se agrega `v0.15.0.db`. La unicidad sigue en el índice filtrado existente. |
| V. Multiplataforma | **Desviación justificada** (ver Complexity Tracking): el principio pide una interfaz en Application para el lector de códigos y no se define. El lector en modo teclado no necesita adaptador de plataforma: la entrada llega por los eventos de teclado de Avalonia en Windows y Linux, igual en ambos sistemas. |
| VI. Calidad verificable | Solo se prueban la validación de integridad del código (regla, normalización, unicidad) y el cálculo del dígito verificador, más la migración obligatoria, sobre SQLite real. El detector y la UI se validan con [quickstart.md](quickstart.md). |
| VII. Simplicidad | Sin dependencias (se descarta ZXing.Net, research §4). Sin umbrales configurables, sin tipo de código en el producto, sin columnas nuevas. Se reutilizan `ScanQueue`, `Cart.Add`, el selector de productos, F2 y `ShowStatus`. |
| VIII. Soporte | La pantalla de prueba permite diagnosticar en campo la distribución de teclado y el sufijo Enter. Las lecturas no encontradas, no válidas o ignoradas se registran con texto, formato y origen. `docs/escaner.md` nuevo y `docs/ventas.md` actualizado. |
| IX. Seguridad local | `InspectScan` exige sesión iniciada pero no permiso de rol: solo lee nombre, SKU y estado de un producto, datos que cualquier cajero ya ve al vender. No hay operaciones sensibles nuevas ni eventos de bitácora. |

**Resultado**: una desviación justificada (Principio V, ver Complexity Tracking). Decisiones explícitas:

- **Código guardado en mayúsculas (research §2)**: CODE128 distingue mayúsculas, pero la spec pide
  comparar sin distinguirlas. Guardar normalizado evita reconstruir `Products` por un `COLLATE NOCASE`.
- **Sin dígito verificador obligatorio en el catálogo (research §1)**: protege los códigos internos
  existentes. El dígito solo elige el aviso.
- **Escaneo sin búsqueda por nombre (research §8)**: evita agregar un producto equivocado cuando un
  código desconocido es parte de otro.
- **Guardián de diálogos solo en el Punto de venta (research §7)**: en otras pantallas escanear dentro
  de un diálogo debe llenar el campo enfocado.

## Project Structure

### Documentation (this feature)

```text
specs/021-barcode-scanner/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── application-ports.md
│   └── ui.md
├── checklists/
└── tasks.md             # Lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Pos.Domain/
│   └── Products/Barcode.cs  BarcodeFormat.cs  Product.cs        # Product delega en Barcode; 1..48
├── Pos.Application/
│   ├── Products/ProductMessages.cs  IProductRepository.cs       # mensaje nuevo; FindByBarcodeAsync
│   ├── Sales/FindProductsForSale/FindProductsForSaleQuery.cs  FindProductsForSaleHandler.cs
│   ├── Sales/SaleDtos.cs                                        # ProductLookup.Format
│   ├── Scanner/InspectScan/InspectScanQuery.cs  InspectScanHandler.cs  ScanInspectionDto.cs
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── Products/ProductRepository.cs                            # normalización nueva; FindByBarcodeAsync
│   ├── Persistence/Migrations/…_ScannerBarcodeFormats.cs        # solo snapshot
│   └── Persistence/Migrations/PosDbContextModelSnapshot.cs
├── Pos.Desktop/
│   ├── Common/Scanner/ScanBurstDetector.cs  ScanReading.cs  ScanTextFormatter.cs
│   ├── Common/DialogService.cs                                  # guardián opcional de lecturas
│   ├── Sales/PointOfSaleView.axaml.cs                           # redirección, guardián de modales, "*"
│   ├── Sales/PointOfSaleViewModel.cs  ScanQueue.cs              # ScanInput(Text, IsScan); avisos
│   ├── About/AboutModule.cs  AboutView.axaml  AboutViewModel.cs # página y botón "Probar escáner"
│   ├── About/ScannerTestView.axaml(.cs)  ScannerTestViewModel.cs
│   └── Resources/Strings.resx                                   # Scan_*, ScannerTest_*, Nav_ScannerTest
tests/
├── Pos.Domain.Tests/Products/BarcodeTests.cs
├── Pos.Application.Tests/TestSupport/InMemoryProductRepository.cs   # FindByBarcodeAsync
├── Pos.Infrastructure.Tests/Products/BarcodeUseCaseTests.cs         # unicidad, búsqueda, escaneo sin nombre
└── Pos.Infrastructure.Tests/SampleDatabases/v0.15.0.db  SampleData (producto PROD-0042)
docs/
├── escaner.md                   # nuevo: lector, sufijo Enter, distribución de teclado, formatos, prueba
├── ventas.md                    # lectura sin foco, avisos, atajo "*"
└── migraciones.md               # sección 0.15.0 (sin SQL)
Directory.Build.props            # Version 0.15.0
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de 001 a 020.

- Las reglas del código viven junto a `Product` en `Domain/Products`.
- `InspectScan` va en una carpeta propia `Application/Scanner/` porque no pertenece a Productos ni a
  Ventas: es diagnóstico de un dispositivo, sin permiso de rol.
- En Desktop, el detector es compartido (`Common/Scanner`) entre el Punto de venta y la pantalla de
  prueba. La pantalla vive en `About/`, porque cuelga del grupo "Ayuda" registrado en `AboutModule`.
- Las pruebas existentes que fijan el mensaje o el largo del código de barras
  (`CreateProductValidatorTests`, `ProductTests`) se ajustan a la regla nueva.

## Complexity Tracking

| Desviación | Por qué se necesita | Alternativa más simple descartada y por qué |
|---|---|---|
| Principio V: el lector de códigos no se define con una interfaz en Application ni con implementaciones por sistema operativo en Infrastructure. | El lector en modo teclado no expone un dispositivo que controlar: el sistema operativo lo presenta como un teclado y Avalonia entrega sus caracteres con los mismos eventos en Windows y Linux. Distinguir la lectura de la escritura manual (`ScanBurstDetector`) depende del foco y de los controles, así que es lógica de la interfaz y vive en Desktop. | Una interfaz `IBarcodeScanner` en Application con adaptadores por sistema operativo: no tendría nada que abstraer y obligaría a sacar eventos de teclado de la interfaz (Principio VII). Se reconsiderará si se soportan lectores en modo serie o con controlador propio, que hoy quedan fuera de alcance (spec, Assumptions). |
