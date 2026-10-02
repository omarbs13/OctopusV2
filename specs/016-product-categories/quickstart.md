# Quickstart: validar Categorías de productos

**Funcionalidad**: `016-product-categories` | **Plan**: [plan.md](plan.md)

## Requisitos previos

- .NET 10 SDK; base de datos de una versión 0.10.0 (o `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.10.0.db`).
- Usuario Administrador y un Cajero.

## 1. Compilar y probar

```bash
dotnet build -v q                                   # 0 errores, 0 advertencias
dotnet test tests/Pos.Domain.Tests --verbosity quiet
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet
dotnet test tests/Pos.ArchitectureTests --verbosity quiet
```

Pruebas esperadas: ver [research.md §17](research.md#17-pruebas-política-mínima-constitución-v120).

## 2. Migración

```bash
dotnet ef migrations script <migración anterior> ProductCategories \
  --project src/Pos.Infrastructure --startup-project src/Pos.Desktop
```

Esperado: `CREATE TABLE "Categories"`, sus índices, `ALTER TABLE "Products" ADD "CategoryId"` y su
índice. **Ningún** `ef_temp_` (sin reconstrucciones). Al abrir la app sobre una base 0.10.0: se
respalda, migra y todos los productos aparecen como "Sin categoría" y siguen vendiéndose (SC-005).

## 3. Catálogo (Historia 1)

1. Catálogos > Categorías → crear "Bebidas" sin descripción: activa, 0 productos.
2. Crear "bebidas" y "  BEBÍDAS ": "Ya existe una categoría con ese nombre". Desactivar "Bebidas" y
   repetir: mismo rechazo.
3. Nombre vacío, de 51 caracteres o descripción de 201: rechazo por campo.
4. Asignar "Bebidas" a 12 productos y desactivarla: aviso con 12; Cancelar → sigue activa; Confirmar →
   inactiva y los 12 la conservan.
5. Eliminar "Bebidas": "No se puede eliminar: la categoría tiene 12 productos…".
6. Crear "Temporal" sin productos y eliminarla: desaparece del listado y de los filtros.
7. Buscar "beb" y alternar el filtro de estado.
8. Abrir la misma categoría en dos sesiones de edición y guardar ambas: la segunda recibe el aviso de
   cambio.

## 4. Asignación (Historia 2)

1. Producto nuevo sin categoría → se guarda; aparece "Sin categoría" en el listado.
2. Asignar "Botanas" → la columna y el contador de "Botanas" cambian; volver a "Sin categoría" → baja.
3. Con "Lácteos" inactiva: no aparece en el selector; un producto que ya la tenía se edita y la
   conserva marcada "(inactiva)".
4. Filtrar el listado por "Botanas" y por "Sin categoría", combinado con búsqueda por nombre o SKU.
5. Vender un producto sin categoría en el punto de venta: la venta se registra normal.
6. Con el formulario de un producto abierto eligiendo "Botanas", desactivar "Botanas" desde otra
   pantalla y guardar el producto: mensaje para elegir otra categoría.

## 5. Reportes (Historia 3)

Datos: en el período, ventas por 1,000.00 de "Bebidas", 600.00 de "Botanas" y 400.00 sin categoría;
incluir una venta mixta Bebidas+Botanas, un descuento de línea, un descuento global, una devolución
parcial y una venta cancelada.

1. Reportes > Ventas, "Ventas por categoría": Bebidas 50.0 %, Botanas 30.0 %, Sin categoría 20.0 %,
   total 2,000.00 = total del reporte sin filtro.
2. Expandir "Bebidas": productos por unidades desc; la suma de importes = 1,000.00.
3. Filtrar por "Bebidas": total 1,000.00; la venta mixta aparece solo con sus líneas de Bebidas; formas
   de pago muestran "—".
4. Mover un producto de "Botanas" a "Bebidas" y volver a consultar: sus ventas pasadas cuentan en
   "Bebidas".
5. Reportes > Inventario filtrado por "Lácteos" y por "Sin categoría": tarjetas, gráfica y tabla solo de
   esos productos; ordenar por la columna "Categoría".
6. Exportar ambos reportes a PDF y Excel con filtro: "Categoría: …" en filtros y mismas cifras; el de
   ventas incluye la sección con desglose.
7. Como Cajero: el reporte de inventario muestra el filtro; "Reportes > Ventas" sigue sin acceso
   (FR-022).

## 6. Rendimiento (SC-004)

Con la base de rendimiento de 009 (10,000 ventas en el período), abrir Reportes > Ventas con y sin
filtro de categoría: menos de 2 s en ambos casos.
