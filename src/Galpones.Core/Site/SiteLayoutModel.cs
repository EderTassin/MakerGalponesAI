namespace Galpones.Core.Site;

/// <summary>
/// Rectángulo en el plano del lote, en metros. Origen (0,0) en la esquina frente-izquierda del
/// lote; X crece hacia el lado derecho del frente, Y crece desde el frente hacia el fondo.
/// </summary>
public sealed record RectDef(double X, double Y, double Ancho, double Profundidad);

/// <summary>Muelle de carga sobre el paño de fondo de la nave, mirando hacia el patio de maniobra.</summary>
public sealed record MuelleDef(string Nombre, double X, double Y);

/// <summary>
/// Implantación simulada de la nave en el lote: estacionamiento de autos, muelles y circulación
/// de transporte pesado. No reemplaza un plano de sitio ni una comprobación normativa (retiros,
/// radios de giro y accesos reales requieren revisión profesional; ver SIT-01/SIT-02).
/// </summary>
public sealed class SiteLayoutModel
{
    public double LoteFrenteM { get; init; }
    public double LoteFondoM { get; init; }
    public RectDef Nave { get; init; } = new(0, 0, 0, 0);

    /// <summary>Posiciones de los ejes estructurales numerados a lo largo de la nave (para dibujar la grilla a escala del sitio).</summary>
    public IReadOnlyList<double> EjesEstructuralesM { get; init; } = [];

    public IReadOnlyList<RectDef> EstacionamientoAutos { get; init; } = [];
    public IReadOnlyList<MuelleDef> Muelles { get; init; } = [];
    public RectDef? PatioManiobraPesada { get; init; }
    public RectDef? CorredorAccesoPesado { get; init; }

    /// <summary>Hallazgos no bloqueantes (franjas angostas, cantidades solicitadas que no entran, etc.).</summary>
    public IReadOnlyList<string> Advertencias { get; init; } = [];
}
