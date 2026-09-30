namespace Pos.Domain.Common;

/// <summary>
/// Violación de una invariante del dominio. Es la última barrera: Application valida antes,
/// así que en operación normal no debería ocurrir.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException()
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
