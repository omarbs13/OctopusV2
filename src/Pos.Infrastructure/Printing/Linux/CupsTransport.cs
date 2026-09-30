using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pos.Application.Printing;

namespace Pos.Infrastructure.Printing.Linux;

/// <summary>
/// Impresión RAW con CUPS: <c>lpstat -e</c> para listar y <c>lp -d &lt;impresora&gt; -o raw</c> para
/// enviar. Los argumentos van en <see cref="ProcessStartInfo.ArgumentList"/>, nunca en una cadena de
/// shell, para que el nombre de la impresora no pueda inyectar comandos.
/// </summary>
public sealed partial class CupsTransport : IRawPrinterTransport
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly ILogger<CupsTransport> _logger;

    public CupsTransport(ILogger<CupsTransport> logger) => _logger = logger;

    public async Task<IReadOnlyList<string>> ListPrintersAsync(CancellationToken cancellationToken)
    {
        var result = await RunAsync(["lpstat", "-e"], null, cancellationToken);
        if (result.ExitCode != 0)
        {
            return [];
        }

        return [.. result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    public async Task<DeviceFailure?> SendAsync(string printerName, byte[] data, CancellationToken cancellationToken)
    {
        var result = await RunAsync(["lp", "-d", printerName, "-o", "raw"], data, cancellationToken);
        if (result.ExitCode == 0)
        {
            return null;
        }

        LogFailed(printerName, result.ExitCode, result.Error.Trim());
        return DeviceFailure.Unavailable;
    }

    private async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string[] command,
        byte[]? input,
        CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(command[0])
        {
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in command.Skip(1))
        {
            info.ArgumentList.Add(argument);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        Process? process = null;
        try
        {
            process = Process.Start(info);
            if (process is null)
            {
                return (-1, string.Empty, string.Empty);
            }

            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            if (input is not null)
            {
                await process.StandardInput.BaseStream.WriteAsync(input, timeout.Token);
                process.StandardInput.Close();
            }

            await process.WaitForExitAsync(timeout.Token);
            return (process.ExitCode, await output, await error);
        }
        catch (Win32Exception ex)
        {
            // lp o lpstat no están instalados: se trata como "sin impresoras".
            LogMissing(ex, command[0]);
            return (-1, string.Empty, string.Empty);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException && !cancellationToken.IsCancellationRequested)
        {
            LogTimeout(ex, command[0]);
            TryKill(process);
            return (-1, string.Empty, string.Empty);
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static void TryKill(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // El proceso ya terminó.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "CUPS no disponible: no se pudo ejecutar {Command}")]
    private partial void LogMissing(Exception exception, string command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "CUPS no respondió a tiempo al ejecutar {Command}")]
    private partial void LogTimeout(Exception exception, string command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "lp falló. Impresora={Printer} Código={ExitCode} {Error}")]
    private partial void LogFailed(string printer, int exitCode, string error);
}
