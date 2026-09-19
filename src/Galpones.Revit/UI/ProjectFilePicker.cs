using System.IO;
using Microsoft.Win32;

namespace Galpones.Revit.UI;

public static class ProjectFilePicker
{
    public const string CarpetaFamiliasNombre = "familias";

    public static string? PedirArchivoDeProyecto()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar proyecto.yaml",
            Filter = "YAML (*.yaml;*.yml)|*.yaml;*.yml|Todos los archivos (*.*)|*.*",
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>La biblioteca de familias vive en familias/ junto al proyecto.yaml.</summary>
    public static string CarpetaFamilias(string yamlPath)
        => Path.Combine(Path.GetDirectoryName(yamlPath)!, CarpetaFamiliasNombre);
}
