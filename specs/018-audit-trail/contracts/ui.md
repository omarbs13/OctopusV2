# Contrato: interfaz "Auditoría"

Pantalla existente `Administration/AuditLogView`, solo para el Administrador (permiso
`ViewAuditLog`). Es de solo lectura: no tiene ningún botón para editar ni borrar entradas.

## Filtros (barra superior)

| Control | Valor inicial | Comportamiento |
|---|---|---|
| Desde / Hasta | Hoy / Hoy | Fechas locales. Si "Hasta" es anterior a "Desde", se muestra el error de validación y no se busca. |
| Usuario | Todos | Lista de todos los usuarios, activos o no, más "Sistema". Coincide con el autor, el autorizador o el usuario afectado. |
| Evento | Todos | Catálogo `AuditActions`. |
| Entidad | Todas | Grupos de `AuditEntityGroup`. |
| Chip "Historial de: {registro}" | oculto | Aparece al elegir "Ver historial del registro". El ✕ lo quita y vuelve al orden normal. |

Cada cambio de filtro vuelve a la página 1 y busca. La paginación de 100 registros y el resumen
"N entradas · página X de Y" no cambian.

## Tabla

Columnas: Fecha y hora · Evento · Entidad · Registro · Usuario · Autorizó · Resumen.

- **Registro**: `EntityName`; en las entradas anteriores, vacío.
- **Resumen**:
  - Con cambios: "Precio, Categoría (+1)", es decir, los campos cambiados, hasta 2, y cuántos
    más hay.
  - Sin cambios: `Details` recortado.
- Sin resultados: "No hay entradas con estos filtros."

## Panel de detalle (fila seleccionada)

- **Encabezado**: evento, fecha y hora, usuario y, si lo hay, autorizó.
- **Motivo**: si lo hay.
- **Tabla de cambios**: Campo | Antes | Después.
  - Los nulos se muestran como "—".
  - Los textos largos se ajustan y no se recortan.
- **Detalles**: el texto completo.
- **Botón "Ver historial del registro"**: aplica el filtro de registro con la entidad y el id de la
  fila, y limpia las fechas. Se desactiva si `EntityId` está vacío.

## Exportación

Botones "Exportar PDF" y "Exportar Excel".

1. Si falta alguna de las dos fechas: "Elige un rango de fechas para exportar." No se exporta.
2. Mientras se genera, se muestra un indicador de espera. La venta y el resto de la aplicación no
   se bloquean.
3. Se abre el diálogo de guardar con el nombre sugerido `bitacora_AAAAMMDD-AAAAMMDD.pdf` o `.xlsx`.
4. Si se guarda bien: se llama a `ConfirmAuditExport` y aparece el aviso "Bitácora exportada (N
   entradas)."
5. Si se cancela el diálogo: no se registra nada.
6. Si falla la escritura: "No se pudo guardar el archivo: {motivo}." No se registra nada.

## Textos nuevos (`Strings.resx`)

`Audit_Entity`, `Audit_Record`, `Audit_Summary`, `Audit_Reason`, `Audit_Changes`, `Audit_Field`,
`Audit_Before`, `Audit_After`, `Audit_ViewHistory`, `Audit_HistoryOf`, `Audit_ExportPdf`,
`Audit_ExportExcel`, `Audit_ExportNeedsRange`, `Audit_Exported`, `Audit_ExportFailed`,
`Audit_NoResults`, más los nombres de los grupos de entidad.
