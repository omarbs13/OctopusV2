using Pos.Application.Abstractions;

namespace Pos.Application.Tests.TestSupport;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 29, 15, 30, 0, DateTimeKind.Utc);
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid UserId { get; } = Guid.Parse("33333333-3333-7333-8333-333333333333");
}
