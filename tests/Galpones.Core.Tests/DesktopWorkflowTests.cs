using Galpones.Core.Input;
using Galpones.Core.IntermediateModel;
using Galpones.Core.Workflow;

namespace Galpones.Core.Tests;

public sealed class DesktopWorkflowTests
{
    private static ProjectInput Example() => new()
    {
        Tipologia = "nave_deposito", Jurisdiccion = "cordoba-capital",
        Lote = new() { Frente = 40, Fondo = 80, Zona = "K" },
        Nave = new() { Largo = 60, Ancho = 25, AlturaLibre = 8 },
        Estructura = new() { Tipo = "porticos_metalicos", Modulacion = 6, PendienteCubiertaPct = 12.5, PerfilColumna = "Acero: Perfil 300", PerfilViga = "Viga: Tipo A" },
        Piso = new() { SobrecargaKnM2 = 30 }, Electrico = new() { PotenciaKva = 150, Tension = "trifasica" }
    };

    [Fact]
    public void YamlRoundTrip_PreservesAliasesFamiliesAndEngineeringInputs()
    {
        var input = Example();
        var yaml = ProjectInputWriter.ToYaml(input);
        var restored = ProjectInputLoader.LoadFromYaml(yaml);
        Assert.Equal(input.Nave.AlturaLibre, restored.Nave.AlturaLibre);
        Assert.Equal(input.Estructura.PendienteCubiertaPct, restored.Estructura.PendienteCubiertaPct);
        Assert.Equal(input.Estructura.PerfilColumna, restored.Estructura.PerfilColumna);
        Assert.Equal(input.Estructura.PerfilViga, restored.Estructura.PerfilViga);
        Assert.Equal(input.Piso.SobrecargaKnM2, restored.Piso.SobrecargaKnM2);
        Assert.Equal(input.Electrico.PotenciaKva, restored.Electrico.PotenciaKva);
        Assert.Equal(input.Lote.Zona, restored.Lote.Zona);
        Assert.Equal(ProjectInputWriter.ToYaml(restored), yaml);
    }

    [Theory]
    [InlineData(60)] [InlineData(61)] [InlineData(62)] [InlineData(63)] [InlineData(65)]
    public void Generator_ClosesTheRequestedLengthAndNeverExceedsMaximumBay(double length)
    {
        var input = Example(); input.Nave.Largo = length;
        var model = GalponModelGenerator.Generate(input);
        var x = model.Grids.Where(g => g.Direction == GridDirection.Y).Select(g => g.PosicionMetros).ToArray();
        Assert.Equal(0, x[0]);
        Assert.Equal(length, x[^1]);
        Assert.Equal(x.Length, x.Distinct().Count());
        for (var i = 1; i < x.Length; i++) Assert.InRange(x[i] - x[i - 1], 0.000001, input.Estructura.Modulacion);
        Assert.Equal(2, model.Columnas.Count(c => c.X == length));
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void Generator_RejectsNonFiniteDimensions(double width)
    {
        var input = Example(); input.Nave.Ancho = width;
        Assert.Throws<ProjectInputValidationException>(() => GalponModelGenerator.Generate(input));
    }

    [Fact]
    public void Generator_RejectsUnboundedModuleCount()
    {
        var input = Example(); input.Estructura.Modulacion = 0.000001;
        Assert.Throws<ProjectInputValidationException>(() => GalponModelGenerator.Generate(input));
    }

    [Fact]
    public void Generator_DoesNotCreateMicroscopicBayFromFloatingPointRounding()
    {
        var input = Example(); input.Nave.Largo = 0.07; input.Estructura.Modulacion = 0.01;
        var x = GalponModelGenerator.Generate(input).Columnas.Select(c => c.X).Distinct().ToArray();
        Assert.Equal(8, x.Length);
        Assert.Equal(0.07, x[^1]);
    }

    [Fact]
    public void Loader_RejectsUnknownFieldsRatherThanDroppingThemOnSave()
    {
        var yaml = ProjectInputWriter.ToYaml(Example()) + "dato_desconocido: 123\n";
        Assert.Throws<YamlDotNet.Core.YamlException>(() => ProjectInputLoader.LoadFromYaml(yaml));
    }

    [Fact]
    public void Loader_ReportsExplicitlyNullSections()
    {
        var yaml = "tipologia: nave_deposito\njurisdiccion: cordoba-capital\nlote: null\n";
        Assert.Throws<ProjectInputValidationException>(() => ProjectInputLoader.LoadFromYaml(yaml));
    }

    [Fact]
    public void Review_ReportsMissingRulesAndUncalculatedStructure()
    {
        var input = Example();
        var review = ProjectReviewService.Evaluate(input, Path.Combine(AppContext.BaseDirectory, "missing-rules"));
        Assert.Contains(review.Items, i => i.Titulo == "Sin reglas para esta jurisdicción" && i.Estado == "PENDIENTE");
        Assert.Contains(review.Items, i => i.Titulo == "Cálculo estructural y altura útil" && i.Estado == "PENDIENTE");
        Assert.NotEmpty(review.Model.Columnas);
    }

    [Fact]
    public void Review_RejectsPathTraversalAsRulePackLocation()
    {
        var input = Example(); input.Jurisdiccion = "../cordoba-capital";
        var review = ProjectReviewService.Evaluate(input, AppContext.BaseDirectory);
        Assert.Contains(review.Items, i => i.Titulo == "Sin reglas para esta jurisdicción");
    }
}
