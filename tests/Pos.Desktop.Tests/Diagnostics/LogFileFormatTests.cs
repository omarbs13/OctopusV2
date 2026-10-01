using Pos.Desktop.Composition;
using Pos.Desktop.Tests.Composition;
using Pos.Desktop.Diagnostics;
using Pos.Infrastructure.Platform;

namespace Pos.Desktop.Tests.Diagnostics;

/// <summary>FR-005 y FR-007: un archivo por día, legible, con fecha, nivel, mensaje, contexto y excepción.</summary>
public sealed class LogFileFormatTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void EntradaFatal_SeEscribeEnElArchivoDelDiaConNivelContextoYTraza()
    {
        var paths = new AppPaths(_directory.Path);
        paths.EnsureDirectories();
        var context = new DiagnosticContext();
        context.SetScreen("sales.pos");
        var logger = Logging.Create(paths, "1.2.3", context);

        try
        {
            throw new InvalidOperationException("falla de prueba");
        }
        catch (InvalidOperationException ex)
        {
            logger.ForContext("Password", "secreto").Fatal(ex, "Error inesperado en la operación {Operation}", "CobrarVenta");
        }

        ((IDisposable)logger).Dispose();
        var file = Assert.Single(Directory.GetFiles(paths.LogsDirectory, "pos-*.log"));
        Assert.Matches(@"pos-\d{8}\.log$", file);
        var text = File.ReadAllText(file);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} \[FATAL\] Error inesperado en la operación CobrarVenta ", text);
        Assert.Contains("System.InvalidOperationException: falla de prueba", text, StringComparison.Ordinal);
        Assert.Contains("\"AppVersion\":\"1.2.3\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secreto", text, StringComparison.Ordinal);
    }
}
