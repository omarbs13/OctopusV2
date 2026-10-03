# Quickstart: validación de Mejoras de UX/UI

**Feature**: 023-ux-ui-polish | **Fecha**: 2026-10-02

Guía para comprobar de punta a punta que la funcionalidad cumple la spec. Los contratos están en
[contracts/ui.md](contracts/ui.md) y los modelos en [data-model.md](data-model.md).

## Requisitos previos

- .NET 10 SDK. Desde la raíz del repositorio:

  ```bash
  dotnet build -v q
  ```

  Debe terminar sin errores ni advertencias.
- Carpeta de datos de la aplicación (la ruta está en el diagnóstico). Para "borrar preferencias",
  eliminar `<datos>/preferences/window.json` y `<datos>/preferences/navigation*.json`.
- Usuarios de prueba: un Administrador y un Cajero.

## Pruebas automáticas

Al implementar se ejecutan solo las pruebas de los proyectos modificados (Principio VI):

```bash
dotnet test tests/Pos.Desktop.Tests --verbosity quiet
dotnet test tests/Pos.Application.Tests --verbosity quiet
dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet
```

Resultado esperado: todas pasan, incluidas las de research §11:

- espera mínima de 2 s;
- menú colapsado sin preferencia, con clave por usuario;
- escáner en Configuración;
- menú sin iconos repetidos;
- `BusinessHeader`;
- encabezado en los tickets;
- PDF con encabezado solo en la página 1;
- aviso en el XLSX.

## Escenarios manuales

Ejecutar la aplicación con `dotnet run --project src/Pos.Desktop`.

### 1. Ventana (US1, US2)

1. Borrar las preferencias y arrancar → la ventana aparece **maximizada**.
2. Restaurar, redimensionar a unos 1300×900, mover y cerrar. Volver a arrancar → aparece **normal**
   con ese tamaño y en esa posición.
3. Maximizar, minimizar y cerrar desde la barra de tareas. Volver a arrancar → aparece
   **maximizada**.
4. En estado normal, arrastrar el borde para reducir la ventana → se detiene en **1024×768**.
5. Editar `window.json` con `X`/`Y` = 20000 y arrancar → aparece centrada en la pantalla
   principal.
6. Escribir texto inválido en `window.json` y arrancar → aparece maximizada, sin mensaje, con un
   aviso "No se pudo leer la preferencia" en el log.
7. (Opcional) Con una resolución de 1024×600: la ventana cabe en la pantalla, aparecen barras de
   desplazamiento y los botones inferiores de "Cerrar turno" son alcanzables.

### 2. Pantalla de carga (US3)

1. Arrancar con la base ya migrada y cronometrar → la pantalla de carga dura unos **2 s**.
2. Arrancar con una base de ejemplo antigua (migración más lenta) → la pantalla dura lo que tarda
   el arranque, sin 2 s extra.
3. Abrir una segunda instancia o simular una falla de migración → el mensaje aparece de inmediato.

### 3. Menú (US4, US5, US6)

1. Borrar las preferencias e iniciar sesión como Administrador → **todos los grupos colapsados**.
2. Expandir Ventas e Inventario, cerrar sesión y volver a entrar con el mismo usuario → solo esos
   dos grupos están expandidos.
3. Entrar como Cajero → sus grupos están colapsados; el estado del Administrador no le afecta.
4. Contraer el menú con Ctrl+B y volver a expandirlo → Ventas e Inventario siguen expandidos.
5. Alternar expandido/contraído y observar el botón hamburguesa → no se mueve horizontalmente y
   está en la columna de los iconos.
6. Con el menú contraído, como Administrador, recorrer todos los iconos → ninguno se repite (tabla
   de [contracts/ui.md](contracts/ui.md#iconos-del-menú)) y cada uno muestra su nombre en el
   tooltip.

### 4. Botones (US7)

Con la ventana maximizada y en 1024×768, revisar estas pantallas:

- Punto de venta: Ingreso, Retiro, Cobrar y los botones laterales.
- Cobro.
- Abrir turno, Movimiento de caja y Cerrar turno.

En todas, el texto debe estar centrado en ambos ejes y ningún texto debe salirse del botón.

### 5. Encabezado del negocio (US8)

1. En Configuración > Datos del negocio, capturar nombre, dirección larga (≥ 150 caracteres),
   teléfono, RFC y logo.
2. Exportar a PDF el reporte de Ventas de un período con más de una página:
   - la página 1 inicia con logo, nombre, dirección ajustada, teléfono y RFC;
   - la página 2 no tiene encabezado de negocio;
   - el pie aparece en ambas.
3. Exportar a Excel el mismo reporte → la hoja "Resumen" inicia con nombre, dirección, teléfono y
   RFC, sin logo.
4. Repetir el paso 2 con Corte de caja, Inventario, Mi turno, Descuentos y Bitácora → el encabezado
   es idéntico en todos.
5. Imprimir, con la impresora configurada como archivo:
   - un ticket de venta;
   - una nota de crédito;
   - un abono;
   - un corte X;
   - un corte Z;
   - un movimiento de caja.

   Todos empiezan con `[LOGOTIPO]` y las mismas líneas en el mismo orden, centradas a 32 o
   48 columnas.
6. Quitar el RFC y el logo y exportar → no quedan líneas vacías ni "RFC:" sin valor.
7. En una base sin datos del negocio, exportar un reporte → "Datos del negocio no capturados" y el
   reporte se genera.

### 6. Tarjeta "Período de evaluación" (US9)

1. Con licencia de evaluación, abrir Inicio con la ventana maximizada y en 1024×768 → la tarjeta
   mide lo mismo que las demás de su fila y "30 días restantes" se ve completo.
2. Ajustar la fecha de la licencia de prueba para tener 1 y 0 días (o usar la base de ejemplo
   correspondiente):
   - la altura no cambia;
   - los avisos largos se recortan con "…";
   - el texto completo aparece al pasar el puntero.

### 7. Configuración y Acerca de (US10)

1. Como Administrador, abrir Configuración → muestra Datos del negocio, Impresora, Seguridad y
   **Probar escáner**. Ayuda → solo "Acerca de".
2. Como Cajero → Configuración aparece **solo con Probar escáner**, y la pantalla abre y lee
   códigos.
3. Abrir "Acerca de":
   - muestra versión, ID de máquina, exportar diagnóstico (Administrador) y administración de
     licencia (Administrador);
   - no muestra la carpeta de datos, el sistema operativo ni "Probar escáner".
4. Exportar el diagnóstico → el ZIP sigue incluyendo la carpeta de datos y el sistema operativo.

## Criterio de terminado

- Todos los escenarios anteriores cumplen.
- Las compilaciones y las pruebas terminan sin advertencias.
- `docs/` está actualizado (ver plan, Principio VIII).
