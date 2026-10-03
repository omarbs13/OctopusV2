using Pos.Application.Abstractions;
using Pos.Application.Diagnostics;
using Pos.Application.Licensing;
using Pos.Desktop.Common;

namespace Pos.Desktop.Tests.TestSupport;

public sealed class FakeAppInfo : IAppInfo
{
    public string Version => "0.1.0";

    public string OperatingSystem => "Linux de prueba";
}

public sealed class FakeMachineId : IMachineIdProvider
{
    public const string Id = "abc123maquina";

    public string GetMachineId() => Id;
}

public sealed class FakeDiagnosticsExporter : IDiagnosticsExporter
{
    public string? Destination { get; private set; }

    public Exception? FailWith { get; set; }

    /// <summary>Si se asigna, la exportación espera a que se complete.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public bool? IncludeDatabase { get; private set; }

    public int LogFileCount { get; set; } = 1;

    public async Task<int> ExportAsync(string destinationFile, bool includeDatabase, CancellationToken cancellationToken)
    {
        if (Gate is not null)
        {
            await Gate.Task;
        }

        if (FailWith is not null)
        {
            throw FailWith;
        }

        Destination = destinationFile;
        IncludeDatabase = includeDatabase;
        return LogFileCount;
    }
}

public sealed class FakeClipboard : IClipboardService
{
    public string? Text { get; private set; }

    public Task SetTextAsync(string text)
    {
        Text = text;
        return Task.CompletedTask;
    }
}
