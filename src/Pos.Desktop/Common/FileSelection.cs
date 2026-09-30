namespace Pos.Desktop.Common;

/// <summary>Archivo elegido en el selector del sistema.</summary>
/// <param name="Name">Nombre del archivo, para mensajes y registro.</param>
/// <param name="Length">Tamaño en bytes.</param>
/// <param name="OpenAsync">Abre el archivo para lectura; el llamador cierra el flujo.</param>
public sealed record FileSelection(string Name, long Length, Func<Task<Stream>> OpenAsync);

/// <summary>Filtro del selector de archivos, por ejemplo "Imágenes" con "*.jpg" y "*.png".</summary>
public sealed record FileTypeFilter(string Name, IReadOnlyList<string> Patterns);
