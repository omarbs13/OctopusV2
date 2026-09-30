namespace Pos.Desktop.CashShifts;

/// <summary>Estado del turno de la caja para el Punto de venta (contracts/ui.md).</summary>
public enum ShiftViewState
{
    /// <summary>Aún no se consulta.</summary>
    Unknown,

    /// <summary>No hay turno abierto: se pide abrirlo.</summary>
    None,

    /// <summary>El turno abierto es de otro usuario: no se puede vender.</summary>
    Other,

    /// <summary>El turno abierto es del usuario actual.</summary>
    Own,
}
