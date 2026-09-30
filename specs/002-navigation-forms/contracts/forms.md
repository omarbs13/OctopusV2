# Contrato: patrón de formularios y cambios sin guardar

## FormViewModel (base para todo formulario)

```text
string Title
IAsyncRelayCommand SaveCommand      // sin ejecuciones simultáneas (evita el doble clic)
IAsyncRelayCommand CancelCommand    // ConfirmLeaveAsync → Closed
bool IsDirty                        // estado actual ≠ estado original (normalizados)
string? FocusField
event Saved(object result)
event Closed

// Lo implementa cada formulario:
protected abstract object CaptureState();              // registro inmutable normalizado
protected abstract Task<bool> SaveCoreAsync();         // valida con el caso de uso y muestra errores

// Lo ofrece la base:
Task<bool> ConfirmLeaveAsync();     // verdadero = se puede salir
protected void ResetOriginalState(); // al abrir, al recargar y después de guardar
```

`ConfirmLeaveAsync()`:

| `IsDirty` | Respuesta del operador | Resultado |
|---|---|---|
| falso | — (no se pregunta) | verdadero |
| verdadero | Guardar, guardado exitoso | verdadero (y se emite `Saved`) |
| verdadero | Guardar, con errores o rechazado (duplicado, conflicto) | falso; los errores quedan visibles |
| verdadero | Descartar | verdadero |
| verdadero | Seguir editando | falso |

Validación: solo al guardar (`SaveCommand` o Guardar en la confirmación). Todos los errores se
muestran juntos, cada uno junto a su campo, y el foco va al primero. Salir de un campo no
valida.

## Presentación (la decide quien abre el formulario)

```text
FormHost (en PageViewModel):
  FormViewModel? ActiveForm
  FormPresentation ActivePresentation   // SidePanel | FullScreen
  Task<bool> OpenAsync(FormViewModel form, FormPresentation presentation)  // antes confirma salir del formulario actual
  Task<bool> CloseActiveAsync()                                           // ConfirmLeaveAsync y luego cierra
```

- **SidePanel**: formularios de hasta 8 campos sin pestañas ni listas internas. Panel a la
  derecha del listado; el listado sigue visible.
- **FullScreen**: el área de contenido muestra solo el formulario, con la barra superior
  "← Regresar al listado" (equivale a Cancelar). El menú lateral sigue visible y se puede usar,
  sujeto a la confirmación.

Un `PageViewModel` con `FormHost` implementa `ILeaveGuard` delegando en `CloseActiveAsync()`.

## Disparadores de la confirmación (FR-028)

| Disparador | Punto de control |
|---|---|
| Cancelar o "Regresar al listado" | `CancelCommand` |
| Abrir otro registro desde el listado con un formulario abierto | `FormHost.OpenAsync` |
| Navegar con el menú | `Navigator.NavigateAsync` → `ILeaveGuard` |
| Cerrar la ventana principal | `Navigator.CanLeaveCurrentAsync` (antes del respaldo al cerrar) |

## Diálogo

`IDialogService.AskUnsavedChangesAsync()` → `Save | Discard | KeepEditing`.

- Texto: "Hay cambios sin guardar. ¿Qué desea hacer?"
- Botones: Guardar (acento), Descartar, Seguir editando (predeterminado para Enter y Esc).

## Campos obligatorios

`FieldLabel` con `IsRequired="True"` muestra "Nombre *" con el asterisco en color de acento, y
su nombre accesible incluye "obligatorio". En Productos son obligatorios Nombre, SKU y Precio.
