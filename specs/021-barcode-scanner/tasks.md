---

description: "Lista de tareas para implementar la integración de escáner de código de barras"
---

# Tasks: Integración de escáner de código de barras

**Input**: documentos de diseño en `specs/021-barcode-scanner/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/application-ports.md, contracts/ui.md, quickstart.md

**Tests**: el plan pide pruebas mínimas (constitución v1.2.0, Principio VI): `BarcodeTests` (Domain), casos de uso sobre SQLite real (`BarcodeUseCaseTests`) y la migración de la base de ejemplo `v0.15.0.db`. **Sin** pruebas de ViewModels, vistas ni del detector de ráfagas. Al implementar se ejecutan solo las pruebas del proyecto modificado (`dotnet test tests/<Proyecto> --verbosity quiet`).

**Organization**: las tareas se agrupan por historia de usuario. Las tres historias son P3 y se ordenan por dependencia (US1 → US2 → US3), según la spec.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia a la que pertenece (US1, US2, US3)

## Path Conventions

Solución por capas: `src/Pos.Domain`, `src/Pos.Application`, `src/Pos.Infrastructure`, `src/Pos.Desktop`; pruebas en `tests/Pos.*.Tests`; documentación en `docs/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: versión de la aplicación para la migración y la base de ejemplo nuevas

- [X] T001 Cambiar `<Version>` de `0.14.0` a `0.15.0` en Directory.Build.props

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: reglas del código de barras en el dominio y detector de ráfagas compartido por el Punto de venta y la pantalla de prueba

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase

- [X] T002 [P] Crear el enum `BarcodeFormat { Empty, Ean13, Ean8, Code128OrCode39, Unrecognized }` en src/Pos.Domain/Products/BarcodeFormat.cs, con comentario XML por valor según data-model.md ("Empty: lectura vacía después de normalizar"; "Ean13: 13 dígitos con dígito verificador correcto"; "Ean8: 8 dígitos con dígito verificador correcto"; "Code128OrCode39: cumple el juego de caracteres y el largo, y no es un EAN con dígito incorrecto"; "Unrecognized: caracteres no admitidos, más de 48, o 8/13 dígitos con dígito verificador incorrecto")
- [X] T003 Crear la clase estática `Barcode` en src/Pos.Domain/Products/Barcode.cs (contracts/application-ports.md, research §1, §2, §4):
  - `public const int MaxLength = 48;`
  - `string? Normalize(string? raw)`: recorta extremos; si empieza y termina con `*` y tiene más de dos caracteres, quita ambos asteriscos y vuelve a recortar; `ToUpperInvariant`; vacío → `null`
  - `bool IsValidForCatalog(string? normalized)`: `null` es válido (campo opcional); si no, debe cumplir `^[A-Z0-9 .$/+%-]{1,48}$` (regex generada con `[GeneratedRegex]`, solo ASCII; el recorte ya quitó espacios de los extremos)
  - `bool HasValidEanCheckDigit(string digits)`: módulo 10 con pesos alternos 3, 1, 3, … de derecha a izquierda sin contar el dígito verificador; válido solo para 8 o 13 dígitos ASCII
  - `BarcodeFormat Classify(string? raw)`: normaliza y aplica en orden: vacío → `Empty`; no cumple el juego de caracteres o más de 48 → `Unrecognized`; 13 dígitos → `Ean13` si el dígito es correcto, si no `Unrecognized`; 8 dígitos → `Ean8` igual; resto → `Code128OrCode39`
  (depende de T002)
- [X] T004 [P] Crear `BarcodeTests` en tests/Pos.Domain.Tests/Products/BarcodeTests.cs con una prueba por regla (caso válido + caso límite más importante): regla del catálogo (`PROD-0042` válido, `ABC_123` inválido, 49 caracteres inválido, 48 válido); normalización (`" *abc-1* "` → `"ABC-1"`, `"   "` → `null`, `"*"` sin quitar); dígito verificador EAN-13 (`7501055300846` válido, `7501055300847` inválido) y EAN-8 (`96385074` válido, `96385075` inválido); `Classify` de cada valor del enum (depende de T003)
- [X] T005 [P] Crear `ScanReading` y `Terminator` en src/Pos.Desktop/Common/Scanner/ScanReading.cs: `enum Terminator { Enter, Tab, None }` y `sealed record ScanReading(string RawText, Terminator Terminator, bool IsBurst, TextBoxSnapshot? FocusedSnapshot)` con `record TextBoxSnapshot(string Text, int CaretIndex)` (data-model.md "Lectura de escáner"; `RawText` sin el terminador)
- [X] T006 Crear `ScanBurstDetector` en src/Pos.Desktop/Common/Scanner/ScanBurstDetector.cs (research §6, contracts/ui.md): clase pura sin dependencias de Avalonia con las constantes `MaxGap = 50 ms`, `MinBurstLength = 3`, `IdleEnd = 300 ms`, `StarDeferral = 60 ms`; métodos `OnText(string text, long timestamp, TextBoxSnapshot? focused)` (guarda la instantánea solo en el primer carácter de la secuencia), `OnTerminator(Terminator terminator, long timestamp)` → `ScanReading?` y `CheckIdle(long timestamp)` → `ScanReading?` con `Terminator.None` tras 300 ms sin teclas; marcas con `Stopwatch.GetTimestamp`; `IsBurst` = al menos 3 caracteres y ningún intervalo (incluido el terminador) mayor a 50 ms; `Reset()`. Sin prueba unitaria (política mínima) (depende de T005)

**Checkpoint**: `dotnet build -v q` sin advertencias y `dotnet test tests/Pos.Domain.Tests --verbosity quiet` en verde

---

## Phase 3: User Story 1 - Soporte de formatos (Priority: P3, primera) 🎯 MVP

**Goal**: el catálogo admite códigos alfanuméricos de CODE128/CODE39 (normalizados en mayúsculas) y el Punto de venta reconoce una lectura del escáner aunque el foco no esté en el campo de captura, sin confundirla con la escritura en otros campos ni con un diálogo abierto.

**Independent Test**: quickstart.md escenarios 1–6, 13 y 14: dar de alta productos con `7501055300846`, `96385074`, `ABC-12345` y `PROD-0042`; escanear cada uno con el foco en la captura y luego en la lista de líneas; cada lectura agrega el producto correcto; F4 + `3` + Enter cambia la cantidad sin buscar.

### Tests for User Story 1

- [X] T007 [P] [US1] Ajustar las pruebas existentes a la regla nueva en tests/Pos.Domain.Tests/Products/ProductTests.cs: renombrar `Create_CodigoDeBarrasDe8a14Digitos_EsValido` para cubrir también `PROD-0042` y `abc-12345` (se guarda `ABC-12345`); en `Create_CodigoDeBarrasInvalido_Lanza` dejar solo casos que siguen siendo inválidos (`١٢٣٤٥٦٧٨`, `ABC_123`, 49 caracteres `new string('A', 49)` vía `MemberData` o `[InlineData]` literal)
- [X] T008 [P] [US1] Ajustar tests/Pos.Application.Tests/Products/CreateProductValidatorTests.cs: `CodigoDeBarrasInvalido_SeRechaza` con `ABC_123`, `ABC'123` y 49 caracteres (los anteriores `75012345678AB`, `1234567`, `123456789012345` ahora son válidos)
- [X] T009 [P] [US1] Ajustar tests/Pos.Desktop.Tests/Products/ProductEditorViewModelCreateTests.cs: en la prueba que espera `ProductMessages.BarcodeFormat`, usar `barcode: "AB_12"` (el `"12"` actual ahora es válido) y actualizar la tupla esperada de la última aserción
- [X] T010 [US1] Crear tests/Pos.Infrastructure.Tests/Products/BarcodeUseCaseTests.cs (SQLite real, siguiendo el patrón de las pruebas vecinas en tests/Pos.Infrastructure.Tests/Products/): prueba de unicidad sin distinguir mayúsculas: crear un producto con código `abc-1` vía `CreateProductHandler` (se guarda `ABC-1`) y otro con `ABC-1` → error de código de barras duplicado (`ProductFields.Barcode`)

### Implementation for User Story 1

- [X] T011 [US1] En src/Pos.Domain/Products/Product.cs: `BarcodeMinLength = 1`, `BarcodeMaxLength = Barcode.MaxLength`; `NormalizeBarcode` delega en `Barcode.Normalize` e `IsValidBarcode` en `Barcode.IsValidForCatalog` (misma firma pública); el mensaje de `DomainException` pasa a "El código de barras admite hasta 48 letras, dígitos, espacios interiores y los símbolos - . $ / + %."; `LooksLikeFullBarcode` **conserva** el patrón numérico `^[0-9]{8,14}$` (renombrar el regex a `FullNumericBarcodePattern` para que no se confunda con la regla del catálogo)
- [X] T012 [P] [US1] En src/Pos.Application/Products/ProductMessages.cs cambiar `BarcodeFormat` a "El código de barras admite hasta 48 letras, dígitos, espacios interiores y los símbolos - . $ / + %." y revisar que src/Pos.Application/Products/ProductRules.cs use `Product.IsValidBarcode(Product.NormalizeBarcode(x))` (sin reglas de largo propias)
- [X] T013 [P] [US1] En src/Pos.Infrastructure/Products/ProductRepository.cs normalizar con `Barcode.Normalize` el código recibido en `FindForSaleAsync` y `ExistsWithCodeAsync` (mayúsculas, sin `*…*`) para comparar por igualdad sobre `IX_Products_Barcode`; `BarcodeExistsAsync` sin cambios (recibe el código ya normalizado)
- [X] T014 [US1] Generar la migración con `dotnet ef migrations add ScannerBarcodeFormats -p src/Pos.Infrastructure -s src/Pos.Infrastructure` (src/Pos.Infrastructure/Persistence/Configurations/ProductConfiguration.cs ya usa `HasMaxLength(Product.BarcodeMaxLength)`, que ahora es 48); verificar que `Up`/`Down` en src/Pos.Infrastructure/Persistence/Migrations/…_ScannerBarcodeFormats.cs queden vacíos y que solo cambie `HasMaxLength(48)` en PosDbContextModelSnapshot.cs; revisar con `dotnet ef migrations script SuppliersAndPurchases ScannerBarcodeFormats -p src/Pos.Infrastructure -s src/Pos.Infrastructure` que no haya SQL ni reconstrucción de `Products` (depende de T011)
- [X] T015 [US1] En tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseGenerator.cs agregar, con el comentario "Desde 0.15.0", un producto activo cuyo código de barras es `PROD-0042`; generar `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.15.0.db` con `POS_GENERATE_SAMPLE_DB=1 dotnet test tests/Pos.Infrastructure.Tests --filter GenerarBaseDeEjemploDeLaVersionActual` según docs/migraciones.md; en SampleDatabaseUpgradeTests.cs verificar que, al migrar `v0.15.0.db`, el producto conserva `PROD-0042` y que los códigos numéricos de bases anteriores no cambian (depende de T001, T014)
- [X] T016 [US1] En src/Pos.Desktop/Sales/ScanQueue.cs cambiar el elemento encolado a `sealed record ScanInput(string Text, bool IsScan)` y actualizar sus usos en src/Pos.Desktop/Sales/PointOfSaleViewModel.cs: `Capture` recibe el origen (`IsScan`) que le pasa la vista y lo encola (por ahora el consumidor sigue llamando a `FindProductsForSale` igual; US2 usa el origen)
- [X] T017 [US1] En src/Pos.Desktop/Sales/PointOfSaleView.axaml.cs (research §5, contracts/ui.md "Punto de venta"): registrar `TextInputEvent` en fase de túnel junto al `KeyDown` existente y alimentar un `ScanBurstDetector` propio de la vista con cada carácter y con Enter/Tab; sin modal abierto (`IsModalOpen == false`) y con el foco fuera de un `TextBox`, mover el foco al campo de captura de forma síncrona, agregar el texto al final, colocar el cursor al final y marcar el evento como atendido; con el foco en otro `TextBox` (cantidad, efectivo, cliente, cupón) no interceptar nada (FR-007); el Enter con el foco en la captura ejecuta `Capture` con `IsScan` del detector; un Enter sin texto previo conserva su efecto normal
- [X] T018 [US1] En src/Pos.Desktop/Sales/PointOfSaleView.axaml.cs implementar el guardián de diálogos (research §7, FR-008): con `IsModalOpen == true` seguir alimentando el detector sin interceptar caracteres; al recibir un Enter que cierra una ráfaga (`IsBurst`), marcar el Enter como atendido, restaurar en el `TextBox` enfocado el texto y la posición del cursor de `FocusedSnapshot`, llamar a un método del ViewModel que muestra `Scan_IgnoredInDialog` con `ShowStatus(warning: true)` y registra en el log nivel `Information` "Lectura ignorada por diálogo abierto"
- [X] T019 [US1] En src/Pos.Desktop/Common/IDialogService.cs y src/Pos.Desktop/Common/DialogService.cs agregar un guardián opcional de lecturas para los diálogos en ventana propia (mensajes, confirmaciones y autorización del Administrador) abiertos desde el Punto de venta (research §7, FR-008):
  - `IDialogService` expone una propiedad `Action? ScanIgnored` (o equivalente) que el Punto de venta asigna al activarse y limpia al desactivarse; con `null`, el resto de la aplicación no aplica el guardián (en el formulario de producto, escanear llena el campo)
  - al abrir una `DialogWindow` con el guardián asignado, `DialogService` crea un `ScanBurstDetector` **propio de esa ventana** (los manejadores de la vista del Punto de venta no reciben el teclado de otra ventana) y registra en túnel `TextInputEvent` y `KeyDown` sobre la ventana, sin interceptar caracteres
  - al recibir un Enter que cierra una ráfaga (`IsBurst`): marcar el Enter como atendido, restaurar en el `TextBox` enfocado de la ventana el texto y la posición del cursor de `FocusedSnapshot` (si lo hay) e invocar `ScanIgnored`
  - conectar desde src/Pos.Desktop/Sales/PointOfSaleViewModel.cs: `ScanIgnored` llama al mismo método de T018 que muestra `Scan_IgnoredInDialog` con `ShowStatus(warning: true)` y registra en el log "Lectura ignorada por diálogo abierto"
  (depende de T006, T018)
- [X] T020 [US1] En src/Pos.Desktop/Sales/PointOfSaleView.axaml.cs cambiar el atajo `*` (o Shift+8) con el campo de captura vacío (research §9): escribir el `*` como texto y esperar `ScanBurstDetector.StarDeferral` (60 ms, `DispatcherTimer` de una sola vez); si llega otro carácter, es parte de una lectura y se deja; si no, quitar el `*` y abrir la cantidad como hoy
- [X] T021 [P] [US1] Agregar a src/Pos.Desktop/Resources/Strings.resx la cadena `Scan_IgnoredInDialog` = "Lectura ignorada: cierre la ventana para escanear"

**Checkpoint**: US1 funcional: el catálogo acepta los cuatro formatos y el Punto de venta reconoce escaneos con el foco en cualquier control. `dotnet test` de Domain, Application, Desktop e Infrastructure en verde.

---

## Phase 4: User Story 2 - Búsqueda por código (Priority: P3, segunda)

**Goal**: un escaneo busca solo por código exacto (y cupón), sin caer a la búsqueda por nombre; sin resultados se distingue "Código no válido" de "Código no encontrado" y se ofrece F2 para la búsqueda manual.

**Independent Test**: quickstart.md escenarios 7–12: escanear un código existente dos veces (una línea, cantidad 2), `7501055300847` ("Código no válido"), `ZZZ-999` ("Código no encontrado" + F2), un código que es código de barras de A y SKU de B (selector), un inactivo (mensaje de 005) y una ráfaga de 20 lecturas (cantidad 20).

### Tests for User Story 2

- [X] T022 [US2] En tests/Pos.Infrastructure.Tests/Products/BarcodeUseCaseTests.cs agregar (SQLite real, con `FindProductsForSaleHandler`): (a) un producto con código `ABC-12345` se encuentra como `ExactMatch` con `new FindProductsForSaleQuery("abc-12345", FromScanner: true)`; (b) con un producto llamado "Leche ZZZ-9990 entera", `new FindProductsForSaleQuery("ZZZ-999", FromScanner: true)` devuelve `LookupKind.None` con `Format == BarcodeFormat.Code128OrCode39` y sin productos (el escaneo no busca por nombre) (depende de T010)

### Implementation for User Story 2

- [X] T023 [P] [US2] En src/Pos.Application/Sales/FindProductsForSale/FindProductsForSaleQuery.cs cambiar a `public sealed record FindProductsForSaleQuery(string Text, bool FromScanner = false);`
- [X] T024 [P] [US2] En src/Pos.Application/Sales/SaleDtos.cs agregar a `ProductLookup` el parámetro opcional final `BarcodeFormat? Format = null` (solo se llena con `Kind == LookupKind.None`)
- [X] T025 [US2] En src/Pos.Application/Sales/FindProductsForSale/FindProductsForSaleHandler.cs (research §8, contracts/application-ports.md): mantener el orden coincidencia exacta de SKU/código (incluye inactivos y borrados con `NotSellableReason`; más de una → `NameMatches`) → cupón (015); hacer la búsqueda por nombre **solo** si `!query.FromScanner`; sin resultados devolver `LookupKind.None` con `Format = Barcode.Classify(query.Text)`; permiso `Sell` sin cambios (depende de T023, T024)
- [X] T026 [US2] En src/Pos.Desktop/Sales/PointOfSaleViewModel.cs: el consumidor de `ScanQueue` llama `FindProductsForSaleQuery(input.Text, input.IsScan)`; con `LookupKind.None` reemplazar `Sale_ProductNotFound` por `Scan_CodeInvalid` (si `Format == BarcodeFormat.Unrecognized`) o `Scan_CodeNotFound` (cualquier otro), con el texto normalizado (`Barcode.Normalize`), vía `ShowStatus(warning: true)` para que se reemplace con la siguiente lectura o acción (FR-014); con `Format == BarcodeFormat.Empty` no mostrar aviso; registrar con nivel `Information` el texto, el formato y si fue escaneo (research §11); verificar que F2 con el campo vacío enfoca la captura para escribir nombre o SKU (FR-012) (depende de T016, T025)
- [X] T027 [P] [US2] En src/Pos.Desktop/Resources/Strings.resx eliminar `Sale_ProductNotFound` y agregar `Scan_CodeNotFound` = "Código no encontrado: {0}. F2: buscar por nombre o SKU" y `Scan_CodeInvalid` = "Código no válido: {0}. F2: buscar por nombre o SKU"; quitar cualquier otra referencia a `Sale_ProductNotFound` en src/ y tests/

**Checkpoint**: US1 y US2 funcionan juntas; `dotnet test tests/Pos.Application.Tests` y `tests/Pos.Infrastructure.Tests` en verde.

---

## Phase 5: User Story 3 - Calibración: probar el escáner (Priority: P3, tercera)

**Goal**: página "Ayuda > Probar escáner" (y botón en "Acerca de") que muestra lo leído, formato, largo, terminador, velocidad y producto, con las últimas 10 lecturas, sin tocar la venta.

**Independent Test**: quickstart.md escenarios 15–19: con una venta de 2 líneas, abrir "Probar escáner", escanear un EAN-13 existente, `ZZZ-999` y una lectura con un carácter alterado; enviar `ABC` sin Enter; hacer 12 lecturas y "Borrar"; volver al Punto de venta y ver las mismas 2 líneas; como Cajero, ver y usar la opción.

### Implementation for User Story 3

- [X] T028 [P] [US3] En src/Pos.Application/Products/IProductRepository.cs agregar `Task<Product?> FindByBarcodeAsync(string normalizedBarcode, CancellationToken cancellationToken);` (producto no borrado con ese código, activo o inactivo)
- [X] T029 [US3] Implementar `FindByBarcodeAsync` en src/Pos.Infrastructure/Products/ProductRepository.cs con igualdad sobre `Barcode` y `DeletedAt == null` (usa `IX_Products_Barcode`), `AsNoTracking` (depende de T028)
- [X] T030 [P] [US3] Implementar `FindByBarcodeAsync` en tests/Pos.Application.Tests/TestSupport/InMemoryProductRepository.cs (depende de T028)
- [X] T031 [P] [US3] Crear src/Pos.Application/Scanner/InspectScan/InspectScanQuery.cs con `public sealed record InspectScanQuery(string RawText);`
- [X] T032 [P] [US3] Crear src/Pos.Application/Scanner/InspectScan/ScanInspectionDto.cs con `public sealed record ScanInspectionDto(string NormalizedText, BarcodeFormat Format, int Length, ScanProductDto? Product);` (`NormalizedText` vacío si la lectura está vacía; `Length` = caracteres de `RawText` sin terminador) y `public sealed record ScanProductDto(string Name, string Sku, bool IsActive);`
- [X] T033 [US3] Crear `InspectScanHandler` en src/Pos.Application/Scanner/InspectScan/InspectScanHandler.cs: `Task<Result<ScanInspectionDto>> HandleAsync(InspectScanQuery query, CancellationToken cancellationToken)`; sin sesión iniciada (`ICurrentUser` no autenticado) → `Forbidden`; no pide permiso de rol; `Format = Barcode.Classify(RawText)`; busca con `FindByBarcodeAsync(Barcode.Normalize(RawText))` si el formato no es `Empty` ni `Unrecognized` o si el normalizado cumple `Barcode.IsValidForCatalog`; no escribe datos ni bitácora (FR-019) (depende de T028, T031, T032)
- [X] T034 [US3] Registrar `InspectScanHandler` como `AddScoped` en src/Pos.Application/DependencyInjection.cs junto a los demás casos de uso (depende de T033)
- [X] T035 [P] [US3] Crear `ScanTextFormatter` en src/Pos.Desktop/Common/Scanner/ScanTextFormatter.cs: método estático `string ToVisible(string raw)` que muestra espacios como `·`, tabulador como `[TAB]` y cualquier carácter de control o fuera de ASCII imprimible como `[U+XXXX]` (contracts/ui.md, ejemplos `PROD·0042`, `750[U+00A0]1`)
- [X] T036 [P] [US3] Agregar a src/Pos.Desktop/Resources/Strings.resx: `Nav_ScannerTest` = "Probar escáner"; `ScannerTest_Instructions` = "Escanee un código. Esta pantalla no modifica la venta."; `ScannerTest_Text`, `ScannerTest_Format`, `ScannerTest_Length` = "Caracteres", `ScannerTest_Terminator` = "Terminó con", `ScannerTest_Speed` = "Velocidad", `ScannerTest_Product`; `ScannerTest_FormatEan13` = "EAN-13", `ScannerTest_FormatEan8` = "EAN-8", `ScannerTest_FormatCode` = "CODE128 / CODE39", `ScannerTest_FormatUnrecognized` = "Formato no reconocido"; `ScannerTest_EndEnter` = "Enter", `ScannerTest_EndTab` = "Tab", `ScannerTest_EndNone` = "Sin Enter", `ScannerTest_ConfigureEnter` = "Configure el lector para enviar Enter al final de cada lectura"; `ScannerTest_SpeedScanner` = "Escáner", `ScannerTest_SpeedManual` = "Escritura manual"; `ScannerTest_Inactive` = "(inactivo)", `ScannerTest_NoProduct` = "Sin producto con este código"; `ScannerTest_History` = "Últimas lecturas", `ScannerTest_Clear` = "Borrar"
- [X] T037 [US3] Crear `ScannerTestViewModel` en src/Pos.Desktop/About/ScannerTestViewModel.cs (hereda de `PageViewModel` como las demás páginas): recibe `ScanReading`, llama `InspectScanHandler` vía `UseCases`/`OperationRunner`, arma un elemento con hora local, texto visible (`ScanTextFormatter`), formato, largo, terminador (con la recomendación si no es Enter, FR-017), velocidad (`IsBurst`) y producto (nombre · SKU, "(inactivo)" o "Sin producto con este código"); `Current` (la más reciente) y `ObservableCollection` `History` de máximo 10 elementos, insertando al inicio (FR-018); comando `ClearCommand`; no referencia `PointOfSaleViewModel`, `Cart` ni el borrador (FR-019) (depende de T033, T035, T036)
- [X] T038 [US3] Crear src/Pos.Desktop/About/ScannerTestView.axaml y ScannerTestView.axaml.cs: encabezado con instrucciones, bloque destacado de la lectura actual (tabla de contracts/ui.md) e historial compacto con "Borrar"; el code-behind registra `TextInputEvent` y `KeyDown` en túnel con su propio `ScanBurstDetector`, trata Enter y Tab como terminadores (Tab no mueve el foco mientras forma parte de una lectura), y un `DispatcherTimer` que llama `CheckIdle` para cerrar lecturas sin terminador a los 300 ms; Esc y la navegación normal salen (depende de T006, T037)
- [X] T039 [US3] En src/Pos.Desktop/About/AboutModule.cs agregar `public const string ScannerTestPageId = "help.scanner-test";` y registrar `services.AddPage<ScannerTestViewModel, ScannerTestView>(ScannerTestPageId, Strings.Nav_ScannerTest, "<icono existente adecuado de Icons.axaml>", 10, GroupId);` sin permiso (visible para todos los roles) (depende de T038)
- [X] T040 [US3] En src/Pos.Desktop/About/AboutViewModel.cs y AboutView.axaml agregar el botón "Probar escáner" (`Strings.Nav_ScannerTest`) que navega a `AboutModule.ScannerTestPageId` con el servicio de navegación existente (FR-015) (depende de T039)

**Checkpoint**: las tres historias funcionan de forma independiente; la venta en curso no cambia tras usar la pantalla de prueba.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: documentación de soporte y validación final

- [X] T041 [P] Crear docs/escaner.md (guía de soporte): requisitos del lector (modo teclado, USB o inalámbrico), sufijo Enter, distribución de teclado (síntomas y cómo verlo en la pantalla de prueba), formatos admitidos y reglas del catálogo (1 a 48 caracteres `A-Z 0-9 espacio - . $ / + %`, mayúsculas, asteriscos de CODE39), uso de "Ayuda > Probar escáner" y diferencia entre "Código no válido" y "Código no encontrado"
- [X] T042 [P] Actualizar docs/ventas.md: lectura del escáner con el foco fuera de la captura, avisos "Código no válido"/"Código no encontrado" con F2, lecturas ignoradas con un diálogo abierto y el atajo `*` con espera de 60 ms
- [X] T043 [P] Agregar a docs/migraciones.md la sección "## 0.15.0: formatos del escáner (`ScannerBarcodeFormats`)": solo actualiza el snapshot (`HasMaxLength` 14 → 48), sin SQL ni reconstrucciones; la base `v0.15.0.db` agrega un producto con código `PROD-0042`
- [X] T044 Ejecutar `dotnet build -v q` (0 errores, 0 advertencias) y `dotnet test --verbosity quiet` (incluye `Pos.ArchitectureTests`, que deben cubrir la carpeta nueva `Application/Scanner/` sin reglas nuevas)
- [ ] T045 Validar manualmente los escenarios 1–19 y la meta de rendimiento (SC-003) de specs/021-barcode-scanner/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: después de Setup; bloquea todas las historias
- **US1 (Phase 3)**: después de Foundational
- **US2 (Phase 4)**: después de Foundational; T026 depende de T016 (US1, `ScanInput`) y T022 comparte archivo con T010
- **US3 (Phase 5)**: después de Foundational; independiente de US1 y US2 (usa `Barcode` y `ScanBurstDetector`)
- **Polish (Phase 6)**: después de las historias que se quieran entregar

### User Story Dependencies

- **US1**: base de la funcionalidad (catálogo y reconocimiento)
- **US2**: usa el origen `IsScan` de US1 (T016); la parte de Application (T023–T025) puede hacerse en paralelo con US1
- **US3**: solo depende de Foundational

### Within Each User Story

- Ajuste de pruebas existentes y pruebas nuevas junto con la implementación de la misma regla
- Domain → Application → Infrastructure → Desktop
- Migración (T014) antes de la base de ejemplo (T015)

### Parallel Opportunities

- T002 y T005 (Domain y Desktop); T004 en paralelo con T005/T006
- US1: T007, T008, T009, T012, T013, T021 en paralelo
- US2: T023, T024, T027 en paralelo
- US3: T028, T031, T032, T035, T036 en paralelo; T029 y T030 en paralelo
- US3 completa en paralelo con US1/US2
- Polish: T041, T042, T043 en paralelo

---

## Parallel Example: User Story 1

```bash
Task: "Ajustar ProductTests en tests/Pos.Domain.Tests/Products/ProductTests.cs"
Task: "Ajustar CreateProductValidatorTests en tests/Pos.Application.Tests/Products/CreateProductValidatorTests.cs"
Task: "Ajustar ProductEditorViewModelCreateTests en tests/Pos.Desktop.Tests/Products/ProductEditorViewModelCreateTests.cs"
Task: "Cambiar ProductMessages.BarcodeFormat en src/Pos.Application/Products/ProductMessages.cs"
Task: "Normalizar con Barcode.Normalize en src/Pos.Infrastructure/Products/ProductRepository.cs"
```

## Parallel Example: User Story 3

```bash
Task: "FindByBarcodeAsync en src/Pos.Application/Products/IProductRepository.cs"
Task: "InspectScanQuery en src/Pos.Application/Scanner/InspectScan/InspectScanQuery.cs"
Task: "ScanInspectionDto en src/Pos.Application/Scanner/InspectScan/ScanInspectionDto.cs"
Task: "ScanTextFormatter en src/Pos.Desktop/Common/Scanner/ScanTextFormatter.cs"
Task: "Cadenas ScannerTest_* en src/Pos.Desktop/Resources/Strings.resx"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 (versión) y Phase 2 (`Barcode`, `BarcodeFormat`, detector)
2. Phase 3 (US1): catálogo alfanumérico, migración, base de ejemplo y reconocimiento en el Punto de venta
3. **Validar**: quickstart.md escenarios 1–6, 13 y 14

### Incremental Delivery

1. Setup + Foundational
2. US1 → validar → entregable (los códigos CODE128/CODE39 ya se escanean)
3. US2 → validar escenarios 7–12 (avisos y búsqueda solo por código)
4. US3 → validar escenarios 15–19 (pantalla de prueba)
5. Polish → documentación, build y suite completa

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes
- `Strings.resx` lo tocan T021, T027 y T036: si se hacen en paralelo, combinar con cuidado
- Al implementar, ejecutar solo las pruebas del proyecto modificado; la suite completa al final (T044)
- La migración publicada nunca se modifica; la base `v0.15.0.db` se genera una sola vez
- Hacer commit después de cada tarea o grupo lógico
