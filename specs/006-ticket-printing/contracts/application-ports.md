# Contrato: casos de uso y puertos de Application

Interfaz que la capa de escritorio consume y que Infrastructure implementa. Las firmas son
orientativas; los nombres exactos se fijan al implementar.

## Casos de uso (Pos.Application)

Todos devuelven `Result` / `Result<T>` con un error de negocio; ninguno lanza por una falla de
dispositivo.

| Caso de uso | Entrada | Salida | Errores |
|---|---|---|---|
| `GetBusinessProfile` | — | `BusinessProfileDto?` (con logotipo) | — |
| `SaveBusinessProfile` | nombre comercial, dirección, teléfono, RFC?, pie?, logotipo (`LogoChange`: conservar, quitar, nuevo archivo) | `Result` | `ValidationFailed` por campo (obligatorio, longitud, logotipo inválido o mayor a 1 MB); el logotipo anterior se conserva |
| `GetPrintingSettings` / `SavePrintingSettings` | `PrintingSettings` | `PrintingSettings` / `Result` | — |
| `ListPrinters` | — | `IReadOnlyList<string>` de impresoras del sistema | Lista vacía si el catálogo falla |
| `PrintTicket` | `PrintTicketCommand(Source, IsReprint)` donde `Source` = `Sale(SaleId)` \| `Sample` | `Result<PrintedTicket>` (destino: impresora o ruta del archivo) | `NotConfigured`, `PrinterUnavailable`, `PrintFailed`, `NotFound` |
| `OpenCashDrawer` (cobro) | `OpenCashDrawerCommand.ForSale(SaleId)` | `Result` | `NotConfigured`, `DrawerFailed`; sin auditoría |
| `OpenCashDrawer` (sin venta) | `OpenCashDrawerCommand.Manual(Reason)` | `Result` | `ValidationFailed` (motivo vacío, sin abrir); `DrawerFailed` (audita el fallo) |

Reglas:

- `PrintTicket` carga la venta con `GetSale`, el perfil con `IBusinessProfileRepository` y arma el
  `TicketDocument` con `TicketBuilder`; no escribe en la base.
- `OpenCashDrawer` manual escribe `DRAWER_OPENED` en la bitácora tanto en éxito como en fallo y
  guarda con `IAuditLog.SaveAsync`.
- El motivo se recorta y se limita a 200 caracteres.

## Puertos nuevos (definidos en Application)

```csharp
public interface IBusinessProfileRepository
{
    Task<BusinessProfile?> GetAsync(CancellationToken ct);   // con seguimiento
    void Add(BusinessProfile profile);
    Task<SaveOutcome> SaveChangesAsync(CancellationToken ct);
}

public interface IPrinterCatalog
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken ct);
}

public interface ITicketPrinter
{
    /// Imprime o guarda el ticket según la configuración vigente.
    Task<PrintOutcome> PrintAsync(TicketDocument ticket, PrintingSettings settings, CancellationToken ct);
}

public interface ICashDrawer
{
    Task<DrawerOutcome> OpenAsync(PrintingSettings settings, CancellationToken ct);
}

public interface IPrintingSettingsStore
{
    PrintingSettings Load();
    void Save(PrintingSettings settings);
}
```

`PrintOutcome` y `DrawerOutcome` son registros con éxito, destino (impresora o ruta) y un código
de falla (`NotConfigured`, `Unavailable`, `IoError`). Nunca llevan excepciones hacia la interfaz.

## Cambio a un puerto existente

```csharp
public interface IAuditLog
{
    void Add(string action, string entityType, Guid entityId, string? details);
    Task SaveAsync(CancellationToken ct);   // nuevo: persiste lo agregado cuando no hay otra escritura
}
```

## Implementaciones (Pos.Infrastructure)

| Puerto | Implementación | Notas |
|---|---|---|
| `ITicketPrinter` | `PlatformTicketPrinter` | Impresora virtual → `FileTicketPrinter`; si no, `EscPosEncoder` + transporte |
| `ICashDrawer` | `PlatformCashDrawer` | Pulso `ESC p`; con impresora virtual no envía nada |
| `IPrinterCatalog` | `PlatformPrinterCatalog` | `WinSpoolTransport` o `CupsTransport` |
| `IRawPrinterTransport` (interno) | `WinSpoolTransport`, `CupsTransport` | `PrintGate` serializa los trabajos |
| `IPrintingSettingsStore` | `PreferencesPrintingSettingsStore` | Clave `printing` de `IPreferencesStore` |
| `IBusinessProfileRepository` | `BusinessProfileRepository` | EF Core |

## Contrato de la interfaz (Pos.Desktop)

- Grupo de navegación **Configuración** con **Datos del negocio** e **Impresora** (con
  impresión de prueba).
- Punto de venta: tras `ConfirmSale`, si hay impresión automática se invoca `PrintTicket` en
  segundo plano; si algún pago es efectivo y `AutoOpenDrawer` está activo, `OpenCashDrawer`. Ante
  falla, un aviso con **Reintentar** y **Continuar sin imprimir**. Botón **Abrir cajón** con
  diálogo de motivo.
- Detalle de venta: botón **Reimprimir**.
