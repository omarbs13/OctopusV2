using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pos.Infrastructure.Persistence;

/// <summary>Fábrica para las herramientas de diseño (dotnet ef).</summary>
internal sealed class PosDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext(string[] args)
    {
        var path = Path.Combine(Path.GetTempPath(), "pos-design-time.db");
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new PosDbContext(options);
    }
}
