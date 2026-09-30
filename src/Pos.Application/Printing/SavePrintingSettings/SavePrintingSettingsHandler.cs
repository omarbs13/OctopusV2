using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Printing.SavePrintingSettings;

/// <summary>Guarda la configuración local de impresión. Sin impresora con impresión automática no es un error.</summary>
public sealed class SavePrintingSettingsHandler
{
    private readonly IAccessControl _access;
    private readonly IPrintingSettingsStore _store;

    public SavePrintingSettingsHandler(IAccessControl access, IPrintingSettingsStore store)
    {
        _access = access;
        _store = store;
    }

    public async Task<Result> HandleAsync(PrintingSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var access = await _access.CheckAsync(Permission.ManageSettings, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        var name = string.IsNullOrWhiteSpace(settings.PrinterName) ? null : settings.PrinterName.Trim();
        _store.Save(settings with { PrinterName = name });
        return Result.Success();
    }
}
