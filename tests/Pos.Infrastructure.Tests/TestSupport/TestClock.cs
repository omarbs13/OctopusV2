using Pos.Application.Abstractions;

namespace Pos.Infrastructure.Tests.TestSupport;

public sealed class TestClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 29, 15, 30, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan span) => UtcNow = UtcNow.Add(span);
}

public sealed class TestCurrentUser : ICurrentUser
{
    public Guid UserId { get; set; } = Guid.Parse("11111111-1111-7111-8111-111111111111");
}
