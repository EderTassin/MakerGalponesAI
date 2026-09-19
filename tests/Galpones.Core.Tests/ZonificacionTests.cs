using Galpones.Core.Input;
using Galpones.Core.Rules;

namespace Galpones.Core.Tests;

public class ZonificacionRulePackLoaderTests
{
    private static IReadOnlyDictionary<string, ZonaRule> LoadRealPack()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Reglas", "cordoba-capital", "zonificacion.yaml");
        return ZonificacionRulePackLoader.LoadFromFile(path);
    }

    [Fact]
    public void LoadFromFile_ParseaLas27ZonasDeLaOrdenanza8256()
    {
        var pack = LoadRealPack();

        Assert.Equal(27, pack.Count);
    }

    [Fact]
    public void LoadFromFile_ParseaZonaK_ConRegimenIndustrialPorTramoDeFrente()
    {
        var pack = LoadRealPack();

        var zonaK = pack["K"];
        Assert.True(zonaK.TienesFosPorTramoDeFrente);
        Assert.Equal(0.80, zonaK.FosMaxFrenteMenor25M);
        Assert.Equal(0.60, zonaK.FosMaxFrenteMayorIgual25M);
        Assert.Equal(1.50, zonaK.FotMax);
        Assert.Null(zonaK.AlturaMaxMetros); // sin limitaciones para uso industrial
        Assert.Contains("Art. 64", zonaK.Articulo);

        Assert.Equal(0.80, zonaK.FosMaxParaFrente(20));
        Assert.Equal(0.60, zonaK.FosMaxParaFrente(40));
    }

    [Fact]
    public void LoadFromFile_ParseaZonaConFosPlano()
    {
        var pack = LoadRealPack();

        var zonaD = pack["D"];
        Assert.False(zonaD.TienesFosPorTramoDeFrente);
        Assert.Equal(0.70, zonaD.FosMax);
        Assert.Equal(2.0, zonaD.FotMax);
        Assert.Equal(12.0, zonaD.AlturaMaxMetros);
    }
}

public class ZonificacionValidatorTests
{
    private static IReadOnlyDictionary<string, ZonaRule> RulePackConZonaKIndustrial() => new Dictionary<string, ZonaRule>(StringComparer.OrdinalIgnoreCase)
    {
        ["K"] = new ZonaRule(
            Zona: "K",
            Caracter: "Zona industrial",
            FosMax: null,
            FosMaxFrenteMenor25M: 0.80,
            FosMaxFrenteMayorIgual25M: 0.60,
            FotMax: 1.50,
            AlturaMaxMetros: null,
            Retiros: "-",
            Articulo: "Ordenanza 8256/86, Art. 64°"),
    };

    private static IReadOnlyDictionary<string, ZonaRule> RulePackConZonaDeAlturaLimitada() => new Dictionary<string, ZonaRule>(StringComparer.OrdinalIgnoreCase)
    {
        ["D"] = new ZonaRule(
            Zona: "D",
            Caracter: "Zona con altura limitada",
            FosMax: 0.70,
            FosMaxFrenteMenor25M: null,
            FosMaxFrenteMayorIgual25M: null,
            FotMax: 2.0,
            AlturaMaxMetros: 12.0,
            Retiros: "-",
            Articulo: "Ordenanza 8256/86, Art. 50°"),
    };

    private static ProjectInput EjemploDelDocumentoEnZonaK() => new()
    {
        Tipologia = "nave_deposito",
        Jurisdiccion = "cordoba-capital",
        Lote = new LoteInput { Frente = 40, Fondo = 80, Zona = "K" },
        Nave = new NaveInput { Largo = 60, Ancho = 25, AlturaLibre = 8 },
        Estructura = new EstructuraInput { Tipo = "porticos_metalicos", Modulacion = 6 },
        Piso = new PisoInput { SobrecargaKnM2 = 30 },
        Electrico = new ElectricoInput { PotenciaKva = 150, Tension = "trifasica" },
    };

    [Fact]
    public void Validate_ProyectoDelDocumentoEnZonaK_EsCompatible()
    {
        var incumplimientos = ZonificacionValidator.Validate(EjemploDelDocumentoEnZonaK(), RulePackConZonaKIndustrial());

        Assert.Empty(incumplimientos);
    }

    [Fact]
    public void Validate_FosExcedeElTramoDeFrenteMayorIgual25M_GeneraIncumplimiento()
    {
        var input = EjemploDelDocumentoEnZonaK();
        input.Lote = new LoteInput { Frente = 40, Fondo = 30, Zona = "K" }; // 1200 m², nave cubre 1500 m² -> FOS 1.25 > 0.60

        var incumplimientos = ZonificacionValidator.Validate(input, RulePackConZonaKIndustrial());

        Assert.Contains(incumplimientos, i => i.Regla == "fos_maximo");
    }

    [Fact]
    public void Validate_ZonaSinLimiteDeAltura_NoGeneraIncumplimientoDeAltura()
    {
        var input = EjemploDelDocumentoEnZonaK();
        input.Nave.AlturaLibre = 40; // Zona K industrial no tiene límite de altura

        var incumplimientos = ZonificacionValidator.Validate(input, RulePackConZonaKIndustrial());

        Assert.DoesNotContain(incumplimientos, i => i.Regla == "altura_maxima");
    }

    [Fact]
    public void Validate_AlturaExcedeMaximoDeZona_GeneraIncumplimiento()
    {
        var input = EjemploDelDocumentoEnZonaK();
        input.Lote.Zona = "D";
        input.Nave.AlturaLibre = 15; // zona D: máximo 12 m

        var incumplimientos = ZonificacionValidator.Validate(input, RulePackConZonaDeAlturaLimitada());

        Assert.Contains(incumplimientos, i => i.Regla == "altura_maxima");
    }

    [Fact]
    public void Validate_ZonaNoDigitalizada_GeneraUnSoloIncumplimientoDeAdvertencia()
    {
        var input = EjemploDelDocumentoEnZonaK();
        input.Lote.Zona = "Z99";

        var incumplimientos = ZonificacionValidator.Validate(input, RulePackConZonaKIndustrial());

        var incumplimiento = Assert.Single(incumplimientos);
        Assert.Equal("zona_no_reconocida", incumplimiento.Regla);
    }
}
