namespace Pos.Application.Licensing;

/// <summary>Evidencia de uso previo: cuándo se creó el primer usuario (011, research §5).</summary>
public interface IInstallationAgeReader
{
    Task<DateTime?> GetFirstUserCreatedUtcAsync(CancellationToken cancellationToken);
}
