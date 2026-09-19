using Galpones.Core.Input;

namespace Galpones.Core.IntermediateModel;

/// <summary>
/// Primera derivación del piloto: a partir del input, genera niveles y grilla estructural
/// de una nave rectangular con pórticos modulados. No aplica reglas normativas (sección 3 del doc).
/// </summary>
public static class GalponModelGenerator
{
    public const double PendienteCubiertaDefaultPct = 10.0;

    public static GalponModel Generate(ProjectInput input)
    {
        ProjectInputLoader.Validate(input);
        var pendientePct = input.Estructura.PendienteCubiertaPct ?? PendienteCubiertaDefaultPct;
        var alturaLibre = input.Nave.AlturaLibre;
        var semiluz = input.Nave.Ancho / 2.0;
        var zCumbrera = alturaLibre + semiluz * pendientePct / 100.0;

        var levels = new List<LevelDef>
        {
            new("Nivel 0", 0.0),
            new("Cubierta", alturaLibre),
            new("Cumbrera", zCumbrera),
        };

        var grids = new List<GridDef>();
        var columnas = new List<ColumnaDef>();
        var vigas = new List<VigaDef>();

        // Ejes numerados (1, 2, 3...): perpendiculares al largo, cada `modulacion` metros.
        // El último tramo puede ser menor: siempre cerrar la nave en su largo solicitado.
        var ratio = input.Nave.Largo / input.Estructura.Modulacion;
        var enteroCercano = Math.Round(ratio);
        var cantidadModulos = (int)(Math.Abs(ratio - enteroCercano) < 1e-9 ? enteroCercano : Math.Ceiling(ratio));
        for (var i = 0; i <= cantidadModulos; i++)
        {
            var posicionX = i == cantidadModulos ? input.Nave.Largo : i * input.Estructura.Modulacion;
            grids.Add(new GridDef((i + 1).ToString(), GridDirection.Y, posicionX, input.Nave.Ancho));

            // Pórtico en cada eje: columna en cada borde y dos caballetes hacia la cumbrera.
            columnas.Add(new ColumnaDef(posicionX, 0.0));
            columnas.Add(new ColumnaDef(posicionX, input.Nave.Ancho));
            vigas.Add(new VigaDef(
                new Punto3D(posicionX, 0.0, alturaLibre),
                new Punto3D(posicionX, semiluz, zCumbrera)));
            vigas.Add(new VigaDef(
                new Punto3D(posicionX, input.Nave.Ancho, alturaLibre),
                new Punto3D(posicionX, semiluz, zCumbrera)));
        }

        // Ejes con letra (A, B): los dos bordes longitudinales de la nave.
        grids.Add(new GridDef("A", GridDirection.X, 0.0, input.Nave.Largo));
        grids.Add(new GridDef("B", GridDirection.X, input.Nave.Ancho, input.Nave.Largo));

        return new GalponModel
        {
            Levels = levels,
            Grids = grids,
            Columnas = columnas,
            Vigas = vigas,
            PendienteCubiertaPct = pendientePct,
            PerfilColumna = input.Estructura.PerfilColumna,
            PerfilViga = input.Estructura.PerfilViga,
        };
    }
}
