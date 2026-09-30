namespace Pos.Application.Abstractions;

public interface IClock
{
    /// <summary>Fecha y hora actual en UTC.</summary>
    DateTime UtcNow { get; }
}
