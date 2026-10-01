# Plan de implementación: Registro global de excepciones

**Rama**: `010-global-exception-logging` | **Fecha**: 2026-09-30 | **Especificación**: [spec.md](spec.md)

**Entrada**: especificación de `/specs/010-global-exception-logging/spec.md`

## Resumen

La aplicación ya tiene una base parcial: Serilog con archivo diario ([Logging.cs](../../src/Pos.Desktop/Composition/Logging.cs)), manejadores globales ([GlobalExceptionHandlers.cs](../../src/Pos.Desktop/Composition/GlobalExceptionHandlers.cs)), `OperationRunner` que captura fallas de las operaciones de pantalla y la exportación de diagnóstico en "Acerca de". Esta funcionalidad **cierra las brechas** frente a la especificación sin tocar los casos de uso:

1. **Contexto ambiental** (usuario, pantalla, venta en curso) que se agrega a cada entrada mediante un enriquecedor de Serilog, no editando handlers.
2. **Niveles correctos**: FATAL para lo no controlado (hoy se usa ERROR), ERROR para fallas controladas, INFO para operaciones críticas y WARNING para anomalías esperadas. Los INFO/ERROR de casos de uso salen de un único punto, `UseCases.RunAsync`, sin cambiar los handlers.
3. **Manejadores globales completos**: nivel FATAL, contexto, un solo mensaje por episodio, agrupación de repeticiones y recuperación (permanecer; si no se puede, ir a la pantalla principal de venta).
4. **Almacenamiento**: formato de texto legible, un archivo por día, retención por **30 días** (hoy 31 archivos) con limpieza por fecha al iniciar y al cambiar el día.
5. **Privacidad**: redacción de propiedades sensibles antes de escribir.
6. **Exportación**: todos los registros de 30 días + resumen de versión/entorno; el respaldo de la base pasa a ser **opcional** (hoy siempre se incluye y solo hay 7 días de logs).

## Contexto técnico

**Lenguaje/Versión**: C# / .NET 10, nullable habilitado

**Dependencias principales**: Avalonia, CommunityToolkit.Mvvm, Serilog 4.4 (ya referenciado en `Pos.Desktop`). **Sin dependencias nuevas.**

**Almacenamiento**: archivos de texto en `IAppPaths.LogsDirectory` (`<datos>/logs`); sin cambios de esquema ni migraciones

**Pruebas**: xUnit (política mínima de la constitución, Principio VI)

**Plataforma**: Windows y Linux; rutas por `IAppPaths`

**Tipo de proyecto**: aplicación de escritorio, por capas (Domain / Application / Infrastructure / Desktop)

**Metas de rendimiento**: escribir una entrada no bloquea la interfaz; exportar 30 días de logs en < 30 s (SC-007); recuperación tras error < 10 s (SC-005)

**Restricciones**: sin red; una falla del propio registro nunca afecta la operación (FR-014); los casos de uso no cambian (FR-015)

**Escala**: un equipo por instalación; unos cientos de entradas al día

## Verificación de la constitución

| Principio | Resultado | Notas |
|---|---|---|
| I. La venta nunca se detiene | Cumple | Es el objetivo: el error no cierra la app ni pierde la venta; el registro falla en silencio |
| II. Capas | Cumple | Contexto, enriquecedores, manejadores y UI en Desktop; limpieza de logs y exportador en Infrastructure; el contrato de exportación en Application. Sin referencias nuevas entre proyectos |
| III. Lógica en el núcleo | Cumple | No se agrega lógica de negocio |
| IV. Integridad de datos | Cumple | Sin cambios de esquema; fechas de log con zona horaria explícita |
| V. Multiplataforma | Cumple | Solo `IAppPaths` y APIs de .NET |
| VI. Calidad verificable | Cumple | Pruebas mínimas: retención de 30 días, redacción de datos sensibles, agrupación de repeticiones, exportación y resiliencia del registro |
| VII. Simplicidad | Cumple | Sin paquetes nuevos; nivel como propiedad en vez de `Serilog.Expressions` |
| VIII. Soporte y diagnóstico | Cumple | Serilog, archivos rotativos, contexto sin datos sensibles, exportación |
| IX. Seguridad local | Cumple | Redacción de contraseñas/tarjetas |

**Desviación a vigilar**: el Principio VIII pide log "estructurado". Se pasa de CLEF (JSON) a texto legible por la historia 2. Sigue siendo estructurado: las propiedades se escriben como pares clave=valor en JSON dentro de cada línea. No requiere justificación adicional.

Reevaluado tras el diseño de la fase 1: sin violaciones.

## Estructura del proyecto

### Documentación

```text
specs/010-global-exception-logging/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── diagnostics-contracts.md
└── tasks.md             # lo crea /speckit-tasks
```

### Código fuente

```text
src/
├── Pos.Application/
│   └── Diagnostics/
│       ├── IDiagnosticsExporter.cs                 # ExportAsync(destino, incluirBase, ct)
│       └── ExportDiagnostics/                      # comando con IncludeDatabase
├── Pos.Infrastructure/
│   └── Diagnostics/
│       ├── ZipDiagnosticsExporter.cs               # 30 días de logs; base opcional
│       └── LogRetention.cs                         # elimina archivos de más de 30 días
└── Pos.Desktop/
    ├── Composition/
    │   ├── Logging.cs                              # plantilla de texto, nivel, enriquecedores, 30 días
    │   └── GlobalExceptionHandlers.cs              # FATAL, contexto, episodio, recuperación
    ├── Diagnostics/                                # carpeta nueva
    │   ├── DiagnosticContext.cs                    # usuario, pantalla, venta en curso
    │   ├── DiagnosticContextEnricher.cs
    │   ├── SensitiveDataRedactor.cs
    │   ├── LevelNameEnricher.cs
    │   ├── ErrorEpisodeGate.cs                     # agrupa repeticiones y limita el mensaje
    │   └── LogRetentionScheduler.cs                # limpieza al iniciar y al cambiar el día
    ├── Common/
    │   ├── OperationRunner.cs                      # nivel FATAL para lo inesperado
    │   └── UseCases.cs                             # INFO de operaciones críticas, ERROR de fallas
    ├── About/AboutViewModel.cs + AboutView.axaml   # casilla "Incluir respaldo de la base"
    └── Resources/Strings.resx                      # mensaje del operador (texto de la especificación)

tests/
├── Pos.Infrastructure.Tests/Diagnostics/           # retención y exportación
└── Pos.Desktop.Tests/Diagnostics/                  # redacción y agrupación
```

**Decisión de estructura**: se amplía lo existente en las capas actuales; la única carpeta nueva es `Pos.Desktop/Diagnostics`, porque el contexto y los enriquecedores dependen de la sesión, la navegación y la pantalla de venta, que viven en Desktop.

## Seguimiento de complejidad

Sin violaciones de la constitución que justificar.
