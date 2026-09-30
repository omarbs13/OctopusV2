# Cómo agregar una funcionalidad

Guía paso a paso usando **Productos** como ejemplo. Cada paso indica el archivo de Productos que
sirve de modelo. Antes de empezar, lee la [constitución](../.specify/memory/constitution.md).

## 0. Especificación, plan y tareas (Spec Kit)

Ninguna funcionalidad empieza con código:

```text
/speckit-specify   → specs/NNN-nombre/spec.md
/speckit-clarify   → resuelve ambigüedades
/speckit-plan      → plan.md, research.md, data-model.md, contracts/, quickstart.md
/speckit-tasks     → tasks.md
/speckit-analyze   → consistencia entre documentos
/speckit-implement
```

El plan debe incluir la verificación de cumplimiento de la constitución. Cualquier desviación se
justifica por escrito en el plan. Ejemplo: `specs/001-pos-foundation/`.

## 1. Dominio (TDD)

Modelo: `src/Pos.Domain/Products/Product.cs` y `tests/Pos.Domain.Tests/Products/ProductTests.cs`.

1. Escribe primero las pruebas de las reglas y de sus casos límite, y verifica que fallen.
2. Crea la entidad en `src/Pos.Domain/<Funcionalidad>/`:
   - `Id` con `Guid.CreateVersion7()`, nunca autoincremental.
   - Campos de auditoría (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`), `DeletedAt`
     para el borrado lógico y `int Version` para la concurrencia. Los llena la persistencia; la
     entidad no los asigna.
   - Setters privados y métodos con intención (`Create`, `Update`, `Delete`) que validan
     invariantes y lanzan `DomainException`.
   - Dinero siempre con `Money` (centavos enteros); nunca `double` ni `float`.
   - Fechas siempre en UTC.
3. Domain no referencia ningún otro proyecto ni librería de infraestructura (lo verifican las
   pruebas de arquitectura).

## 2. Caso de uso (TDD)

Modelo: `src/Pos.Application/Products/CreateProduct/` y
`tests/Pos.Application.Tests/Products/CreateProductHandlerTests.cs`.

Organización por funcionalidad, un caso de uso por carpeta:

```text
src/Pos.Application/<Funcionalidad>/<CasoDeUso>/
  <CasoDeUso>Command.cs     record con la entrada tal como la captura la UI
  <CasoDeUso>Validator.cs   AbstractValidator<T> de FluentValidation
  <CasoDeUso>Handler.cs     clase simple con HandleAsync(...) → Result / Result<T>
```

- Sin MediatR: el handler es una clase normal que se registra en DI.
- Devuelve `Result` con errores de negocio explícitos (`ValidationFailed`, `Duplicate`,
  `NotFound`, `Conflict`). Las excepciones son solo para fallas inesperadas.
- Los nombres de campo de los errores (`ProductFields`) coinciden con los controles de la UI.
- Los mensajes para el operador están en español.
- Escribe las pruebas con un repositorio en memoria (modelo:
  `tests/Pos.Application.Tests/TestSupport/InMemoryProductRepository.cs`).

## 3. Puerto y repositorio

Modelo: `src/Pos.Application/Products/IProductRepository.cs` y
`src/Pos.Infrastructure/Products/ProductRepository.cs`.

- La interfaz se define en Application y es específica del agregado. No hay repositorios
  genéricos.
- La implementación va en Infrastructure. Traduce las excepciones de EF Core y SQLite a
  resultados (`SaveOutcome`): ninguna excepción del proveedor llega a Application.
- Una operación = un `SaveChangesAsync` = una transacción.
- Para la concurrencia optimista, establece `OriginalValue` de `Version` con la versión que vio
  el operador (ver `SaveChangesAsync` en el repositorio de Productos).
- Prueba contra **SQLite real** con `TestDb`, nunca con el proveedor InMemory de EF Core
  (modelo: `tests/Pos.Infrastructure.Tests/Products/`).

## 4. Configuración y migración

Modelo: `src/Pos.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`.

1. Agrega el `DbSet` en `PosDbContext` y una configuración `IEntityTypeConfiguration<T>`.
2. Índices únicos filtrados por `"DeletedAt" IS NULL` si los valores deben poder reutilizarse
   después de un borrado.
3. Crea y revisa la migración según [migraciones.md](migraciones.md).

## 5. Registro en DI

- Application: `src/Pos.Application/DependencyInjection.cs`. Validadores como singleton,
  handlers con `AddScoped`.
- Infrastructure: `src/Pos.Infrastructure/DependencyInjection.cs`. Repositorios con
  `AddScoped`, porque comparten el `DbContext` de la operación.

Cada operación de la UI crea su propio ámbito de DI por medio de `UseCases`, así que cada caso
de uso usa un `DbContext` nuevo.

## 6. ViewModel

Modelo: `src/Pos.Desktop/Products/ProductEditorViewModel.cs` y `ProductsViewModel.cs`.

- Solo coordina la interfaz: invoca casos de uso y presenta resultados. **No calcula** totales,
  impuestos ni descuentos.
- Toda operación pasa por `OperationRunner.RunAsync("NombreOperacion", ..., contexto)`. Así un
  error inesperado se registra con la operación, el usuario y los identificadores, el operador
  ve un mensaje comprensible y la pantalla sigue usable.
- Invoca el caso de uso con `UseCases.RunAsync<THandler, TResult>(h => h.HandleAsync(...))`.
- Usa `[ObservableProperty]` y `[RelayCommand]` de CommunityToolkit.Mvvm. Los comandos
  asíncronos no admiten ejecuciones simultáneas, lo que evita guardar dos veces por un doble clic.
- Los diálogos se muestran a través de `IDialogService`, para poder probarlo sin UI.
- Una pantalla del menú lateral hereda de `PageViewModel`.
- Pruebas: `tests/Pos.Desktop.Tests/` con `DesktopTestHost` (casos de uso reales y un
  repositorio en memoria).

## 7. Vista

Modelo: `src/Pos.Desktop/Products/ProductsView.axaml` y `ProductEditorView.axaml`.

- Compiled bindings (`x:DataType`) siempre.
- **Ningún texto fijo**: todo va en `src/Pos.Desktop/Resources/Strings.resx` y se usa con
  `{x:Static res:Strings.Clave}`.
- Asocia el ViewModel con su vista en `src/Pos.Desktop/Composition/App.axaml`
  (`DataTemplate`), sin reflexión.
- Registra el ViewModel en `src/Pos.Desktop/Composition/HostBuilder.cs`. Si es una pantalla del
  menú, regístralo también como `PageViewModel`.
- Ningún ViewModel ni vista accede al `DbContext` ni a SQL; las pruebas de arquitectura lo
  verifican.

## 8. Verificación final

```bash
dotnet build   # 0 advertencias
dotnet test    # todas las suites, incluidas las de arquitectura
```

Además:

- Si cambió el esquema, amplía `SampleData` y revisa `SampleDatabaseUpgradeTests`.
- Recorre el `quickstart.md` de tu funcionalidad con la red desconectada.
- Documenta lo necesario para operar y dar soporte a la funcionalidad.
- Haz commits con Conventional Commits (`feat:`, `fix:`, `docs:`...).
