using Pos.Infrastructure.Diagnostics;

namespace Pos.Infrastructure.Tests.Diagnostics;

/// <summary>FR-006: los logs de más de 30 días se eliminan por fecha; el resto no se toca.</summary>
public sealed class LogRetentionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"pos-logs-{Guid.NewGuid():N}");
    private static readonly DateOnly Today = new(2026, 9, 30);

    public LogRetentionTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private void Create(string name) => File.WriteAllText(Path.Combine(_directory, name), "x");

    [Fact]
    public void ConservaHasta30DiasYEliminaLosMasAntiguos()
    {
        Create("pos-20260901.log"); // hace 29 días
        Create("pos-20260831.log"); // hace 30 días
        Create("pos-20260816.log"); // hace 45 días
        Create("otro.log");

        var deleted = LogRetention.Clean(_directory, Today);

        Assert.Equal(1, deleted);
        var left = Directory.GetFiles(_directory).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["otro.log", "pos-20260831.log", "pos-20260901.log"], left);
    }

    [Fact]
    public void CarpetaInexistente_NoLanza()
    {
        Assert.Equal(0, LogRetention.Clean(Path.Combine(_directory, "no-existe"), Today));
    }
}
