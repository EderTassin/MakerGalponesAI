namespace Galpones.Core.IntermediateModel;

/// <summary>
/// Modelo intermedio independiente de Revit: coordenadas en metros.
/// El adaptador Revit lo traduce a Level/Grid/... en unidades internas de Revit.
/// </summary>
public sealed class GalponModel
{
    public IReadOnlyList<LevelDef> Levels { get; init; } = [];
    public IReadOnlyList<GridDef> Grids { get; init; } = [];
}

public sealed record LevelDef(string Nombre, double ElevacionMetros);

public enum GridDirection
{
    /// <summary>La línea corre paralela al eje X, en una posición Y fija (ejes con letra: A, B...).</summary>
    X,
    /// <summary>La línea corre paralela al eje Y, en una posición X fija (ejes numerados: 1, 2, 3...).</summary>
    Y,
}

/// <summary>
/// <paramref name="PosicionMetros"/> es la coordenada fija (Y si Direction=X, X si Direction=Y).
/// <paramref name="LongitudMetros"/> es el largo de la línea a lo largo de su dirección.
/// </summary>
public sealed record GridDef(string Nombre, GridDirection Direction, double PosicionMetros, double LongitudMetros);
