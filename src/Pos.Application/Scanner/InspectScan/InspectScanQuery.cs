namespace Pos.Application.Scanner.InspectScan;

/// <summary>Lectura de la pantalla "Probar escáner" (021), sin el terminador.</summary>
public sealed record InspectScanQuery(string RawText);
