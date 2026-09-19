using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Galpones.Core.Input;
using Galpones.Core.IntermediateModel;
using Galpones.Revit.Families;
using Galpones.Revit.UI;
using DataStorage = Autodesk.Revit.DB.ExtensibleStorage.DataStorage;

namespace Galpones.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public sealed class GenerarGalponCommand : IExternalCommand
{
    private const string GenerationMarker = "MakerGalpones.Generation.v1";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        if (uiDoc is null || uiDoc.Document.IsFamilyDocument || uiDoc.Document.IsReadOnly)
        {
            TaskDialog.Show("MakerGalpones", "Abrí un proyecto Revit editable con una plantilla estructural antes de generar.");
            return Result.Cancelled;
        }
        var doc = uiDoc.Document;
        if (new FilteredElementCollector(doc).OfClass(typeof(DataStorage)).Any(e => e.Name == GenerationMarker))
        {
            TaskDialog.Show("MakerGalpones", "Este documento ya recibió una generación de MakerGalpones. La actualización de elementos todavía está pendiente. Usá otro documento para una nueva alternativa.");
            return Result.Cancelled;
        }

        var yamlPath = ProjectFilePicker.PedirArchivoDeProyecto();
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

        // El arquitecto confirma o cambia las familias antes de tocar el documento.
        var picker = new FamilyPickerWindow(
            doc, ProjectFilePicker.CarpetaFamilias(yamlPath), model.PerfilColumna, model.PerfilViga);
        if (picker.ShowDialogOwned() != true)
            return Result.Cancelled;
        var perfilColumna = picker.Selecciones["Columna"];
        var perfilViga = picker.Selecciones["Viga"];

        using var tx = new Transaction(doc, "Generar galpón");
        tx.Start();
        try
        {
            var niveles = CrearNiveles(doc, model.Levels);
            var ejes = CrearGrillas(doc, model.Grids);
            // Las columnas y vigas referencian niveles: hay que regenerar antes de usarlas.
            doc.Regenerate();

            var (columnas, vigas) = CrearEstructura(doc, model, niveles, perfilColumna, perfilViga);
            var marker = DataStorage.Create(doc);
            marker.Name = GenerationMarker;
            if (tx.Commit() != TransactionStatus.Committed)
            {
                message = "Revit no confirmó la transacción de generación. Revisá los mensajes del documento.";
                return Result.Failed;
            }

            TaskDialog.Show(
                "Galpones",
                $"Listo. Se crearon {niveles.Count} niveles, {ejes} ejes, {columnas} columnas y {vigas} vigas " +
                $"a partir de {Path.GetFileName(yamlPath)} (pendiente de cubierta {model.PendienteCubiertaPct:0.##}%).");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
            message = ex.Message;
            return Result.Failed;
        }
    }

    private static Dictionary<string, Level> CrearNiveles(Document doc, IReadOnlyList<LevelDef> levels)
    {
        var creados = new Dictionary<string, Level>();
        foreach (var level in levels)
        {
            var elevacionFt = UnitUtils.ConvertToInternalUnits(level.ElevacionMetros, UnitTypeId.Meters);
            var existing = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .FirstOrDefault(l => l.Name == level.Nombre);
            if (existing is not null)
            {
                if (Math.Abs(existing.Elevation - elevacionFt) > 0.000001)
                    throw new InvalidOperationException($"El nivel '{level.Nombre}' ya existe con otra elevación. Usá un documento nuevo para evitar alterar el proyecto existente.");
                creados[level.Nombre] = existing;
                continue;
            }
            var revitLevel = Level.Create(doc, elevacionFt);
            revitLevel.Name = level.Nombre;
            creados[level.Nombre] = revitLevel;
        }

        return creados;
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

    private static (int Columnas, int Vigas) CrearEstructura(
        Document doc, GalponModel model, IReadOnlyDictionary<string, Level> niveles,
        string perfilColumna, string perfilViga)
    {
        var nivel0 = niveles["Nivel 0"];
        var nivelCubierta = niveles["Cubierta"];

        var columnSymbol = FamilySymbolResolver.FindOrCreateColumnSymbol(
            doc, perfilColumna,
            FamilySymbolResolver.ColumnaDefaultAnchoM, FamilySymbolResolver.ColumnaDefaultAltoM);
        var beamSymbol = FamilySymbolResolver.FindOrCreateBeamSymbol(
            doc, perfilViga,
            FamilySymbolResolver.VigaDefaultAnchoM, FamilySymbolResolver.VigaDefaultAltoM);

        foreach (var columna in model.Columnas)
        {
            var ubicacion = new XYZ(Metros(columna.X), Metros(columna.Y), 0);
            var revitColumn = doc.Create.NewFamilyInstance(ubicacion, columnSymbol, nivel0, StructuralType.Column);
            revitColumn.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(nivelCubierta.Id);
            revitColumn.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(0.0);
        }

        foreach (var viga in model.Vigas)
        {
            var inicio = ToXyz(viga.Inicio);
            var fin = ToXyz(viga.Fin);
            var curva = Line.CreateBound(inicio, fin);
            // La curva 3D produce directamente el caballéte inclinado.
            doc.Create.NewFamilyInstance(curva, beamSymbol, nivelCubierta, StructuralType.Beam);
        }

        return (model.Columnas.Count, model.Vigas.Count);
    }

    private static double Metros(double valor) => UnitUtils.ConvertToInternalUnits(valor, UnitTypeId.Meters);

    private static XYZ ToXyz(Punto3D p) => new(Metros(p.X), Metros(p.Y), Metros(p.Z));
}
