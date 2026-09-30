# Implementation Plan: Impresión de ticket, cajón de dinero y datos del negocio

**Branch**: `006-ticket-printing` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-ticket-printing/spec.md`

## Summary

Se entrega el comprobante impreso de cada venta y la apertura del cajón, sin que una falla del
dispositivo afecte la venta.

1. **Datos del negocio**: una fila única `BusinessProfile` en SQLite (nombre, dirección, teléfono,
   RFC, logotipo, pie). El logotipo se valida y normaliza con el `IImageProcessor` existente
   (research §6).
2. **Configuración de impresión por máquina**: JSON en la carpeta de preferencias mediante el
   `IPreferencesStore` existente. No va a la base de datos (research §2).
3. **Ticket en dos pasos**:
   - `Pos.Application` arma el ticket como una lista de líneas de texto ya ajustadas al ancho
     (32 o 48 columnas) con cantidad e importe alineados (research §3).
   - `Pos.Infrastructure` lo codifica a ESC/POS (CP858, corte, pulso del cajón, logotipo raster) o
     lo guarda como archivo de texto (impresora virtual) (research §4).
4. **Acceso a la impresora por sistema operativo**: Windows con `winspool.drv` (RAW) y Linux con
   CUPS (`lp -o raw`, `lpstat`). Sin paquetes NuGet nuevos (research §5).
5. **La venta primero**: el cobro registra la venta como hoy; la impresión corre después, en
   segundo plano, por una cola serial que conserva el orden. Una falla produce un aviso con
   reintento y nunca toca la venta (research §7).
6. **Cajón**: se abre al cobrar con efectivo (opción local). La apertura sin venta pide motivo y
   escribe `DRAWER_OPENED` en la bitácora, también si falla el intento (research §8).
7. **Reimpresión y cancelada**: botón en el detalle de la venta; leyendas "REIMPRESIÓN" y
   "CANCELADA" (research §9).

Hay una migración nueva (`BusinessProfile`) que solo crea una tabla. Ninguna dependencia externa
nueva.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12.1.3, CommunityToolkit.Mvvm, Hosting, Serilog,
EF Core 10 Sqlite, FluentValidation, SkiaSharp). No se agrega ninguna: la codificación CP858 viene
en el runtime (`CodePagesEncodingProvider`) y el acceso a impresoras usa P/Invoke y procesos del
sistema (research §5).

**Storage**:

- SQLite: tabla nueva `BusinessProfile` (una fila). Sin reconstrucción de tablas.
- Archivo JSON local `preferences/printing.json` para la configuración de impresión.
- Carpeta `tickets/` bajo la carpeta de datos para la impresora virtual.

**Testing**: xUnit v3, según la política mínima de la constitución v1.2.0:

- Application: armado del ticket (ajuste de texto, alineación, leyendas, pagos y cambio) y
  validación de datos del negocio.
- Handlers: la falla de impresión o de cajón no cambia la venta; la apertura sin venta audita
  también el intento fallido.
- SQLite real: persistencia de `BusinessProfile` y la bitácora del cajón.
- Obligatorias: migración de bases de ejemplo y pruebas de arquitectura.
- Sin pruebas de ViewModels, vistas ni de los adaptadores de hardware (se prueban a mano con la
  impresora virtual, ver [quickstart.md](quickstart.md)).

**Target Platform**: Windows 10+ y Linux (X11 o Wayland), con impresoras térmicas ESC/POS de 58 mm
y 80 mm.

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- El Punto de venta queda libre para la siguiente venta sin esperar a la impresora (SC-005).
- El ticket de 50 líneas se arma en menos de 50 ms.

**Constraints**:

- Funciona sin conexión.
- 0 advertencias.
- La impresión nunca bloquea, retrasa ni deshace la venta (FR-009).
- Los tickets consecutivos salen en orden y sin mezclarse.

**Scale/Scope**: 2 pantallas nuevas (Datos del negocio, Impresora), 1 diálogo (motivo de apertura),
botones en Punto de venta y en el detalle de venta, 1 tabla nueva.

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó tras el diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | La impresión y el cajón se ejecutan después de confirmar la venta y fuera de su transacción; toda excepción de dispositivo se captura, se registra y se muestra como aviso comprensible. Funciona sin red. |
| II. Capas | Puertos `ITicketPrinter`, `ICashDrawer` y `IPrinterCatalog` en Application; implementaciones por sistema operativo en Infrastructure. Desktop solo invoca casos de uso. |
| III. Lógica en el núcleo | El armado del ticket y las reglas de datos del negocio están en Application/Domain, no en ViewModels. |
| IV. Integridad de datos | `BusinessProfile` con GUID v7, fechas UTC, versión de concurrencia y auditoría por el interceptor. Migración EF Core nueva; el SQL se revisa; sin `HasData` (datos de la instalación). No se guarda el ticket. |
| V. Multiplataforma | Todo el hardware detrás de interfaces; rutas con APIs de .NET; el código de plataforma se selecciona en el arranque con `OperatingSystem.IsWindows()`. |
| VI. Calidad verificable | Pruebas solo de reglas con cálculo (ajuste y alineación de importes), validaciones de integridad y la auditoría; migración y arquitectura obligatorias. |
| VII. Simplicidad | Sin dependencias nuevas, sin librería ESC/POS de terceros (el subconjunto necesario son unos pocos comandos), sin cola persistente ni reintentos automáticos. |
| VIII. Soporte | Los fallos de impresión y cajón se registran en Serilog con operación, folio y nombre de impresora, sin datos sensibles. |
| IX. Seguridad local | La apertura sin venta queda en la bitácora con usuario, fecha, motivo y resultado. |

**Resultado**: sin violaciones; la tabla de Complexity Tracking no aplica.

## Project Structure

### Documentation (this feature)

```text
specs/006-ticket-printing/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── application-ports.md
│   └── ticket-format.md
└── tasks.md             # Lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Pos.Domain/
│   └── Business/
│       └── BusinessProfile.cs
├── Pos.Application/
│   ├── Abstractions/IAuditLog.cs                  # + SaveAsync (bitácora fuera de otra transacción)
│   ├── Business/
│   │   ├── IBusinessProfileRepository.cs
│   │   ├── BusinessProfileDto.cs
│   │   ├── GetBusinessProfile/
│   │   └── SaveBusinessProfile/                   # comando, validador, handler (logotipo)
│   ├── Printing/
│   │   ├── PrintingSettings.cs                    # impresora, ancho, automática, cajón
│   │   ├── IPrintingSettingsStore.cs
│   │   ├── IPrinterCatalog.cs                     # impresoras instaladas
│   │   ├── ITicketPrinter.cs
│   │   ├── ICashDrawer.cs
│   │   ├── Ticket/                                # TicketDocument, TicketBuilder, TextWrap
│   │   ├── GetPrintingSettings/ SavePrintingSettings/
│   │   ├── ListPrinters/
│   │   ├── PrintTicket/                           # venta, reimpresión, prueba
│   │   └── OpenCashDrawer/                        # automático y sin venta (con auditoría)
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── Business/BusinessProfileRepository.cs
│   ├── Persistence/Configurations/BusinessProfileConfiguration.cs
│   ├── Persistence/Migrations/…_BusinessProfile.cs
│   └── Printing/
│       ├── EscPosEncoder.cs                       # texto CP858, corte, pulso, logotipo raster
│       ├── FileTicketPrinter.cs                   # impresora virtual
│       ├── RawPrinterTransport.cs (interfaz interna)
│       ├── Windows/WinSpoolTransport.cs
│       ├── Linux/CupsTransport.cs
│       ├── PrintGate.cs                           # semáforo: un trabajo a la vez
│       └── PlatformPrinting.cs                    # elige la implementación por sistema operativo
├── Pos.Desktop/
│   └── Settings/                                  # BusinessProfile*, Printer*, DrawerReason*
│       (+ Sales/PointOfSale: cobro imprime y abre; botón "Abrir cajón"
│        + Sales/SaleDetail: botón "Reimprimir")
tests/
├── Pos.Domain.Tests/       # (sin cambios salvo BusinessProfile si tiene reglas)
├── Pos.Application.Tests/  Printing/ TicketBuilderTests, PrintTicketHandlerTests, OpenCashDrawerHandlerTests
│                           Business/ SaveBusinessProfileValidatorTests
└── Pos.Infrastructure.Tests/ Business/ BusinessProfilePersistenceTests, Audit de cajón
docs/impresion.md           # guía de soporte
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de las
funcionalidades 001 a 005. `Printing` y `Business` son carpetas nuevas en Application e
Infrastructure; la UI agrupa las dos pantallas nuevas en un grupo de navegación "Configuración".

## Complexity Tracking

Sin violaciones de la constitución; no aplica.
