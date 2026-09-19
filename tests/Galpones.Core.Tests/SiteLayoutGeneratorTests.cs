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
    public void Generate_GeneraEstacionamientoEnZonasLibresSinSuperponerLaNave()
    {
        var site = GenerarConDefaults(EjemploDelDocumento());

        Assert.NotEmpty(site.EstacionamientoAutos);
        Assert.All(site.EstacionamientoAutos, plaza =>
        {
            var sinSuperposicion =
                plaza.X + plaza.Ancho <= site.Nave.X + 0.0001 ||
                plaza.X >= site.Nave.X + site.Nave.Ancho - 0.0001 ||
                plaza.Y + plaza.Profundidad <= site.Nave.Y + 0.0001 ||
                plaza.Y >= site.Nave.Y + site.Nave.Profundidad - 0.0001;
            Assert.True(sinSuperposicion, "La plaza no debe superponerse con la nave.");
            Assert.True(plaza.X >= -0.0001 && plaza.X + plaza.Ancho <= site.LoteFrenteM + 0.0001);
            Assert.True(plaza.Y >= -0.0001 && plaza.Y + plaza.Profundidad <= site.LoteFondoM + 0.0001);
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

    [Fact]
    public void Generate_ConImplantacionExplicita_RespetaLaPosicion()
    {
        var input = EjemploDelDocumento();
        input.Implantacion = new ImplantacionInput { XM = 7.5, YM = 12 };

        var site = GenerarConDefaults(input);

        Assert.Equal(7.5, site.Nave.X);
        Assert.Equal(12, site.Nave.Y);
        Assert.Equal(0, site.NaveRotacionGrados);
    }

    [Fact]
    public void Generate_ConRotacion90_ElLargoQuedaParaleloAlFrente()
    {
        var input = EjemploDelDocumento();
        input.Lote = new LoteInput { Frente = 100, Fondo = 45, Zona = "industrial-2" };
        input.Implantacion = new ImplantacionInput { RotacionGrados = 90 };

        var site = GenerarConDefaults(input);

        Assert.Equal(60, site.Nave.Ancho);
        Assert.Equal(25, site.Nave.Profundidad);
        Assert.Equal(90, site.NaveRotacionGrados);
    }

    [Fact]
    public void Generate_CaraDeMuellesAuto_EligeElLadoConMasProfundidadLibre()
    {
        var input = EjemploDelDocumento();
        // Nave pegada al fondo (ocupa y 15..75): el mayor espacio libre queda hacia el frente.
        input.Implantacion = new ImplantacionInput { XM = 4.5, YM = 15 };
        input.Lote = new LoteInput { Frente = 40, Fondo = 75, Zona = "industrial-2" };

        var site = GenerarConDefaults(input);

        Assert.Equal(CaraMuelles.Frente, site.CaraDeMuelles);
        Assert.All(site.Muelles, m => Assert.Equal(site.Nave.Y, m.Y));
    }

    [Fact]
    public void Generate_MuellesEnFondo_PorDefectoConElEjemploDelDocumento()
    {
        var site = GenerarConDefaults(EjemploDelDocumento());

        Assert.Equal(CaraMuelles.Fondo, site.CaraDeMuelles);
        Assert.All(site.Muelles, m => Assert.Equal(site.Nave.Y + site.Nave.Profundidad, m.Y));
    }

    [Fact]
    public void Generate_MuellesEnOverride_RespetaLaCaraPedida()
    {
        var input = EjemploDelDocumento();
        input.Logistica.MuellesEn = "frente";

        var site = GenerarConDefaults(input);

        Assert.Equal(CaraMuelles.Frente, site.CaraDeMuelles);
        Assert.All(site.Muelles, m => Assert.Equal(site.Nave.Y, m.Y));
    }

    [Fact]
    public void Generate_ConAngulo45_EntranMenosPlazasPorHileraQueEn90()
    {
        var input90 = EjemploDelDocumento();
        var input45 = EjemploDelDocumento();
        input45.Logistica.AnguloEstacionamiento = 45;

        var site90 = GenerarConDefaults(input90);
        var site45 = GenerarConDefaults(input45);

        Assert.True(site45.EstacionamientoAutos.Count < site90.EstacionamientoAutos.Count,
            $"45° ({site45.EstacionamientoAutos.Count}) debería dar menos plazas que 90° ({site90.EstacionamientoAutos.Count}) en el mismo lote.");
        Assert.All(site45.EstacionamientoAutos, p => Assert.Equal(45, p.Angulo));
    }

    [Fact]
    public void Generate_CalculaMetricasDeTabulacion()
    {
        var site = GenerarConDefaults(EjemploDelDocumento());

        Assert.Equal(46.875, site.Metricas.CoberturaPct, precision: 3); // 25×60 / 40×80
        Assert.Equal(site.EstacionamientoAutos.Count, site.Metricas.AutosColocados);
        Assert.Equal(site.Muelles.Count, site.Metricas.MuellesColocados);
        Assert.Equal(12.0, site.Metricas.ProfundidadPatioM, precision: 3); // 80 - (8+60)
        Assert.True(site.Metricas.SuperficieEstacionamientoM2 > 0);
    }

    [Fact]
    public void Generate_PatioConEspacioSuficiente_NoAdvierteProfundidad()
    {
        var input = EjemploDelDocumento();
        input.Lote = new LoteInput { Frente = 40, Fondo = 120, Zona = "industrial-2" };
        input.Nave = new NaveInput { Largo = 60, Ancho = 25, AlturaLibre = 8 };

        var site = GenerarConDefaults(input);

        Assert.Equal(35.0, site.Metricas.ProfundidadPatioM, precision: 3); // objetivo default, hay 49 libres
        Assert.DoesNotContain(site.Advertencias, a => a.Contains("patio de maniobra"));
    }

    [Fact]
    public void Validate_RechazaRotacionInvalida()
    {
        var input = EjemploDelDocumento();
        input.Implantacion = new ImplantacionInput { RotacionGrados = 45 };

        var ex = Assert.Throws<ProjectInputValidationException>(() => ProjectInputLoader.Validate(input));
        Assert.Contains(ex.Errors, e => e.Contains("rotacion_grados"));
    }

    [Fact]
    public void Validate_RechazaImplantacionFueraDelLote()
    {
        var input = EjemploDelDocumento();
        input.Implantacion = new ImplantacionInput { XM = 20, YM = 12 }; // 20 + 25 > 40 de frente

        var ex = Assert.Throws<ProjectInputValidationException>(() => ProjectInputLoader.Validate(input));
        Assert.Contains(ex.Errors, e => e.Contains("implantacion.x_m"));
    }

    [Fact]
    public void Validate_RechazaAnguloDeEstacionamientoInvalido()
    {
        var input = EjemploDelDocumento();
        input.Logistica.AnguloEstacionamiento = 30;

        var ex = Assert.Throws<ProjectInputValidationException>(() => ProjectInputLoader.Validate(input));
        Assert.Contains(ex.Errors, e => e.Contains("angulo_estacionamiento"));
    }

    [Fact]
    public void Yaml_ImplantacionSobreviveElRoundTrip()
    {
        var input = EjemploDelDocumento();
        input.Implantacion = new ImplantacionInput { XM = 7.5, YM = 12, RotacionGrados = 90 };
        input.Lote = new LoteInput { Frente = 100, Fondo = 45, Zona = "industrial-2" };
        input.Logistica.AnguloEstacionamiento = 60;
        input.Logistica.MuellesEn = "fondo";

        var yaml = ProjectInputWriter.ToYaml(input);
        var leido = ProjectInputLoader.LoadFromYaml(yaml);

        Assert.Equal(7.5, leido.Implantacion.XM);
        Assert.Equal(12, leido.Implantacion.YM);
        Assert.Equal(90, leido.Implantacion.RotacionGrados);
        Assert.Equal(60, leido.Logistica.AnguloEstacionamiento);
        Assert.Equal("fondo", leido.Logistica.MuellesEn);
    }
}
