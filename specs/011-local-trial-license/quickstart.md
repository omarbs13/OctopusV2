# Guía de validación: Licencia local

## Prerrequisitos

- `dotnet build -v q` sin errores ni advertencias.
- Carpeta de datos aislada: `export POS_DATA_DIR=/tmp/pos-lic-test`.
- Para probar importaciones a mano, una compilación `DEBUG` con la clave pública de desarrollo (`POS_LICENSE_DEV_PUBLIC_KEY`) y archivos emitidos con la clave privada de desarrollo (no se guarda en el repositorio). Las pruebas automáticas usan un emisor de apoyo.

## Pruebas automáticas

```bash
dotnet test tests/Pos.Domain.Tests --verbosity quiet
dotnet test tests/Pos.Application.Tests --verbosity quiet
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet
dotnet test tests/Pos.ArchitectureTests --verbosity quiet
```

## Escenarios manuales

| # | Escenario | Pasos | Resultado esperado |
|---|---|---|---|
| 1 | Máquina nueva | Carpeta de datos vacía; abrir la app | Se crea `license.lic`; Inicio muestra 30 días restantes y el contacto |
| 2 | Copia a otra máquina | Copiar `license.lic` a otra carpeta con otro ID (o alterarlo) y abrir | Modo lectura con mensaje de licencia inválida |
| 3 | Vencimiento | Con licencia de prueba vieja (fecha de inicio hace 31 días) abrir | Punto de venta, abrir turno, reportes y usuarios se bloquean con el mensaje; Productos, Inventario y Ventas se consultan; Inicio dice "Sistema en modo lectura. Contacte para activación." |
| 4 | Cerrar turno vencido | Con turno abierto al vencer | El turno se puede cerrar |
| 5 | Importar válida | Administrador: Acerca de → Administración de licencia → importar `.poslic` correcto | Se desbloquea sin reiniciar |
| 6 | Importar de otra máquina | Importar un `.poslic` con otro ID | Rechazo claro; licencia sin cambios |
| 7 | Avisos | Simular 5 y 1 días restantes | Aviso en Inicio a 5 días; aviso rojo en el login a 1 día |
| 8 | Borrar `.lic` | Borrar con datos existentes y abrir | Se regenera con el mismo ID y la fecha de inicio del primer usuario, no hoy |
| 9 | Reloj atrás | Retroceder el reloj del sistema | Los días restantes no aumentan |
| 10 | Diagnóstico | Acerca de → Exportar diagnóstico | El zip no contiene `license.lic` |

## Criterios de aceptación cubiertos

Escenarios 1 a 6 y 10 cubren los criterios 1 a 6 de la especificación; 7 a 9 cubren los avisos y las reglas de regeneración y reloj.
