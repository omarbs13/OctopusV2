# Guía de validación rápida: Registro global de excepciones

Compilar y probar: `dotnet build -v q` y `dotnet test --verbosity quiet` (al implementar, solo el proyecto modificado).

## Prerrequisitos

- Carpeta de datos temporal: `POS_DATA_DIR=/tmp/pos-diag dotnet run --project src/Pos.Desktop`.
- Usuario administrador creado en el primer arranque.

## Escenarios

1. **Excepción en un caso de uso (SC-001, SC-004)**: con una compilación de depuración, forzar una excepción en una operación de pantalla. Esperado: aparece "Ocurrió un error inesperado. Los detalles se registraron para soporte técnico.", la aplicación sigue abierta y `logs/pos-<fecha>.log` tiene una entrada `[FATAL]` con tipo, traza, usuario y pantalla.
2. **Tarea asincrónica (SC-001)**: lanzar una tarea sin esperar que falle y forzar `GC.Collect()`. Esperado: entrada `[FATAL]` y la aplicación no se cierra.
3. **Contexto de venta (SC-002)**: con 3 líneas en la venta y sin cobrar, provocar el error. Esperado: la entrada incluye `SaleLines=3` o el folio y la pantalla `sales.pos`; la venta sigue intacta.
4. **Niveles**: abrir un turno y registrar una venta (INFO), desconectar la impresora (WARNING), enviar un formulario inválido (ERROR). Verificar cada nivel en el archivo.
5. **Agrupación**: provocar el mismo error 10 veces en 5 s. Esperado: un solo mensaje al operador y una entrada con el conteo.
6. **Retención (SC-003)**: crear archivos `pos-<fecha>.log` de hace 29, 30 y 45 días y reiniciar. Esperado: se eliminan los de más de 30 días.
7. **Datos sensibles (SC-006)**: registrar una propiedad `Password`. Esperado: aparece `***`.
8. **Exportar (SC-007)**: "Acerca de > Exportar diagnóstico" sin marcar la base: el zip trae `info.json` y `logs/`. Marcándola: también `pos.db`.
9. **Falla del registro (FR-014)**: quitar el permiso de escritura de la carpeta de logs; la aplicación debe seguir operando.

Contratos: [contracts/diagnostics-contracts.md](contracts/diagnostics-contracts.md). Modelo: [data-model.md](data-model.md).
