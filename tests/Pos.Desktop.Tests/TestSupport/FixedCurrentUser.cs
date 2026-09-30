using Pos.Application.Abstractions;

namespace Pos.Desktop.Tests.TestSupport;

public sealed class FixedCurrentUser : ICurrentUser
{
    public static readonly Guid Id = Guid.Parse("22222222-2222-7222-8222-222222222222");

    public Guid UserId => Id;
}
