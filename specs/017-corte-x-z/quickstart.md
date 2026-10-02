# Quickstart: validar Corte X y Corte Z

Guía para comprobar la funcionalidad de punta a punta. Reglas en [data-model.md](data-model.md),
casos de uso en [contracts/application-ports.md](contracts/application-ports.md) y pantallas en
[contracts/ui.md](contracts/ui.md).

## Prerrequisitos

- .NET 10 SDK.
- Licencia (o prueba) con el módulo "Turnos y arqueo" activo.
- Un Administrador y un Cajero (spec 007). Impresora configurada o salida a archivo (spec 006).

## Compilar y probar

```bash
dotnet build -v q
dotnet test tests/Pos.Domain.Tests --verbosity quiet
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet --filter "FullyQualifiedName~ShiftCut|FullyQualifiedName~SampleDatabase"
dotnet test tests/Pos.ArchitectureTests --verbosity quiet
```

Esperado: 0 errores y 0 advertencias; pasan `ShiftCutTests`, `ShiftCutUseCaseTests`,
`ShiftCutsMigrationTests` y `SampleDatabaseUpgradeTests` (incluye `v0.12.0.db`).

## Migración desde 0.11.0

1. Copiar una base 0.11.0 con turnos cerrados a la carpeta de datos y arrancar la aplicación.
2. Esperado: respaldo previo, migración `ShiftCuts` sin reconstrucción de tablas.
3. "Caja > Histórico de cortes" está vacío; los turnos anteriores siguen en "Ventas > Turnos" con
   "CORTE DE CAJA" y sin folio Z (FR-010a).

## Escenario 1: Corte X (Historia 1)

1. Como Administrador, abrir turno con fondo $500 y registrar dos ventas (una en efectivo, una con
   tarjeta).
2. "Caja > Corte X" → "Generar Corte X". Esperado: folio `X-000001`, cifras del turno, leyenda de
   lectura parcial. Imprimir: título "CORTE X", folio, turno, fecha y la leyenda.
3. En el Punto de venta la barra del turno no cambió; vender una vez más.
4. Generar otro Corte X → `X-000002` incluye la venta nueva. Abrir `X-000001` en el histórico: sus
   cifras no cambiaron (escenario 1.4, 3.4).
5. Generar dos Corte X seguidos: folios distintos, cifras iguales.

## Escenario 2: Cajero con autorización

1. Iniciar sesión como Cajero con turno propio. "Caja > Corte X" → "Generar Corte X".
2. Aparece la autorización de Administrador. Contraseña incorrecta → no se genera; la bitácora
   muestra "Autorización de administrador rechazada".
3. Autorizar correctamente → se genera e imprime. La bitácora muestra "Corte X generado" con quién
   autorizó.
4. El Cajero no ve "Caja > Histórico de cortes".

## Escenario 3: Corte Z (Historia 2)

1. "Caja > Corte Z" → "Hacer Corte Z": conteo ciego, cifras, comentario si hay diferencia.
2. Confirmar. Esperado: "Corte Z Z-000001 · Turno T-… cerrado" e impresión con "CORTE Z".
3. Intentar vender: se pide abrir turno nuevo.
4. Abrir turno nuevo y generar Corte X: cifras en cero (salvo fondo inicial).
5. Cerrar desde "Ventas > Turnos" el turno de otro usuario como Administrador → recibe `Z-000002`.
6. En "Turnos", el detalle del turno cerrado muestra "Corte Z: Z-000001".

## Escenario 4: Histórico (Historia 3)

1. Filtrar por tipo Z, por rango de fechas y por usuario: solo aparecen los que cumplen.
2. Abrir un corte → "Reimprimir": ticket con "REIMPRESIÓN" y cifras idénticas. La bitácora muestra
   "Corte reimpreso".

## Escenario 5: licencia y fallas

1. Desactivar el módulo "Turnos y arqueo": el grupo "Caja" desaparece. Reactivar: vuelve con los
   cortes intactos.
2. Desconectar la impresora y generar un Corte X: queda en el histórico y se reimprime después.

## Verificación de folios (SQL de apoyo, solo lectura)

```sql
SELECT "Type", COUNT(*), MIN("Number"), MAX("Number") FROM "ShiftCuts" GROUP BY "Type";
```

Esperado: `COUNT = MAX` y `MIN = 1` para cada tipo (sin huecos ni repeticiones, SC-002).
