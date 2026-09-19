namespace Galpones.Core.Site;

/// <summary>
/// Rectángulo en el plano del lote, en metros. Origen (0,0) en la esquina frente-izquierda del
/// lote; X crece hacia el lado derecho del frente, Y crece desde el frente hacia el fondo.
/// </summary>
public sealed record RectDef(double X, double Y, double Ancho, double Profundidad);

/// <summary>Muelle de carga sobre la cara de muelles de la nave, mirando hacia el patio de maniobra.</summary>
public sealed record MuelleDef(string Nombre, double X, double Y);

/// <summary>
/// Plaza de estacionamiento. <paramref name="Angulo"/> es el ángulo de la plaza respecto de la
/// hilera (45, 60 o 90°); el render la dibuja rotada alrededor de su centro.
/// </summary>
public sealed record PlazaDef(double X, double Y, double Ancho, double Profundidad, double Angulo);

/// <summary>Cara de la nave por donde cargan los camiones.</summary>
public enum CaraMuelles
{
    /// <summary>Hacia la calle (y decreciente).</summary>
    Frente,
    /// <summary>Hacia el fondo del lote (y creciente).</summary>
    Fondo,
    /// <summary>Hacia la izquierda del frente (x decreciente).</summary>
    Izquierda,
    /// <summary>Hacia la derecha del frente (x creciente).</summary>
    Derecha,
}

/// <summary>Tabulación en vivo del sitio (al estilo de los live metrics de TestFit).</summary>
public sealed record MetricasSitio(
    double CoberturaPct,
    int AutosColocados,
    int? AutosSolicitados,
    int MuellesColocados,
    double ProfundidadPatioM,
    double SuperficieEstacionamientoM2);

/// <summary>
/// Implantación simulada de la nave en el lote: estacionamiento de autos, muelles y circulación
/// de transporte pesado. No reemplaza un plano de sitio ni una comprobación normativa (retiros,
/// radios de giro y accesos reales requieren revisión profesional; ver SIT-01/SIT-02).
/// </summary>
public sealed class SiteLayoutModel
{
    public double LoteFrenteM { get; init; }
    public double LoteFondoM { get; init; }

    /// <summary>Bounding box de la nave ya rotada, en coordenadas del lote.</summary>
    public RectDef Nave { get; init; } = new(0, 0, 0, 0);

    /// <summary>Rotación aplicada a la nave (0, 90, 180, 270).</summary>
    public int NaveRotacionGrados { get; init; }

    /// <summary>Posiciones de los ejes estructurales numerados a lo largo del eje largo de la nave.</summary>
    public IReadOnlyList<double> EjesEstructuralesM { get; init; } = [];

    public IReadOnlyList<PlazaDef> EstacionamientoAutos { get; init; } = [];
    public IReadOnlyList<MuelleDef> Muelles { get; init; } = [];
    public RectDef? PatioManiobraPesada { get; init; }
    public RectDef? CorredorAccesoPesado { get; init; }
    public CaraMuelles CaraDeMuelles { get; init; }
    public MetricasSitio Metricas { get; init; } = new(0, 0, null, 0, 0, 0);

    /// <summary>Posiciones X/Y donde la nave "se pega" al arrastrarla (retiros configurados), para la vista interactiva.</summary>
    public IReadOnlyList<double> GuiasSnapX { get; init; } = [];
    public IReadOnlyList<double> GuiasSnapY { get; init; } = [];

    /// <summary>Hallazgos no bloqueantes (franjas angostas, cantidades solicitadas que no entran, etc.).</summary>
    public IReadOnlyList<string> Advertencias { get; init; } = [];
}
