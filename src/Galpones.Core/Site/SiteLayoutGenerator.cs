using Galpones.Core.Input;
using Galpones.Core.IntermediateModel;

namespace Galpones.Core.Site;

/// <summary>
/// Simulación de prefactibilidad de sitio, en el espíritu de SIT-01/SIT-02 del backlog: implanta
/// la nave en el lote y genera automáticamente estacionamiento de autos y circulación/muelles de
/// transporte pesado a partir de dimensiones estándar. Es un borrador orientativo para revisar
/// dimensiones de terreno con el arquitecto, no una comprobación de radios de giro reales, un
/// plano de sitio ni una aprobación normativa (los retiros por ordenanza dependen de la zona y
/// quedan pendientes de NOR-01).
///
/// Requiere que <see cref="ProjectInputLoader.Validate"/> ya haya aceptado el input: asume que la
/// nave entra en el lote con los retiros indicados.
/// </summary>
public static class SiteLayoutGenerator
{
    public const double ProfundidadMuelleRecomendadaM = 30.0;
    private const double ModuloMuelleM = 3.6;
    private const double MargenMuelleM = 1.5;

    private const double AnchoAutoM = 2.5;
    private const double LargoAutoM = 5.0;
    private const double PasilloAutosM = 6.0;
    private const double MargenCalleAutosM = 2.0;

    private const double AnchoMinimoCorredorPesadoM = 4.5;

    public static SiteLayoutModel Generate(ProjectInput input, GalponModel model)
    {
        var advertencias = new List<string>();
        var frente = input.Lote.Frente;
        var fondo = input.Lote.Fondo;
        var retiroFrente = input.Logistica.RetiroFrenteM ?? LogisticaInput.RetiroFrenteDefaultM;
        var retiroFondo = input.Logistica.RetiroFondoM ?? LogisticaInput.RetiroFondoDefaultM;
        var retiroLateral = input.Logistica.RetiroLateralM ?? LogisticaInput.RetiroLateralDefaultM;

        // La nave se apoya contra el corredor lateral izquierdo; el margen derecho sobrante
        // (si el frente es más ancho que nave.ancho + 2×retiro_lateral) queda como retiro sin uso asignado.
        var nave = new RectDef(retiroLateral, retiroFrente, input.Nave.Ancho, input.Nave.Largo);

        if (retiroLateral < AnchoMinimoCorredorPesadoM)
            advertencias.Add($"logistica.retiro_lateral_m ({retiroLateral:0.#} m) es angosto para un carril de transporte pesado; se recomiendan ≥{AnchoMinimoCorredorPesadoM:0.#} m, sujeto a verificar el radio de giro real del vehículo.");

        var corredor = retiroLateral > 0 ? new RectDef(0, 0, retiroLateral, fondo) : null;

        var profundidadPatio = fondo - retiroFondo - (nave.Y + nave.Profundidad);
        var patio = profundidadPatio > 0 ? new RectDef(0, nave.Y + nave.Profundidad, frente, profundidadPatio) : null;
        if (profundidadPatio < ProfundidadMuelleRecomendadaM)
            advertencias.Add($"El patio de maniobra queda en {Math.Max(profundidadPatio, 0):0.#} m de profundidad; se recomiendan ≥{ProfundidadMuelleRecomendadaM:0.#} m para que un semirremolque maniobre en reversa. Pendiente de validar con operaciones.");

        var muelles = GenerarMuelles(input, nave, advertencias);
        var autos = GenerarEstacionamiento(input, frente, retiroFrente, retiroLateral, advertencias);

        return new SiteLayoutModel
        {
            LoteFrenteM = frente,
            LoteFondoM = fondo,
            Nave = nave,
            EjesEstructuralesM = [.. model.Grids.Where(g => g.Direction == GridDirection.Y).Select(g => g.PosicionMetros).OrderBy(v => v)],
            EstacionamientoAutos = autos,
            Muelles = muelles,
            PatioManiobraPesada = patio,
            CorredorAccesoPesado = corredor,
            Advertencias = advertencias,
        };
    }

    private static List<MuelleDef> GenerarMuelles(ProjectInput input, RectDef nave, List<string> advertencias)
    {
        var anchoUtil = Math.Max(0, nave.Ancho - 2 * MargenMuelleM);
        var maximo = anchoUtil > 0 ? (int)Math.Floor(anchoUtil / ModuloMuelleM) + 1 : 0;
        var solicitados = input.Logistica.CamionesCantidad;
        var cantidad = Math.Clamp(solicitados ?? maximo, 0, maximo);

        if (maximo == 0)
            advertencias.Add($"nave.ancho ({nave.Ancho:0.##} m) no permite ubicar muelles de carga con el módulo estándar de {ModuloMuelleM:0.#} m.");
        else if (solicitados is { } s && s > maximo)
            advertencias.Add($"Se pidieron {s} muelles de carga pero el paño de fondo de la nave admite como máximo {maximo} con el módulo de {ModuloMuelleM:0.#} m.");

        var muelles = new List<MuelleDef>();
        if (cantidad <= 0) return muelles;
        var y = nave.Y + nave.Profundidad;
        var paso = nave.Ancho / cantidad;
        for (var i = 0; i < cantidad; i++)
            muelles.Add(new MuelleDef($"M{i + 1}", nave.X + paso * (i + 0.5), y));
        return muelles;
    }

    private static List<RectDef> GenerarEstacionamiento(ProjectInput input, double frente, double retiroFrente, double retiroLateral, List<string> advertencias)
    {
        var filasY = new List<double>();
        var dosFilas = retiroFrente >= 2 * LargoAutoM + PasilloAutosM + MargenCalleAutosM;
        var unaFila = retiroFrente >= LargoAutoM + MargenCalleAutosM;
        if (dosFilas)
        {
            filasY.Add(retiroFrente - LargoAutoM);
            filasY.Add(retiroFrente - 2 * LargoAutoM - PasilloAutosM);
        }
        else if (unaFila)
        {
            filasY.Add(retiroFrente - LargoAutoM);
        }
        else
        {
            advertencias.Add($"logistica.retiro_frente_m ({retiroFrente:0.#} m) es insuficiente para estacionamiento de autos; se recomiendan ≥{LargoAutoM + MargenCalleAutosM:0.#} m.");
        }

        var anchoDisponible = Math.Max(0, frente - retiroLateral);
        var columnas = filasY.Count > 0 ? (int)Math.Floor(anchoDisponible / AnchoAutoM) : 0;
        var maximo = columnas * filasY.Count;
        var solicitados = input.Logistica.AutosCantidad;
        var cantidad = Math.Clamp(solicitados ?? maximo, 0, maximo);
        if (solicitados is { } s && s > maximo)
            advertencias.Add($"Se pidieron {s} espacios de auto pero la franja frontal admite como máximo {maximo}.");

        var espacios = new List<RectDef>();
        var colocados = 0;
        foreach (var y in filasY)
        {
            for (var i = 0; i < columnas && colocados < cantidad; i++, colocados++)
                espacios.Add(new RectDef(retiroLateral + i * AnchoAutoM, y, AnchoAutoM, LargoAutoM));
        }
        return espacios;
    }
}
