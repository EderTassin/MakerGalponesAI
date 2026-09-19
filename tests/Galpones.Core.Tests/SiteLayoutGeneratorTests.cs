using Galpones.Core.Input;
using Galpones.Core.IntermediateModel;
using Galpones.Core.Site;

namespace Galpones.Core.Tests;

public class SiteLayoutGeneratorTests
{
    private static ProjectInput EjemploDelDocumento() => new()
    {
        Tipologia = "nave_deposito",
        Jurisdiccion = "cordoba-capital",
        Lote = new LoteInput { Frente = 40, Fondo = 80, Zona = "industrial-2" },
        Nave = new NaveInput { Largo = 60, Ancho = 25, AlturaLibre = 8 },
        Estructura = new EstructuraInput { Tipo = "porticos_metalicos", Modulacion = 6 },
        Piso = new PisoInput { SobrecargaKnM2 = 30 },
        Electrico = new ElectricoInput { PotenciaKva = 150, Tension = "trifasica" },
    };

    private static SiteLayoutModel GenerarConDefaults(ProjectInput input)
    {
        ProjectInputLoader.Validate(input);
        var model = GalponModelGenerator.Generate(input);
        return SiteLayoutGenerator.Generate(input, model);
    }

    [Fact]
    public void Generate_UbicaLaNaveDentroDelLoteConLosRetirosPorDefecto()
    {
        var site = GenerarConDefaults(EjemploDelDocumento());

        Assert.Equal(LogisticaInput.RetiroLateralDefaultM, site.Nave.X);
        Assert.Equal(LogisticaInput.RetiroFrenteDefaultM, site.Nave.Y);
        Assert.Equal(25, site.Nave.Ancho);
        Assert.Equal(60, site.Nave.Profundidad);
        Assert.True(site.Nave.X + site.Nave.Ancho <= site.LoteFrenteM);
        Assert.True(site.Nave.Y + site.Nave.Profundidad <= site.LoteFondoM);
    }

    [Fact]
    public void Generate_GeneraEstacionamientoDentroDeLaFranjaFrontalSinSuperponerLaNave()
    {
        var site = GenerarConDefaults(EjemploDelDocumento());

        Assert.NotEmpty(site.EstacionamientoAutos);
        Assert.All(site.EstacionamientoAutos, espacio =>
        {
            Assert.True(espacio.X >= site.Nave.X - 0.0001 || espacio.X + espacio.Ancho <= site.Nave.X + 0.0001 || espacio.Y + espacio.Profundidad <= site.Nave.Y + 0.0001);
            Assert.True(espacio.Y + espacio.Profundidad <= site.Nave.Y + 0.0001, "El estacionamiento no debe invadir la franja de la nave.");
            Assert.True(espacio.X >= 0 && espacio.X + espacio.Ancho <= site.LoteFrenteM + 0.0001);
        });
    }

    [Fact]
    public void Generate_GeneraUnMuellePorCadaModuloDeAnchoDeNaveYLosUbicaEnElPanioDeFondo()
    {
        var site = GenerarConDefaults(EjemploDelDocumento());

        Assert.NotEmpty(site.Muelles);
        Assert.All(site.Muelles, m => Assert.Equal(site.Nave.Y + site.Nave.Profundidad, m.Y));
        Assert.All(site.Muelles, m => Assert.InRange(m.X, site.Nave.X, site.Nave.X + site.Nave.Ancho));
    }

    [Fact]
    public void Generate_RespetaLaCantidadDeCamionesSolicitadaCuandoEntra()
    {
        var input = EjemploDelDocumento();
        input.Logistica.CamionesCantidad = 2;

        var site = GenerarConDefaults(input);

        Assert.Equal(2, site.Muelles.Count);
    }

    [Fact]
    public void Generate_AdvierteYRecortaCuandoSePidenMasCamionesQueLosQueEntran()
    {
        var input = EjemploDelDocumento();
        input.Logistica.CamionesCantidad = 1000;

        var site = GenerarConDefaults(input);

        Assert.True(site.Muelles.Count < 1000);
        Assert.Contains(site.Advertencias, a => a.Contains("muelles"));
    }

    [Fact]
    public void Generate_RespetaLaCantidadDeAutosSolicitadaCuandoEntra()
    {
        var input = EjemploDelDocumento();
        input.Logistica.AutosCantidad = 3;

        var site = GenerarConDefaults(input);

        Assert.Equal(3, site.EstacionamientoAutos.Count);
    }

    [Fact]
    public void Generate_AdvierteCuandoElRetiroLateralEsAngostoParaTransportePesado()
    {
        var input = EjemploDelDocumento();
        input.Logistica.RetiroLateralM = 2.0;

        var site = GenerarConDefaults(input);

        Assert.Contains(site.Advertencias, a => a.Contains("retiro_lateral_m"));
    }

    [Fact]
    public void Generate_AdvierteCuandoElPatioDeManiobraQuedaPorDebajoDeLoRecomendado()
    {
        var site = GenerarConDefaults(EjemploDelDocumento());

        // Con los defaults del documento (fondo 80, nave 60, retiro_frente 8, retiro_fondo 3) el
        // patio queda en 9 m, muy por debajo de los ~30 m recomendados para un semirremolque.
        Assert.Contains(site.Advertencias, a => a.Contains("patio de maniobra"));
    }

    [Fact]
    public void Generate_IncluyeLosEjesEstructuralesDeLaNaveParaDibujarLaGrillaAEscalaDelSitio()
    {
        var input = EjemploDelDocumento();
        var model = GalponModelGenerator.Generate(input);

        var site = SiteLayoutGenerator.Generate(input, model);

        Assert.Equal(11, site.EjesEstructuralesM.Count); // 60 / 6 + 1
        Assert.Equal(0.0, site.EjesEstructuralesM[0]);
        Assert.Equal(60.0, site.EjesEstructuralesM[^1]);
    }
}
