using Galpones.Core.Input;
using Galpones.Core.IntermediateModel;

namespace Galpones.Core.Site;

/// <summary>
/// Solver de implantación estilo TestFit (SIT-01/SIT-02 del backlog): dada la posición y rotación
/// de la nave en el lote, elige la cara de muelles, genera el patio de maniobra pesada y la calle
/// de acceso, y rellena las zonas libres con estacionamiento de autos de forma eficiente.
/// Es un borrador orientativo para revisar dimensiones de terreno con el arquitecto: no calcula
/// radios de giro reales ni aplica retiros por ordenanza (pendiente de NOR-01).
///
/// Dimensiones de referencia (ver PLAN-DE-TRABAJO y fuentes de investigación):
/// plaza de auto 2,5 × 5,0 m; pasillo 90° 6,0 m (doble sentido), 60° 5,0 m, 45° 4,0 m (un sentido);
/// módulo de muelle 3,6 m; calle pesada 7,3 m; patio de maniobra ≥30 m operativo, recomendado ≥35 m.
///
/// Requiere que <see cref="ProjectInputLoader.Validate"/> ya haya aceptado el input.
/// </summary>
public static class SiteLayoutGenerator
{
    /// <summary>Por debajo de esta profundidad el patio de maniobra se marca como advertencia.</summary>
    public const double ProfundidadPatioMinimaOperativaM = 30.0;

    private const double ModuloMuelleM = 3.6;
    private const double MargenMuelleM = 1.5;

    private const double AnchoPlazaM = 2.5;
    private const double LargoPlazaM = 5.0;
    private const double AnchoMinimoCorredorPesadoM = 4.5;

    private const double Epsilon = 0.0001;

    public static SiteLayoutModel Generate(ProjectInput input, GalponModel model)
    {
        var advertencias = new List<string>();
        var frente = input.Lote.Frente;
        var fondo = input.Lote.Fondo;
        var retiroFrente = input.Logistica.RetiroFrenteM ?? LogisticaInput.RetiroFrenteDefaultM;
        var retiroFondo = input.Logistica.RetiroFondoM ?? LogisticaInput.RetiroFondoDefaultM;
        var retiroLateral = input.Logistica.RetiroLateralM ?? LogisticaInput.RetiroLateralDefaultM;
        var patioObjetivo = input.Logistica.ProfundidadPatioM ?? LogisticaInput.ProfundidadPatioDefaultM;
        var anchoCalle = input.Logistica.AnchoCallePesadaM ?? LogisticaInput.AnchoCallePesadaDefaultM;
        var angulo = input.Logistica.AnguloEstacionamiento ?? LogisticaInput.AnguloEstacionamientoDefault;
        var rotacion = input.Implantacion?.RotacionGrados ?? 0;

        // --- 1. Implantación de la nave (bbox ya rotado) ---
        var (naveAncho, naveProfundidad) = rotacion is 90 or 270
            ? (input.Nave.Largo, input.Nave.Ancho)
            : (input.Nave.Ancho, input.Nave.Largo);
        var naveX = Clamp(input.Implantacion?.XM ?? retiroLateral, 0, Math.Max(0, frente - naveAncho));
        var naveY = Clamp(input.Implantacion?.YM ?? retiroFrente, 0, Math.Max(0, fondo - naveProfundidad));
        var nave = new RectDef(naveX, naveY, naveAncho, naveProfundidad);

        if (naveX < retiroLateral - Epsilon || naveY < retiroFrente - Epsilon
            || naveX + naveAncho > frente - retiroLateral + Epsilon
            || naveY + naveProfundidad > fondo - retiroFondo + Epsilon)
        {
            advertencias.Add($"La implantación elegida invade los retiros ({retiroLateral:0.#} m laterales, {retiroFrente:0.#} m al frente, {retiroFondo:0.#} m al fondo). Es una simulación: los retiros por ordenanza se verifican en NOR-01.");
        }
        if (retiroLateral < AnchoMinimoCorredorPesadoM)
            advertencias.Add($"logistica.retiro_lateral_m ({retiroLateral:0.#} m) es angosto para un carril de transporte pesado; se recomiendan ≥{AnchoMinimoCorredorPesadoM:0.#} m, sujeto a verificar el radio de giro real del vehículo.");

        // --- 2. Cara de muelles: la de mayor profundidad libre, salvo override ---
        var libres = new Dictionary<CaraMuelles, double>
        {
            [CaraMuelles.Frente] = nave.Y,
            [CaraMuelles.Fondo] = fondo - (nave.Y + naveProfundidad),
            [CaraMuelles.Izquierda] = nave.X,
            [CaraMuelles.Derecha] = frente - (nave.X + naveAncho),
        };
        var cara = ParseCara(input.Logistica.MuellesEn) ?? libres.OrderByDescending(kv => kv.Value).First().Key;

        // --- 3. Patio de maniobra pesada (truck court) ---
        var libreCara = libres[cara];
        var profundidadPatio = Math.Min(patioObjetivo, Math.Max(0, libreCara));
        RectDef? patio = profundidadPatio >= 1.0 ? RectHaciaAfuera(nave, cara, profundidadPatio) : null;
        if (patio is null)
            advertencias.Add($"La cara de muelles ({cara.ToString().ToLowerInvariant()}) no tiene espacio libre para un patio de maniobra; los camiones no pueden maniobrar en reversa. Mové o rotá la nave.");
        else if (profundidadPatio < ProfundidadPatioMinimaOperativaM)
            advertencias.Add($"El patio de maniobra queda en {profundidadPatio:0.#} m de profundidad; el mínimo operativo para un semirremolque es ~{ProfundidadPatioMinimaOperativaM:0.#} m y se recomiendan ≥{LogisticaInput.ProfundidadPatioDefaultM:0.#} m. Pendiente de validar con operaciones.");

        // --- 4. Calle de acceso pesada: conecta el patio con la calle ---
        RectDef? corredor = null;
        if (cara != CaraMuelles.Frente && nave.Y >= 1.0)
        {
            var lado = libres[CaraMuelles.Derecha] >= libres[CaraMuelles.Izquierda] ? CaraMuelles.Derecha : CaraMuelles.Izquierda;
            if (cara is CaraMuelles.Izquierda or CaraMuelles.Derecha)
                lado = cara; // el patio ya está en ese lateral: la calle entra por ahí
            var libreLado = libres[lado];
            var anchoCorredor = Math.Min(anchoCalle, Math.Max(0, libreLado));
            if (anchoCorredor < 3.0)
                advertencias.Add($"No hay franja lateral libre suficiente para la calle de acceso pesado ({libreLado:0.#} m). Se necesitan ~{anchoCalle:0.#} m.");
            else
            {
                corredor = lado == CaraMuelles.Izquierda
                    ? new RectDef(0, 0, anchoCorredor, nave.Y + (cara == lado ? 0 : naveProfundidad))
                    : new RectDef(frente - anchoCorredor, 0, anchoCorredor, nave.Y + (cara == lado ? 0 : naveProfundidad));
                if (anchoCorredor < anchoCalle - Epsilon)
                    advertencias.Add($"La calle de acceso pesado queda en {anchoCorredor:0.#} m de ancho (recomendado {anchoCalle:0.#} m doble sentido).");
            }
        }

        // --- 5. Muelles sobre la cara elegida ---
        var muelles = GenerarMuelles(input, nave, cara, advertencias);

        // --- 6. Estacionamiento: rellenar las zonas libres ---
        var obstaculos = new List<RectDef> { nave };
        if (patio is { } p) obstaculos.Add(p);
        if (corredor is { } c) obstaculos.Add(c);
        var zonasLibres = new List<RectDef> { new(0, 0, frente, fondo) };
        foreach (var obstaculo in obstaculos)
            zonasLibres = zonasLibres.SelectMany(z => Restar(z, obstaculo)).ToList();

        var (plazas, superficieParking) = GenerarEstacionamiento(input, zonasLibres, angulo, advertencias);

        return new SiteLayoutModel
        {
            LoteFrenteM = frente,
            LoteFondoM = fondo,
            Nave = nave,
            NaveRotacionGrados = rotacion,
            EjesEstructuralesM = [.. model.Grids.Where(g => g.Direction == GridDirection.Y).Select(g => g.PosicionMetros).OrderBy(v => v)],
            EstacionamientoAutos = plazas,
            Muelles = muelles,
            PatioManiobraPesada = patio,
            CorredorAccesoPesado = corredor,
            CaraDeMuelles = cara,
            GuiasSnapX = [retiroLateral, Math.Max(0, frente - retiroLateral - naveAncho)],
            GuiasSnapY = [retiroFrente, Math.Max(0, fondo - retiroFondo - naveProfundidad)],
            Metricas = new MetricasSitio(
                CoberturaPct: 100.0 * naveAncho * naveProfundidad / (frente * fondo),
                AutosColocados: plazas.Count,
                AutosSolicitados: input.Logistica.AutosCantidad,
                MuellesColocados: muelles.Count,
                ProfundidadPatioM: patio is null ? 0 : profundidadPatio,
                SuperficieEstacionamientoM2: Math.Round(superficieParking, 1)),
            Advertencias = advertencias,
        };
    }

    private static CaraMuelles? ParseCara(string? muellesEn) => muellesEn?.Trim().ToLowerInvariant() switch
    {
        null or "" or "auto" => null,
        "frente" => CaraMuelles.Frente,
        "fondo" => CaraMuelles.Fondo,
        "izquierda" => CaraMuelles.Izquierda,
        "derecha" => CaraMuelles.Derecha,
        _ => null,
    };

    private static RectDef RectHaciaAfuera(RectDef nave, CaraMuelles cara, double profundidad) => cara switch
    {
        CaraMuelles.Frente => new(nave.X, nave.Y - profundidad, nave.Ancho, profundidad),
        CaraMuelles.Fondo => new(nave.X, nave.Y + nave.Profundidad, nave.Ancho, profundidad),
        CaraMuelles.Izquierda => new(nave.X - profundidad, nave.Y, profundidad, nave.Profundidad),
        CaraMuelles.Derecha => new(nave.X + nave.Ancho, nave.Y, profundidad, nave.Profundidad),
        _ => throw new ArgumentOutOfRangeException(nameof(cara)),
    };

    private static List<MuelleDef> GenerarMuelles(ProjectInput input, RectDef nave, CaraMuelles cara, List<string> advertencias)
    {
        var largoCara = cara is CaraMuelles.Izquierda or CaraMuelles.Derecha ? nave.Profundidad : nave.Ancho;
        var anchoUtil = Math.Max(0, largoCara - 2 * MargenMuelleM);
        var maximo = anchoUtil > 0 ? (int)Math.Floor(anchoUtil / ModuloMuelleM) + 1 : 0;
        var solicitados = input.Logistica.CamionesCantidad;
        var cantidad = Math.Clamp(solicitados ?? maximo, 0, maximo);

        if (maximo == 0)
            advertencias.Add($"La cara de muelles ({largoCara:0.##} m) no admite muelles con el módulo estándar de {ModuloMuelleM:0.#} m.");
        else if (solicitados is { } s && s > maximo)
            advertencias.Add($"Se pidieron {s} muelles de carga pero la cara {cara.ToString().ToLowerInvariant()} de la nave admite como máximo {maximo} con el módulo de {ModuloMuelleM:0.#} m.");

        var muelles = new List<MuelleDef>();
        if (cantidad <= 0) return muelles;
        var paso = largoCara / cantidad;
        for (var i = 0; i < cantidad; i++)
        {
            var t = paso * (i + 0.5);
            muelles.Add(cara switch
            {
                CaraMuelles.Frente => new MuelleDef($"M{i + 1}", nave.X + t, nave.Y),
                CaraMuelles.Fondo => new MuelleDef($"M{i + 1}", nave.X + t, nave.Y + nave.Profundidad),
                CaraMuelles.Izquierda => new MuelleDef($"M{i + 1}", nave.X, nave.Y + t),
                CaraMuelles.Derecha => new MuelleDef($"M{i + 1}", nave.X + nave.Ancho, nave.Y + t),
                _ => throw new ArgumentOutOfRangeException(nameof(cara)),
            });
        }
        return muelles;
    }

    private static (List<PlazaDef> Plazas, double SuperficieM2) GenerarEstacionamiento(
        ProjectInput input, List<RectDef> zonasLibres, int angulo, List<string> advertencias)
    {
        var solicitados = input.Logistica.AutosCantidad;
        var restantes = solicitados ?? int.MaxValue;
        var plazas = new List<PlazaDef>();
        var superficie = 0.0;

        // Prioridad: primero las zonas que tocan la calle (frente), después por cercanía al frente.
        var zonas = zonasLibres
            .Where(z => z.Ancho >= 2.0 && z.Profundidad >= 2.0)
            .OrderByDescending(z => z.Y <= Epsilon)
            .ThenBy(z => z.Y)
            .ThenByDescending(z => z.Ancho * z.Profundidad);

        foreach (var zona in zonas)
        {
            if (restantes <= 0) break;
            var (plazasZona, superficieZona) = RellenarZona(zona, angulo, restantes, tocaLaCalle: zona.Y <= Epsilon);
            plazas.AddRange(plazasZona);
            superficie += superficieZona;
            restantes -= plazasZona.Count;
        }

        if (plazas.Count == 0)
            advertencias.Add("No quedó ninguna zona libre con las dimensiones mínimas para estacionamiento de autos.");
        else if (solicitados is { } s && plazas.Count < s)
            advertencias.Add($"Se pidieron {s} espacios de auto pero las zonas libres admiten como máximo {plazas.Count}.");

        return (plazas, superficie);
    }

    /// <summary>
    /// Rellena una zona rectangular con bandas de estacionamiento al ángulo dado: bandas dobles
    /// (dos hileras comparten pasillo) y una banda simple al final si entra. Prueba hileras a lo
    /// largo de X y de Y y se queda con la orientación que da más plazas. Si la zona toca la calle,
    /// la primera hilera usa la calle como pasillo (sin pasillo interno).
    /// </summary>
    private static (List<PlazaDef>, double) RellenarZona(RectDef zona, int angulo, int maximo, bool tocaLaCalle)
    {
        var a = RellenarZonaEnOrientacion(zona, angulo, maximo, tocaLaCalle, filasALoLargoDeX: true);
        var b = RellenarZonaEnOrientacion(zona, angulo, maximo, tocaLaCalle, filasALoLargoDeX: false);
        return a.Item1.Count >= b.Item1.Count ? a : b;
    }

    private static (List<PlazaDef>, double) RellenarZonaEnOrientacion(
        RectDef zona, int angulo, int maximo, bool tocaLaCalle, bool filasALoLargoDeX)
    {
        // En la orientación elegida, "largoZona" es la dimensión a lo largo de las hileras y
        // "profundidadZona" la que consumen las bandas (plaza + pasillo).
        var largoZona = filasALoLargoDeX ? zona.Ancho : zona.Profundidad;
        var profundidadZona = filasALoLargoDeX ? zona.Profundidad : zona.Ancho;

        var rad = angulo * Math.PI / 180.0;
        var profundidadPlaza = LargoPlazaM * Math.Sin(rad) + AnchoPlazaM * Math.Cos(rad);
        var pasoSobreHilera = AnchoPlazaM / Math.Sin(rad);
        var pasillo = angulo switch { 45 => 4.0, 60 => 5.0, _ => 6.0 };

        var bandaDoble = 2 * profundidadPlaza + pasillo;
        var bandaSimple = profundidadPlaza + pasillo;
        // Hilera contra el borde de calle: los autos maniobran desde la calle, sin pasillo interno.
        var bandaSimpleCalle = tocaLaCalle ? profundidadPlaza : double.MaxValue;

        var plazasPorHilera = (int)Math.Floor(largoZona / pasoSobreHilera);
        if (plazasPorHilera <= 0) return ([], 0);

        var plazas = new List<PlazaDef>();
        var superficie = 0.0;
        var cursor = 0.0;
        var restantes = maximo;

        // Primera banda: si la zona toca la calle, hilera simple sin pasillo.
        if (profundidadZona >= bandaSimpleCalle && restantes > 0)
        {
            AgregarHileras(1, cursor, sinPasillo: true);
            cursor += profundidadPlaza;
        }

        while (restantes > 0)
        {
            if (profundidadZona - cursor >= bandaDoble)
            {
                AgregarHileras(2, cursor, sinPasillo: false);
                cursor += bandaDoble;
            }
            else if (profundidadZona - cursor >= bandaSimple)
            {
                AgregarHileras(1, cursor, sinPasillo: false);
                cursor += bandaSimple;
            }
            else break;
        }

        return (plazas, superficie);

        void AgregarHileras(int cantidad, double inicioBanda, bool sinPasillo)
        {
            for (var fila = 0; fila < cantidad && restantes > 0; fila++)
            {
                var inicioPlaza = inicioBanda + fila * profundidadPlaza;
                var enEstaHilera = Math.Min(plazasPorHilera, restantes);
                for (var i = 0; i < enEstaHilera; i++)
                {
                    var (px, py) = filasALoLargoDeX
                        ? (zona.X + i * pasoSobreHilera, zona.Y + inicioPlaza)
                        : (zona.X + inicioPlaza, zona.Y + i * pasoSobreHilera);
                    var (ancho, profundidad) = filasALoLargoDeX
                        ? (pasoSobreHilera, profundidadPlaza)
                        : (profundidadPlaza, pasoSobreHilera);
                    plazas.Add(new PlazaDef(px, py, ancho, profundidad, angulo));
                }
                superficie += enEstaHilera * pasoSobreHilera * profundidadPlaza;
                restantes -= enEstaHilera;
            }
            if (!sinPasillo)
                superficie += pasillo * largoZona;
        }
    }

    /// <summary>Resta b de a; devuelve hasta 4 rectángulos con las partes de a que quedan libres.</summary>
    private static IEnumerable<RectDef> Restar(RectDef a, RectDef b)
    {
        var x1 = Math.Max(a.X, b.X);
        var x2 = Math.Min(a.X + a.Ancho, b.X + b.Ancho);
        var y1 = Math.Max(a.Y, b.Y);
        var y2 = Math.Min(a.Y + a.Profundidad, b.Y + b.Profundidad);
        if (x1 >= x2 || y1 >= y2)
        {
            yield return a;
            yield break;
        }
        if (y1 > a.Y) yield return new(a.X, a.Y, a.Ancho, y1 - a.Y);
        if (y2 < a.Y + a.Profundidad) yield return new(a.X, y2, a.Ancho, a.Y + a.Profundidad - y2);
        if (x1 > a.X) yield return new(a.X, y1, x1 - a.X, y2 - y1);
        if (x2 < a.X + a.Ancho) yield return new(x2, y1, a.X + a.Ancho - x2, y2 - y1);
    }

    private static double Clamp(double valor, double min, double max) => Math.Min(Math.Max(valor, min), max);
}
