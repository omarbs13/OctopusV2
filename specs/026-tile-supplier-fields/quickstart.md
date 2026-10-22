# Quickstart: validar la funcionalidad

## Prerrequisitos
- .NET 10 SDK. Desde la raíz: `dotnet build -v q`

## Pruebas
- Dominio: `dotnet test tests/Pos.Domain.Tests --verbosity quiet`
- Infraestructura (SQLite real): `dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet`
- Aplicación: `dotnet test tests/Pos.Application.Tests --verbosity quiet`

## Escenarios (los criterios de la spec)
1. **Migración**: abrir la base de ejemplo de la versión anterior; verificar mismos conteos de productos,
   ventas, imágenes y existencias, totales de reportes idénticos y resumen visible una sola vez.
2. **Sugerencia**: X M2 $196.25 y Embalaje 1.86 → X CAJA sugerido $365.03.
3. **Unicidad**: guardar "SUNWOOD MIEL 18X60" y luego "sunwood miel 18 x 60" del mismo proveedor → rechazado;
   con "20X90" → aceptado; "120*240" vs "120X240" → rechazado.
4. **Venta**: 3 cajas a $287.05 → $861.15; 4.92 m² a $175.03 → $861.15.
5. **Búsqueda**: "fort wood" devuelve todas las medidas.
6. **Duplicados migrados**: dos productos con el mismo nombre → ambos conservados y marcados "Para revisión".
7. **Respaldo**: restaurar un respaldo de la versión anterior y confirmar los puntos 1 y 6.

Detalles de campos en [data-model.md](data-model.md); interfaz en [contracts/ui.md](contracts/ui.md).
