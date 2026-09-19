namespace Galpones.Core.IntermediateModel;

/// <summary>
/// Modelo intermedio independiente de Revit: coordenadas en metros.
/// El adaptador Revit lo traduce a Level/Grid/... en unidades internas de Revit.
/// </summary>
public sealed class GalponModel
{
    public IReadOnlyList<LevelDef> Levels { get; init; } = [];
    public IReadOnlyList<GridDef> Grids { get; init; } = [];
    public IReadOnlyList<ColumnaDef> Columnas { get; init; } = [];
    public IReadOnlyList<VigaDef> Vigas { get; init; } = [];

    /// <summary>Pendiente de cubierta aplicada, en % (con default ya resuelto).</summary>
    public double PendienteCubiertaPct { get; init; }

    /// <summary>Tipo de columna preferido en Revit, si el input lo especificó.</summary>
    public string? PerfilColumna { get; init; }

    /// <summary>Tipo de viga preferido en Revit, si el input lo especificó.</summary>
    public string? PerfilViga { get; init; }
}

public sealed record LevelDef(string Nombre, double ElevacionMetros);

/// <summary>Columna con base en el nivel 0 y tope en el nivel Cubierta, en la posición (X, Y) de planta.</summary>
public sealed record ColumnaDef(double X, double Y);

/// <summary>Viga inclinada (caballéte) entre dos puntos 3D; nivel de referencia: Cubierta.</summary>
public sealed record VigaDef(Punto3D Inicio, Punto3D Fin);

public sealed record Punto3D(double X, double Y, double Z);

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
