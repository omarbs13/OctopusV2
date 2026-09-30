namespace Pos.Domain.CashShifts;

/// <summary>
/// En esta fase hay una sola caja por instalación. <c>RegisterCode</c> es la única preparación para
/// varias cajas (plan, Principio VII).
/// </summary>
public static class CashRegister
{
    public const string Default = "CAJA-1";

    public const string DisplayName = "Caja 1";
}
