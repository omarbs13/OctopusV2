namespace Pos.Application.Abstractions;

/// <summary>Usuario que realiza la operación; se registra en la auditoría.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}
