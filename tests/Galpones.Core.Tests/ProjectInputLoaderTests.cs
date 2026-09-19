using Galpones.Core.Input;

namespace Galpones.Core.Tests;

public class ProjectInputLoaderTests
{
    [Theory]
    [InlineData("pendiente_cubierta_pct: 0")]
    [InlineData("pendiente_cubierta_pct: -5")]
    [InlineData("pendiente_cubierta_pct: 150")]
    public void LoadFromYaml_RechazaPendienteDeCubiertaInvalida(string pendiente)
    {
        var yaml = $$"""
            tipologia: nave_deposito
            jurisdiccion: cordoba-capital
            lote: { frente: 40, fondo: 80, zona: "industrial-2" }
            nave: { largo: 60, ancho: 25, altura_libre: 8 }
            estructura: { tipo: porticos_metalicos, modulacion: 6, {{pendiente}} }
            piso: { sobrecarga_kN_m2: 30 }
            electrico: { potencia_kVA: 150, tension: trifasica }
            """;

        var ex = Assert.Throws<ProjectInputValidationException>(() => ProjectInputLoader.LoadFromYaml(yaml));

        Assert.Contains(ex.Errors, e => e.Contains("pendiente_cubierta_pct"));
    }

    [Fact]
    public void LoadFromFile_ParseaElEjemploDelDocumento()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "proyecto-ejemplo.yaml");

        var input = ProjectInputLoader.LoadFromFile(path);

        Assert.Equal("nave_deposito", input.Tipologia);
        Assert.Equal("cordoba-capital", input.Jurisdiccion);
        Assert.Equal(40, input.Lote.Frente);
        Assert.Equal(80, input.Lote.Fondo);
        Assert.Equal("industrial-2", input.Lote.Zona);
        Assert.Equal(60, input.Nave.Largo);
        Assert.Equal(25, input.Nave.Ancho);
        Assert.Equal(8, input.Nave.AlturaLibre);
        Assert.Equal("porticos_metalicos", input.Estructura.Tipo);
        Assert.Equal(6, input.Estructura.Modulacion);
        Assert.Equal(10, input.Estructura.PendienteCubiertaPct);
        Assert.Equal(30, input.Piso.SobrecargaKnM2);
        Assert.Equal(150, input.Electrico.PotenciaKva);
        Assert.Equal("trifasica", input.Electrico.Tension);
    }

    [Theory]
    [InlineData("nave: { largo: 0, ancho: 25, altura_libre: 8 }")]
    public void LoadFromYaml_RechazaDimensionesInvalidas(string naveOverride)
    {
        var yaml = $$"""
            tipologia: nave_deposito
            jurisdiccion: cordoba-capital
            lote: { frente: 40, fondo: 80, zona: "industrial-2" }
            {{naveOverride}}
            estructura: { tipo: porticos_metalicos, modulacion: 6 }
            piso: { sobrecarga_kN_m2: 30 }
            electrico: { potencia_kVA: 150, tension: trifasica }
            """;

        var ex = Assert.Throws<ProjectInputValidationException>(() => ProjectInputLoader.LoadFromYaml(yaml));

        Assert.Contains(ex.Errors, e => e.Contains("nave.largo"));
    }

    [Fact]
    public void LoadFromYaml_RechazaNaveMasAnchaQueElLote()
    {
        var yaml = """
            tipologia: nave_deposito
            jurisdiccion: cordoba-capital
            lote: { frente: 10, fondo: 80, zona: "industrial-2" }
            nave: { largo: 60, ancho: 25, altura_libre: 8 }
            estructura: { tipo: porticos_metalicos, modulacion: 6 }
            piso: { sobrecarga_kN_m2: 30 }
            electrico: { potencia_kVA: 150, tension: trifasica }
            """;

        var ex = Assert.Throws<ProjectInputValidationException>(() => ProjectInputLoader.LoadFromYaml(yaml));

        Assert.Contains(ex.Errors, e => e.Contains("nave.ancho"));
    }

    [Fact]
    public void LoadFromYaml_RechazaLaNaveSiNoEntraEnElLoteConLosRetirosDeSitio()
    {
        var yaml = """
            tipologia: nave_deposito
            jurisdiccion: cordoba-capital
            lote: { frente: 40, fondo: 80, zona: "industrial-2" }
            nave: { largo: 75, ancho: 25, altura_libre: 8 }
            estructura: { tipo: porticos_metalicos, modulacion: 6 }
            piso: { sobrecarga_kN_m2: 30 }
            electrico: { potencia_kVA: 150, tension: trifasica }
            logistica: { retiro_frente_m: 15 }
            """;

        var ex = Assert.Throws<ProjectInputValidationException>(() => ProjectInputLoader.LoadFromYaml(yaml));

        Assert.Contains(ex.Errors, e => e.Contains("logistica.retiro_frente_m"));
    }
}
