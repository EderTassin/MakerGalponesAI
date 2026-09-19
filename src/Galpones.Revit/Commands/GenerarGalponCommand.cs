using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Galpones.Core.Input;
using Galpones.Core.IntermediateModel;
using Microsoft.Win32;

namespace Galpones.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public sealed class GenerarGalponCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var yamlPath = PedirArchivoDeProyecto();
        if (yamlPath is null)
            return Result.Cancelled;

        ProjectInput input;
        try
        {
            input = ProjectInputLoader.LoadFromFile(yamlPath);
        }
        catch (ProjectInputValidationException ex)
        {
            TaskDialog.Show("Galpones", "El proyecto.yaml tiene errores:\n\n" + string.Join("\n", ex.Errors));
            return Result.Failed;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }

        var model = GalponModelGenerator.Generate(input);

        using var tx = new Transaction(doc, "Generar galpón");
        tx.Start();
        try
        {
            var niveles = CrearNiveles(doc, model.Levels);
            var ejes = CrearGrillas(doc, model.Grids);
            tx.Commit();

            TaskDialog.Show(
                "Galpones",
                $"Listo. Se crearon {niveles} niveles y {ejes} ejes a partir de {Path.GetFileName(yamlPath)}.");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            tx.RollBack();
            message = ex.Message;
            return Result.Failed;
        }
    }

    private static string? PedirArchivoDeProyecto()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar proyecto.yaml",
            Filter = "YAML (*.yaml;*.yml)|*.yaml;*.yml|Todos los archivos (*.*)|*.*",
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static int CrearNiveles(Document doc, IReadOnlyList<LevelDef> levels)
    {
        foreach (var level in levels)
        {
            var elevacionFt = UnitUtils.ConvertToInternalUnits(level.ElevacionMetros, UnitTypeId.Meters);
            var revitLevel = Level.Create(doc, elevacionFt);
            revitLevel.Name = level.Nombre;
        }

        return levels.Count;
    }

    private static int CrearGrillas(Document doc, IReadOnlyList<GridDef> grids)
    {
        foreach (var grid in grids)
        {
            var posicionFt = UnitUtils.ConvertToInternalUnits(grid.PosicionMetros, UnitTypeId.Meters);
            var longitudFt = UnitUtils.ConvertToInternalUnits(grid.LongitudMetros, UnitTypeId.Meters);

            var linea = grid.Direction == GridDirection.Y
                ? Line.CreateBound(new XYZ(posicionFt, 0, 0), new XYZ(posicionFt, longitudFt, 0))
                : Line.CreateBound(new XYZ(0, posicionFt, 0), new XYZ(longitudFt, posicionFt, 0));

            var revitGrid = Grid.Create(doc, linea);
            revitGrid.Name = grid.Nombre;
        }

        return grids.Count;
    }
}
