namespace Pos.Desktop.Forms;

/// <summary>Cómo se muestra un formulario; lo decide quien lo abre, no el formulario (FR-023).</summary>
public enum FormPresentation
{
    /// <summary>Hasta 8 campos, sin pestañas ni listas internas: panel lateral sobre el listado.</summary>
    SidePanel,

    /// <summary>Formularios grandes: ocupan el área de contenido con el menú visible.</summary>
    FullScreen,
}

/// <summary>Respuesta del operador al salir de un formulario con cambios sin guardar.</summary>
public enum UnsavedChangesChoice
{
    Save,
    Discard,
    KeepEditing,
}
