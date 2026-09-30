using Pos.Application.Abstractions;
using Pos.Application.Diagnostics;
using Pos.Application.Diagnostics.ExportDiagnostics;

namespace Pos.Application.Tests.Diagnostics;

public class ExportDiagnosticsHandlerTests
{
    private readonly FakeExporter _exporter = new();

    private ExportDiagnosticsHandler Handler => new(_exporter);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Exito_DevuelveLaRutaFinal()
    {
        var result = await Handler.HandleAsync(new ExportDiagnosticsCommand("/destino/diag.zip"), Ct);

        Assert.Equal("/destino/diag.zip", result.Value);
        Assert.Equal("/destino/diag.zip", _exporter.Destination);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task FallaDelExportador_DevuelveExportFailedComprensibleSinPropagar(Type exceptionType)
    {
        _exporter.FailWith = (Exception)Activator.CreateInstance(exceptionType, "detalle técnico")!;

        var result = await Handler.HandleAsync(new ExportDiagnosticsCommand("/destino/diag.zip"), Ct);

        var error = Assert.IsType<ExportFailed>(result.Error);
        Assert.Equal(ExportDiagnosticsHandler.ExportFailedMessage, error.Message);
        Assert.DoesNotContain("detalle técnico", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DestinoVacio_DevuelveValidationFailed(string destination)
    {
        var result = await Handler.HandleAsync(new ExportDiagnosticsCommand(destination), Ct);

        Assert.IsType<ValidationFailed>(result.Error);
        Assert.Null(_exporter.Destination);
    }

    private sealed class FakeExporter : IDiagnosticsExporter
    {
        public string? Destination { get; private set; }

        public Exception? FailWith { get; set; }

        public Task ExportAsync(string destinationFile, CancellationToken cancellationToken)
        {
            if (FailWith is not null)
            {
                throw FailWith;
            }

            Destination = destinationFile;
            return Task.CompletedTask;
        }
    }
}
