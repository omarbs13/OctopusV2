# Quickstart: validar la auditoría detallada

Guía para comprobar de punta a punta la spec [018-audit-trail](spec.md). Los contratos están en
[contracts/](contracts/) y el modelo en [data-model.md](data-model.md).

## §1 Preparación

```bash
dotnet build -v q
dotnet test --verbosity quiet
```

- Arranca la aplicación con una base que venga de la versión 0.12.0. La migración `AuditTrail` se
  aplica sola, después del respaldo automático.
- Inicia sesión como Administrador y ten a mano un Cajero.

## §2 Historia 1: registro con antes y después

1. **Producto**:
   1. Crea el producto "Prueba auditoría", con precio $25.00.
   2. Edítalo: precio $28.50 y otra categoría.
   3. Guárdalo sin cambios.
   4. Bórralo.
2. **Cancelación y devolución**: como Cajero, cancela una venta con autorización de Administrador,
   con motivo "Error de captura". Haz una devolución parcial de otra venta.
3. **Descuentos**: cobra una venta con un descuento de línea dentro del límite (sin autorización) y
   otra con un descuento fuera del límite (autorizado).
4. **Cajón**: abre el cajón sin venta.
5. **Usuarios**: crea un usuario, cámbiale el rol, desactívalo y restablece su contraseña.
6. **Sesión**: cierra sesión, intenta entrar con una contraseña incorrecta y luego entra bien.
7. Abre **Administración > Auditoría**.

**Esperado**:

- **Producto**:
  - "Producto creado" con todos los campos iniciales.
  - "Producto modificado" con solo Precio ($25.00 → $28.50) y Categoría.
  - **No** hay entrada por el guardado sin cambios.
  - "Producto eliminado" con los últimos valores.
- **Cancelación**: muestra Usuario = Cajero, Autorizó = Administrador y Motivo = "Error de captura".
- **Descuentos**: dos entradas "Venta con descuento", una por venta. La autorizada muestra quién
  autorizó.
- **Cajón**: "Cajón abierto sin venta" con el usuario.
- **Usuario**: "Usuario modificado" con Rol antes → después. En el restablecimiento de contraseña
  no aparece ninguna contraseña.
- **Sesión**: cierre de sesión, intento fallido e inicio de sesión.
- Ninguna fila tiene vacío el campo Usuario. No existe ninguna opción para editar o borrar.

Como Cajero, verifica que no aparece "Auditoría".

## §3 Historia 2: filtros

1. **Entidad = Producto**: solo aparecen los eventos de productos.
2. **Evento = "Producto modificado"** y un rango de fechas: solo las modificaciones en el rango.
3. **Usuario = el Administrador**: incluye la cancelación que autorizó, aunque la hizo el Cajero.
4. Selecciona la entrada del producto y pulsa **"Ver historial del registro"**: aparecen sus tres
   eventos, del más antiguo al más reciente. Quita el chip.
5. Pon "Hasta" antes de "Desde": aparece un mensaje de validación y no hay resultados.
6. Usa filtros sin coincidencias: aparece "No hay entradas con estos filtros."
7. Las entradas creadas antes de actualizar siguen visibles con su descripción original y aparecen
   con el filtro de su entidad.

## §4 Historia 3: exportación

1. Filtra un rango con más de 100 entradas y pulsa **Exportar PDF** y luego **Exportar Excel**.
   - **PDF**: tiene encabezado del negocio, rango, filtros, fecha de generación y usuario, y todas
     las entradas.
   - **Excel**: tiene una fila por campo modificado, con las columnas de FR-024.
2. La bitácora muestra dos entradas "Bitácora exportada" con formato, rango y número de entradas.
3. Exporta sin una de las fechas: se pide el rango y no se genera nada.
4. Exporta a una carpeta sin permiso de escritura: aparece el mensaje de error y **no** hay entrada
   "Bitácora exportada".

## §5 Integridad e inmutabilidad (pruebas automáticas)

```bash
dotnet test tests/Pos.Domain.Tests --verbosity quiet
dotnet test tests/Pos.Application.Tests --verbosity quiet
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet
dotnet test tests/Pos.ArchitectureTests --verbosity quiet
```

Deben pasar:

- `AuditFieldChangeTests`, `AuditChangesTests` y `UserAuditFieldsTests`.
- `ProductAuditTests`, `AuditAuthorTests`, `SaleDiscountAuditTests`, `AuditLogReaderTests` y
  `ExportAuditLogTests`.
- Las pruebas de descuentos existentes (`ConfirmSaleApprovalTests`), ajustadas al nuevo evento.
- `AuditTrailMigrationTests` y `SampleDatabaseUpgradeTests` (incluida `v0.13.0.db`).
- La regla de arquitectura que prohíbe `ExecuteUpdate` y `ExecuteDelete` sobre `AuditEntries`.

## §6 Rendimiento (SC-002, antes de publicar)

```bash
dotnet run --project tests/Pos.Infrastructure.Tests -c Release -- -class "Pos.Infrastructure.Tests.Audit.AuditLogPerformanceTests" -explicit only
```

La prueba siembra 1,000,000 de entradas y mide la primera página con cada combinación de filtros:
fechas, usuario, evento, entidad, historial y todos juntos.

**Esperado**: cada búsqueda tarda menos de 1 s. Si alguna pasa de 300 ms, revisa el plan de la
consulta (`EXPLAIN QUERY PLAN`) antes de publicar.

## §7 Exportación grande (SC-006)

Con la base sembrada de §6, exporta un rango de 10,000 entradas a PDF y a Excel. Cada exportación
debe tardar menos de 30 s y el archivo debe contener todas las entradas.
