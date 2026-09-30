using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Pos.Application.Printing;

namespace Pos.Infrastructure.Printing.Windows;

/// <summary>Impresión RAW con el spooler de Windows (<c>winspool.drv</c>). Se verifica a mano (quickstart).</summary>
[SupportedOSPlatform("windows")]
public sealed partial class WinSpoolTransport : IRawPrinterTransport
{
    private const int EnumLocal = 0x2;
    private const int EnumConnections = 0x4;
    private const int ErrorInsufficientBuffer = 122;

    private readonly ILogger<WinSpoolTransport> _logger;

    public WinSpoolTransport(ILogger<WinSpoolTransport> logger) => _logger = logger;

    public Task<IReadOnlyList<string>> ListPrintersAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<string>>(List, cancellationToken);

    public Task<DeviceFailure?> SendAsync(string printerName, byte[] data, CancellationToken cancellationToken) =>
        Task.Run(() => Send(printerName, data), cancellationToken);

    private List<string> List()
    {
        var flags = EnumLocal | EnumConnections;
        _ = EnumPrinters(flags, null, 4, IntPtr.Zero, 0, out var needed, out _);
        if (needed <= 0)
        {
            return [];
        }

        var buffer = Marshal.AllocHGlobal(needed);
        try
        {
            if (!EnumPrinters(flags, null, 4, buffer, needed, out _, out var returned))
            {
                LogFailed("EnumPrinters", Marshal.GetLastWin32Error());
                return [];
            }

            var names = new List<string>();
            var size = Marshal.SizeOf<PrinterInfo4>();
            for (var i = 0; i < returned; i++)
            {
                var info = Marshal.PtrToStructure<PrinterInfo4>(buffer + (i * size));
                if (Marshal.PtrToStringUni(info.PrinterName) is { Length: > 0 } name)
                {
                    names.Add(name);
                }
            }

            return names;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private DeviceFailure? Send(string printerName, byte[] data)
    {
        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero))
        {
            LogFailed("OpenPrinter", Marshal.GetLastWin32Error());
            return DeviceFailure.Unavailable;
        }

        var docStarted = false;
        var pageStarted = false;
        try
        {
            var doc = new DocInfo1 { DocName = "Ticket", OutputFile = null, DataType = "RAW" };
            if (StartDocPrinter(handle, 1, ref doc) == 0)
            {
                LogFailed("StartDocPrinter", Marshal.GetLastWin32Error());
                return DeviceFailure.IoError;
            }

            docStarted = true;
            if (!StartPagePrinter(handle))
            {
                LogFailed("StartPagePrinter", Marshal.GetLastWin32Error());
                return DeviceFailure.IoError;
            }

            pageStarted = true;
            if (!WritePrinter(handle, data, data.Length, out var written) || written != data.Length)
            {
                LogFailed("WritePrinter", Marshal.GetLastWin32Error());
                return DeviceFailure.IoError;
            }

            return null;
        }
        finally
        {
            if (pageStarted)
            {
                EndPagePrinter(handle);
            }

            if (docStarted)
            {
                EndDocPrinter(handle);
            }

            ClosePrinter(handle);
        }
    }

    private void LogFailed(string call, int error) =>
        LogWin32(call, error, error == 0 || error == ErrorInsufficientBuffer ? string.Empty : new Win32Exception(error).Message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "winspool falló en {Call}. Error={Error} {Message}")]
    private partial void LogWin32(string call, int error, string message);

    [StructLayout(LayoutKind.Sequential)]
    private struct PrinterInfo4
    {
        public IntPtr PrinterName;
        public IntPtr ServerName;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo1
    {
        public string DocName;
        public string? OutputFile;
        public string DataType;
    }

    [DllImport("winspool.drv", EntryPoint = "EnumPrintersW", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool EnumPrinters(int flags, string? name, int level, IntPtr printerEnum, int size, out int needed, out int returned);

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool OpenPrinter(string printerName, out IntPtr handle, IntPtr defaults);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int StartDocPrinter(IntPtr handle, int level, ref DocInfo1 docInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool StartPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool WritePrinter(IntPtr handle, byte[] buffer, int count, out int written);

    [DllImport("winspool.drv", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool EndPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool EndDocPrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool ClosePrinter(IntPtr handle);
}
