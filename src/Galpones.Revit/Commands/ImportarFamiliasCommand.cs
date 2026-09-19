using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Galpones.Revit.Families;
using Galpones.Revit.UI;

namespace Galpones.Revit.Commands;

/// <summary>
/// Carga en el documento todas las familias .rfa de la carpeta familias/ del proyecto.
/// La versión de la carpeta manda: las familias existentes se recargan sobrescribiendo tipos.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public sealed class ImportarFamiliasCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;

        var yamlPath = ProjectFilePicker.PedirArchivoDeProyecto();
        if (yamlPath is null)
            return Result.Cancelled;

        var carpetaFamilias = ProjectFilePicker.CarpetaFamilias(yamlPath);

        FamilyImporter.ImportResult resultado;
        using (var tx = new Transaction(doc, "Importar familias"))
        {
            tx.Start();
            resultado = FamilyImporter.ImportarDesdeCarpeta(doc, carpetaFamilias);
            if (resultado.HuboCambios)
                tx.Commit();
            else
                tx.RollBack();
        }

        TaskDialog.Show("Galpones — Importar familias", resultado.Resumen(carpetaFamilias));
        return Result.Succeeded;
    }
}
