using Galpones.Core.Input;

namespace Galpones.Core.IntermediateModel;

/// <summary>
/// Primera derivación del piloto: a partir del input, genera niveles y grilla estructural
/// de una nave rectangular con pórticos modulados. No aplica reglas normativas (sección 3 del doc).
/// </summary>
public static class GalponModelGenerator
{
    public static GalponModel Generate(ProjectInput input)
    {
        var levels = new List<LevelDef>
        {
            new("Nivel 0", 0.0),
            new("Cubierta", input.Nave.AlturaLibre),
        };

        var grids = new List<GridDef>();

        // Ejes numerados (1, 2, 3...): perpendiculares al largo, cada `modulacion` metros.
        var cantidadModulos = (int)Math.Round(input.Nave.Largo / input.Estructura.Modulacion, MidpointRounding.AwayFromZero);
        for (var i = 0; i <= cantidadModulos; i++)
        {
            var posicionX = Math.Min(i * input.Estructura.Modulacion, input.Nave.Largo);
            grids.Add(new GridDef((i + 1).ToString(), GridDirection.Y, posicionX, input.Nave.Ancho));
        }

        // Ejes con letra (A, B): los dos bordes longitudinales de la nave.
        grids.Add(new GridDef("A", GridDirection.X, 0.0, input.Nave.Largo));
        grids.Add(new GridDef("B", GridDirection.X, input.Nave.Ancho, input.Nave.Largo));

        return new GalponModel
        {
            Levels = levels,
            Grids = grids,
        };
    }
}
