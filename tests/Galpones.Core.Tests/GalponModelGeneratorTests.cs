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
    public void Generate_CreaNivel0CubiertaYCumbrera()
    {
        var model = GalponModelGenerator.Generate(EjemploDelDocumento());

        Assert.Equal(3, model.Levels.Count);
        Assert.Contains(model.Levels, l => l.Nombre == "Nivel 0" && l.ElevacionMetros == 0.0);
        Assert.Contains(model.Levels, l => l.Nombre == "Cubierta" && l.ElevacionMetros == 8.0);
        // Cumbrera: 8 + (25/2) * 10% = 9.25
        Assert.Contains(model.Levels, l => l.Nombre == "Cumbrera" && Math.Abs(l.ElevacionMetros - 9.25) < 1e-9);
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

    [Fact]
    public void Generate_CreaDosColumnasPorEjeNumerado()
    {
        var model = GalponModelGenerator.Generate(EjemploDelDocumento());

        Assert.Equal(22, model.Columnas.Count); // 11 ejes x 2
        Assert.Equal(11, model.Columnas.Count(c => c.Y == 0.0));
        Assert.Equal(11, model.Columnas.Count(c => c.Y == 25.0));
        Assert.Contains(model.Columnas, c => c.X == 0.0 && c.Y == 0.0);
        Assert.Contains(model.Columnas, c => c.X == 60.0 && c.Y == 25.0);
    }

    [Fact]
    public void Generate_CreaDosCaballetesPorEjeNumeradoHaciaLaCumbrera()
    {
        var model = GalponModelGenerator.Generate(EjemploDelDocumento());

        Assert.Equal(22, model.Vigas.Count); // 11 ejes x 2
        Assert.All(model.Vigas, v => Assert.Equal(12.5, v.Fin.Y));
        Assert.All(model.Vigas, v => Assert.Equal(9.25, v.Fin.Z, precision: 9));
        Assert.Contains(model.Vigas, v =>
            v.Inicio == new Punto3D(0.0, 0.0, 8.0) && v.Fin == new Punto3D(0.0, 12.5, 9.25));
        Assert.Contains(model.Vigas, v =>
            v.Inicio == new Punto3D(0.0, 25.0, 8.0) && v.Fin == new Punto3D(0.0, 12.5, 9.25));
    }

    [Fact]
    public void Generate_SinPendienteEnElInput_AplicaElDefaultDe10Pct()
    {
        var model = GalponModelGenerator.Generate(EjemploDelDocumento());

        Assert.Equal(GalponModelGenerator.PendienteCubiertaDefaultPct, model.PendienteCubiertaPct);
    }

    [Fact]
    public void Generate_ConPendienteEnElInput_LaUsaParaLaCumbrera()
    {
        var input = EjemploDelDocumento();
        input.Estructura.PendienteCubiertaPct = 20;

        var model = GalponModelGenerator.Generate(input);

        Assert.Equal(20, model.PendienteCubiertaPct);
        // Cumbrera: 8 + 12.5 * 20% = 10.5
        Assert.Contains(model.Levels, l => l.Nombre == "Cumbrera" && Math.Abs(l.ElevacionMetros - 10.5) < 1e-9);
        Assert.All(model.Vigas, v => Assert.Equal(10.5, v.Fin.Z, precision: 9));
    }
}
