using Galpones.Core.Input;
using Galpones.Core.IntermediateModel;

namespace Galpones.Core.Tests;

public class GalponModelGeneratorTests
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

    [Fact]
    public void Generate_CreaNivel0YCubierta()
    {
        var model = GalponModelGenerator.Generate(EjemploDelDocumento());

        Assert.Equal(2, model.Levels.Count);
        Assert.Contains(model.Levels, l => l.Nombre == "Nivel 0" && l.ElevacionMetros == 0.0);
        Assert.Contains(model.Levels, l => l.Nombre == "Cubierta" && l.ElevacionMetros == 8.0);
    }

    [Fact]
    public void Generate_ConLargo60YModulacion6_Crea11EjesNumerados()
    {
        var model = GalponModelGenerator.Generate(EjemploDelDocumento());

        var ejesNumerados = model.Grids.Where(g => g.Direction == GridDirection.Y).ToList();

        Assert.Equal(11, ejesNumerados.Count); // 60 / 6 + 1
        Assert.Equal("1", ejesNumerados[0].Nombre);
        Assert.Equal(0.0, ejesNumerados[0].PosicionMetros);
        Assert.Equal("11", ejesNumerados[^1].Nombre);
        Assert.Equal(60.0, ejesNumerados[^1].PosicionMetros);
    }

    [Fact]
    public void Generate_CreaEjesALyBEnLosBordesDelAncho()
    {
        var model = GalponModelGenerator.Generate(EjemploDelDocumento());

        var ejesLetra = model.Grids.Where(g => g.Direction == GridDirection.X).ToList();

        Assert.Equal(2, ejesLetra.Count);
        Assert.Contains(ejesLetra, g => g.Nombre == "A" && g.PosicionMetros == 0.0);
        Assert.Contains(ejesLetra, g => g.Nombre == "B" && g.PosicionMetros == 25.0);
        Assert.All(ejesLetra, g => Assert.Equal(60.0, g.LongitudMetros));
    }
}
