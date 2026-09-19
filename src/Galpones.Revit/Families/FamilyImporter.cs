using System.IO;
using Autodesk.Revit.DB;

namespace Galpones.Revit.Families;

/// <summary>
/// Importa familias .rfa de la carpeta familias/ del proyecto al documento Revit.
/// La versión de la carpeta manda: si la familia ya existe, se recarga sobrescribiendo
/// los valores de los parámetros de tipo.
/// Debe llamarse dentro de una transacción abierta por el llamador.
/// </summary>
public static class FamilyImporter
{
    public sealed record ImportResult(
        bool CarpetaEncontrada,
        int Nuevas,
        int Actualizadas,
        IReadOnlyList<string> Errores)
    {
        public bool HuboCambios => Nuevas + Actualizadas > 0;

        public string Resumen(string carpeta)
        {
            if (!CarpetaEncontrada)
                return $"No existe la carpeta de familias:\n{carpeta}\n\nCreala junto al proyecto.yaml y pone ahí los .rfa del estudio.";

            var lineas = new List<string> { $"Nuevas: {Nuevas}. Actualizadas: {Actualizadas}." };
            if (Nuevas + Actualizadas == 0 && Errores.Count == 0)
                lineas.Add($"No se encontraron archivos .rfa en:\n{carpeta}");
            if (Errores.Count > 0)
                lineas.Add("Errores:\n" + string.Join("\n", Errores));
            return string.Join("\n", lineas);
        }
    }

    public static ImportResult ImportarDesdeCarpeta(Document doc, string carpetaFamilias)
    {
        if (!Directory.Exists(carpetaFamilias))
            return new ImportResult(false, 0, 0, []);

        var archivos = Directory
            .EnumerateFiles(carpetaFamilias, "*.rfa", SearchOption.AllDirectories)
            .OrderBy(a => a)
            .ToList();
        if (archivos.Count == 0)
            return new ImportResult(true, 0, 0, []);

        var familiasExistentes = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nuevas = 0;
        var actualizadas = 0;
        var errores = new List<string>();
        var options = new OverwriteFamilyLoadOptions();

        foreach (var archivo in archivos)
        {
            var nombreBase = Path.GetFileNameWithoutExtension(archivo);
            try
            {
                var yaExistia = familiasExistentes.Contains(nombreBase);
                if (doc.LoadFamily(archivo, options, out var family))
                {
                    if (yaExistia || familiasExistentes.Contains(family.Name))
                        actualizadas++;
                    else
                        nuevas++;
                    familiasExistentes.Add(family.Name);
                }
                else
                {
                    errores.Add($"{nombreBase}: LoadFamily devolvió false");
                }
            }
            catch (Exception ex)
            {
                errores.Add($"{nombreBase}: {ex.Message}");
            }
        }

        return new ImportResult(true, nuevas, actualizadas, errores);
    }

    private sealed class OverwriteFamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(
            Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
