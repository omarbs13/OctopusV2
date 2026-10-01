# Guía de validación: Reportes y análisis

Sirve para comprobar de extremo a extremo que la funcionalidad cumple la especificación.

## Preparación

```bash
dotnet build -v q
dotnet test --verbosity quiet tests/Pos.Domain.Tests
dotnet test --verbosity quiet tests/Pos.Application.Tests
dotnet test --verbosity quiet tests/Pos.Infrastructure.Tests
```

Al implementar se ejecutan solo las pruebas del proyecto modificado (constitución, Principio VI).

Datos de prueba (ver [data-model.md](data-model.md)):

- Un Administrador y dos Cajeros (Ana y Luis).
- Datos del negocio capturados (nombre, dirección, teléfono).
- Ventas de los últimos 40 días con las tres formas de pago, al menos una cancelada.
- Turnos cerrados de Ana y Luis: uno con diferencia de −6 %, otro de +2 %, y un turno abierto.
- Productos con movimientos en fechas distintas: uno que queda en 0 después de la fecha de consulta,
  uno con existencia igual a su mínimo y uno marcado como crítico.

## Escenarios

1. **Ventas (Historia 1)**: como Administrador, abrir Reportes > Ventas con "Últimos 7 días". Comprobar
   que el total, la cantidad y el ticket promedio coinciden con el cálculo manual sin la venta cancelada,
   que efectivo + tarjeta + transferencia suman el total y que la gráfica tiene siete puntos. Activar el
   comparativo y verificar la variación contra los 7 días anteriores. Filtrar por Ana y comprobar que
   todo se recalcula.
2. **Arqueo (Historia 2)**: abrir Reportes > Arqueo con "Este mes". Comprobar diferencia −60.00 y −6 %
   con alerta, barra roja, barra verde para +2 %, turno abierto como "En curso" sin cifras de efectivo y
   totales que excluyen ese turno. Cambiar el umbral a 10 % y comprobar que desaparece la alerta.
3. **Inventario (Historia 3)**: elegir una fecha anterior a una salida que dejó un producto en 0 y
   comprobar que aparece con su existencia de esa fecha; con la fecha de hoy aparece sin existencia.
   Probar filtro, búsqueda, orden y paginación de 100.
4. **PDF (Historia 4)**: exportar cada reporte. Abrir el PDF en Windows y en Linux y comprobar A4
   horizontal, encabezado y pie en todas las páginas, acentos, tabla que continúa en otra página con su
   encabezado, gráficas y que el arqueo no incluye cifras del turno abierto.
5. **Excel (Historia 5)**: exportar a Excel; comprobar tres hojas, importes y fechas como valores
   (ordenar y sumar una columna) y gráficas como imágenes.
6. **Mi turno (Historia 6)**: como Ana con turno abierto, ver fondo, ventas, ingresos y retiros sin
   esperado ni contado. Cerrar el turno y comprobar que aparecen. Exportar PDF. Comprobar que no puede
   abrir los reportes de Ventas ni Arqueo ni ver turnos de Luis.
7. **Alertas (Historia 7)**: como Administrador, ver en Inicio el turno con diferencia sobre el umbral y el
   producto crítico con existencia baja. Sin alertas, ver "No hay alertas".
8. **Sin datos**: elegir un período sin ventas en cada reporte y comprobar "Sin datos en este período",
   sin error, con Exportar deshabilitado.
9. **Permisos**: como Ana, verificar que el grupo Reportes solo muestra Inventario, sin costos.

## Rendimiento (SC-002, SC-006)

La prueba de Infrastructure `SalesReportPerformanceTests` siembra 10,000 ventas en SQLite y afirma que el
reporte de ventas y el de arqueo responden en menos de 2 s y que la generación del PDF tarda menos de
10 s. Se ejecuta con el resto de las pruebas de Infrastructure.

## Migración

- Generar la migración `ReportsAnalytics`, revisar el SQL (sin reconstrucción de `Products`).
- Ejecutar la prueba que migra todas las bases de ejemplo, incluida `v0.7.0.db` generada antes de la
  migración (ver `docs/migraciones.md`).
