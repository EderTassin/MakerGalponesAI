using Autodesk.Revit.DB;

namespace Galpones.Revit.Families;

/// <summary>
/// Resuelve FamilySymbol estructurales desde el documento activo. Como las bibliotecas
/// de contenido no siempre están instaladas, no se cargan .rfa externos: se busca un
/// tipo existente (preferentemente por nombre) o se duplica uno con las dimensiones pedidas.
/// Requiere un documento con familias estructurales (p. ej. plantilla "Structural Analysis").
/// </summary>
public static class FamilySymbolResolver
{
    public const double ColumnaDefaultAnchoM = 0.30;
    public const double ColumnaDefaultAltoM = 0.30;
    public const double VigaDefaultAnchoM = 0.20;
    public const double VigaDefaultAltoM = 0.40;

    private const string PlantillaSugerida = "Structural Analysis-DefaultESPESP.rte";

    public static FamilySymbol FindOrCreateColumnSymbol(Document doc, string? nombrePreferido, double anchoM, double altoM)
        => FindOrCreateSymbol(doc, BuiltInCategory.OST_StructuralColumns, "columna estructural",
            nombrePreferido, anchoM, altoM);

    public static FamilySymbol FindOrCreateBeamSymbol(Document doc, string? nombrePreferido, double anchoM, double altoM)
        => FindOrCreateSymbol(doc, BuiltInCategory.OST_StructuralFraming, "viga estructural",
            nombrePreferido, anchoM, altoM);

    /// <summary>Tipos disponibles en el documento, con formato "Familia: Tipo", para mostrar en el selector.</summary>
    public static IReadOnlyList<string> ListAvailableSymbols(Document doc, BuiltInCategory category)
        => new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(category)
            .Cast<FamilySymbol>()
            .Select(s => $"{s.Family.Name}: {s.Name}")
            .Distinct()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static FamilySymbol FindOrCreateSymbol(
        Document doc, BuiltInCategory category, string descripcion,
        string? nombrePreferido, double anchoM, double altoM)
    {
        var symbols = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(category)
            .Cast<FamilySymbol>()
            .ToList();

        if (symbols.Count == 0)
        {
            throw new InvalidOperationException(
                $"El documento no tiene ninguna familia de {descripcion} cargada. " +
                $"Abrí el proyecto desde la plantilla '{PlantillaSugerida}' o cargá una familia de {descripcion} antes de generar.");
        }

        if (!string.IsNullOrWhiteSpace(nombrePreferido))
        {
            var preferido = symbols.FirstOrDefault(s =>
                string.Equals(s.Name, nombrePreferido, StringComparison.OrdinalIgnoreCase) ||
                $"{s.Family.Name}: {s.Name}".Equals(nombrePreferido, StringComparison.OrdinalIgnoreCase));
            if (preferido is not null)
                return EnsureActive(preferido);

            throw new InvalidOperationException(
                $"No se encontró el tipo de {descripcion} '{nombrePreferido}' en el documento. " +
                $"Tipos disponibles: {string.Join(", ", symbols.Select(s => $"'{s.Family.Name}: {s.Name}'").Distinct())}.");
        }

        var nuevoNombre = $"Galpon {anchoM * 1000:0}x{altoM * 1000:0}";
        var existente = symbols.FirstOrDefault(s => s.Name.Equals(nuevoNombre, StringComparison.OrdinalIgnoreCase));
        if (existente is not null)
            return EnsureActive(existente);

        var duplicado = (FamilySymbol)symbols[0].Duplicate(nuevoNombre);
        SetParameterSiExiste(duplicado, anchoM, "b", "Width", "Ancho");
        SetParameterSiExiste(duplicado, altoM, "h", "Depth", "Height", "Alto");
        return EnsureActive(duplicado);
    }

    private static FamilySymbol EnsureActive(FamilySymbol symbol)
    {
        if (!symbol.IsActive)
            symbol.Activate();
        return symbol;
    }

    private static void SetParameterSiExiste(FamilySymbol symbol, double valorMetros, params string[] nombresPosibles)
    {
        var valorFt = UnitUtils.ConvertToInternalUnits(valorMetros, UnitTypeId.Meters);
        foreach (var nombre in nombresPosibles)
        {
            var param = symbol.LookupParameter(nombre);
            if (param is { IsReadOnly: false } && param.StorageType == StorageType.Double)
            {
                param.Set(valorFt);
                return;
            }
        }
    }
}
