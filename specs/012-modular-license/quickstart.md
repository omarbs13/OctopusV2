# Guía de validación: Licencia modular

## Prerrequisitos

- `dotnet build -v q` sin errores ni advertencias.
- Carpeta de datos aislada: `export POS_DATA_DIR=/tmp/pos-modlic-test` (se borra entre escenarios que lo indiquen).
- Para importar a mano: compilación `DEBUG` con la clave pública de desarrollo (`POS_LICENSE_DEV_PUBLIC_KEY`) y archivos `.poslic` formato 2 emitidos con la clave privada de desarrollo (no se guarda en el repositorio). Las pruebas automáticas usan un emisor de apoyo. Formato en [contracts/license-contracts.md](contracts/license-contracts.md).
- Para simular fechas se usa el reloj inyectable en pruebas; a mano, se cambia la fecha del sistema.

## Pruebas automáticas

```bash
dotnet test tests/Pos.Domain.Tests --verbosity quiet
dotnet test tests/Pos.Application.Tests --verbosity quiet
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet
dotnet test tests/Pos.ArchitectureTests --verbosity quiet
```

(Al implementar se ejecutan solo los proyectos modificados; la suite completa corre en CI.)

## Escenarios manuales

| # | Escenario | Pasos | Resultado esperado |
|---|---|---|---|
| 1 | Primer arranque | Carpeta vacía; abrir | Se crea `license.lic` v2 y la fila de `LicenseSeals`; Inicio muestra 30 días; todos los módulos visibles |
| 2 | Último día | Fecha de inicio hace 29 días | Día 30: todo activo y aviso "Mañana vence el período de evaluación." |
| 3 | Aviso de 5 días | Fecha de inicio hace 25 días | Aviso "Te quedan 5 días de acceso a todos los módulos."; con 4, 3 y 2 días no hay aviso |
| 4 | Modo modular sin compras | Fecha de inicio hace 31 días | Menú sin Inventario, Reportes ni Turnos; Productos, Ventas, Usuarios y Ajustes siguen; sin aviso de cuenta atrás |
| 5 | Venta con módulos bloqueados | Igual a 4; vender | La venta se completa sin turno y sin descontar existencias; no hay error |
| 6 | Operación bloqueada | Acceso directo/atajo a un módulo inactivo | "Este módulo no está activo en tu licencia."; sin cambios de datos |
| 7 | Importar válida | Administrador: Acerca de → Administración de licencia → importar `.poslic` que activa Inventario | Inventario aparece de inmediato sin reiniciar; los demás siguen bloqueados; `FirstRun` no cambia |
| 8 | Sumar módulos | Importar otra licencia con Reportes (y repetir la primera) | Quedan Inventario + Reportes; sin duplicados |
| 9 | Otra máquina / alterada | Importar `.poslic` con otro ID o con un byte cambiado | Rechazo claro; licencia sin cambios |
| 10 | Archivo `license.lic` editado | Cambiar un byte y abrir | Mensaje claro; se regenera sin módulos comprados con la fecha original; la evaluación sigue su curso |
| 11 | Archivo borrado | Borrar `license.lic` con datos existentes y abrir | Igual que 10; la fecha de inicio viene de la copia protegida, no de hoy (SC-007) |
| 12 | Borrar archivo y atrasar reloj | Borrar el archivo y poner el reloj 10 días atrás | Los días restantes no aumentan |
| 13 | Datos conservados | Bloquear y reactivar Inventario | Existencias y movimientos previos intactos (SC-005) |
| 14 | Actualizar desde 011 con licencia activa | Datos con `license.lic` v1 y concesión activa; abrir | Todos los módulos habilitados; archivo pasa a v2 |
| 15 | Actualizar desde 011 sin licencia | `license.lic` v1 de evaluación o vencido; abrir | Evaluación de 30 días nueva |
| 16 | Usuario no administrador | Iniciar sesión como Cajero | No ve la administración de licencia |
| 17 | Diagnóstico | Acerca de → Exportar diagnóstico | El zip no contiene `license.lic` |

## Criterios cubiertos

Escenarios 1–4 y 7 cubren las historias 1 y 5 y SC-001/SC-006; 6 y 13 la historia 4 y SC-003/SC-005; 7–9 y 16 las historias 3 y 6 y SC-004; 10–12 la historia 2 y SC-002/SC-007; 14–15 FR-022.

## Verificación de migración

La prueba de arranque y migraciones debe aplicar la migración `ModularLicense` sobre las bases de ejemplo existentes sin perder datos y dejar la tabla `LicenseSeals` vacía. Revisar a mano el SQL generado (solo `CREATE TABLE`).
