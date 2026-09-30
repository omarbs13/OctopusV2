# Contrato: pantalla de carga

## Secuencia

```text
Program.Main (instancia única, logging)  →  App: muestra SplashWindow
  → StartupPresenter.RunAsync(progress)     // la pantalla de carga es la dueña de los diálogos
       DatabaseStartup.RunAsync(progress)
  → ok: espera hasta completar al menos 800 ms → abre MainWindow en Inicio → cierra SplashWindow
  → error: el diálogo de la fundación (dueño: SplashWindow) → cierra SplashWindow → termina
```

## StartupStep → texto

| Paso | Texto |
|---|---|
| (inicial) | "Iniciando…" |
| `CheckingDatabase` | "Verificando la base de datos…" |
| `BackingUp` | "Respaldando la base de datos…" |
| `Migrating` | "Actualizando la base de datos…" |
| `Restoring` | "Restaurando el respaldo…" |
| `Finishing` | "Preparando…" |

`DatabaseStartup` informa cada paso **antes** de ejecutarlo. El respaldo automático también se
informa como `BackingUp`.

## Contenido de la ventana

Logotipo (máximo 160 × 160, proporción conservada), nombre "POS", versión, barra de progreso
indeterminada y texto del paso. Sin bordes, centrada y fuera de la barra de tareas.
