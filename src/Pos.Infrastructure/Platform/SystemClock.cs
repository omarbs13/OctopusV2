using Pos.Application.Abstractions;

namespace Pos.Infrastructure.Platform;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
