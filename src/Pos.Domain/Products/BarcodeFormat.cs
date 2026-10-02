namespace Pos.Domain.Products;

/// <summary>Formato de una lectura de código de barras (021, research §4).</summary>
public enum BarcodeFormat
{
    /// <summary>Lectura vacía después de normalizar.</summary>
    Empty,

    /// <summary>13 dígitos con dígito verificador correcto.</summary>
    Ean13,

    /// <summary>8 dígitos con dígito verificador correcto.</summary>
    Ean8,

    /// <summary>Cumple el juego de caracteres y el largo, y no es un EAN con dígito incorrecto.</summary>
    Code128OrCode39,

    /// <summary>Caracteres no admitidos, más de 48, o 8/13 dígitos con dígito verificador incorrecto.</summary>
    Unrecognized,
}
