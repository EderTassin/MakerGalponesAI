using System.Text.RegularExpressions;
using Galpones.Core.Input;
using Galpones.Core.IntermediateModel;
using Galpones.Core.Rules;
using Galpones.Core.Site;

namespace Galpones.Core.Workflow;

public sealed record ReviewItem(string Estado, string Titulo, string Detalle, string Fuente = "");
public sealed record ProjectReview(GalponModel Model, SiteLayoutModel SiteLayout, IReadOnlyList<ReviewItem> Items);

/// <summary>Comparte generación y revisión preliminar entre desktop y futuros adaptadores.</summary>
public static class ProjectReviewService
{
    public static ProjectReview Evaluate(ProjectInput input, string rulesDirectory)
    {
        var model = GalponModelGenerator.Generate(input);
        var siteLayout = SiteLayoutGenerator.Generate(input, model);
        var items = new List<ReviewItem>();
        foreach (var advertencia in siteLayout.Advertencias)
            items.Add(new("PENDIENTE", "Simulación de sitio", advertencia));
        // Una jurisdicción del archivo nunca debe convertirse en una ruta arbitraria.
        var validFolder = Regex.IsMatch(input.Jurisdiccion, "^[a-zA-Z0-9_-]+$");
        var rulePath = validFolder ? Path.Combine(rulesDirectory, input.Jurisdiccion, "zonificacion.yaml") : "";
        if (File.Exists(rulePath))
        {
            try
            {
                var pack = ZonificacionRulePackLoader.LoadFromFile(rulePath);
                foreach (var issue in ZonificacionValidator.Validate(input, pack))
                    items.Add(new("REVISAR", issue.Regla.Replace('_', ' '), issue.Detalle, issue.Articulo == "-" ? "" : issue.Articulo));
                items.Add(new("PARCIAL", "Revisión normativa preliminar",
                    (pack.ContainsKey(input.Lote.Zona)
                        ? "Se ejecutaron las comprobaciones disponibles de zona, FOS y altura libre. "
                        : "No se pudieron evaluar FOS ni altura porque falta una zona reconocida. ") +
                    "El pack es un borrador pendiente de aprobación. La altura total, los retiros, el FOT y el uso permitido requieren revisión profesional."));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or YamlDotNet.Core.YamlException or ArgumentException)
            {
                items.Add(new("PENDIENTE", "No se pudo leer el pack de reglas", ex.Message));
            }
        }
        else
            items.Add(new("PENDIENTE", "Sin reglas para esta jurisdicción", "El modelo se puede generar, pero no se ejecutaron comprobaciones normativas para esta ubicación."));

        var remainder = input.Nave.Largo % input.Estructura.Modulacion;
        if (remainder > 0.000001 && input.Estructura.Modulacion - remainder > 0.000001)
            items.Add(new("DATO", "Último módulo ajustado", $"El último tramo mide {remainder:0.###} m para cerrar exactamente los {input.Nave.Largo:0.###} m de largo."));
        items.Add(new("PENDIENTE", "Cálculo estructural y altura útil",
            "Se genera la disposición geométrica de pórticos. Las cargas no dimensionan perfiles ni piso. Confirmar altura libre real descontando vigas, cubierta e instalaciones."));
        if (string.IsNullOrWhiteSpace(input.Estructura.PerfilColumna) || string.IsNullOrWhiteSpace(input.Estructura.PerfilViga))
            items.Add(new("PENDIENTE", "Selección de familias", "Elegí las familias de columna y viga en Revit antes de generar. Los nombres escritos deben existir en el documento de destino."));
        items.Add(new("PENDIENTE", "Desarrollo y documentación", "Cubierta física, cerramientos, piso, aberturas, instalaciones y planos todavía no se generan en esta versión."));
        return new(model, siteLayout, items);
    }
}
