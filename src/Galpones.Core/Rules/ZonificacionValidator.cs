using Galpones.Core.Input;

namespace Galpones.Core.Rules;

/// <summary>
/// Regla de tipo "restricción" (plugin-revit-galpones.md, sección 4): valida y marca
/// incumplimientos, no bloquea el modelado. La auditoría final queda a cargo del
/// arquitecto (sección 1, acuerdos posteriores).
/// </summary>
public sealed record IncumplimientoNormativo(string Regla, string Detalle, string Articulo);

public static class ZonificacionValidator
{
    public static IReadOnlyList<IncumplimientoNormativo> Validate(
        ProjectInput input,
        IReadOnlyDictionary<string, ZonaRule> rulePack)
    {
        if (!rulePack.TryGetValue(input.Lote.Zona, out var zona))
        {
            return
            [
                new IncumplimientoNormativo(
                    "zona_no_reconocida",
                    $"La zona '{input.Lote.Zona}' no está digitalizada en el pack de reglas de " +
                    $"'{input.Jurisdiccion}'. Confirmar el código real contra el Plano de Zonificación " +
                    "vigente (IDECOR) antes de continuar.",
                    "-"),
            ];
        }

        var incumplimientos = new List<IncumplimientoNormativo>();

        if (zona.AlturaMaxMetros is { } alturaMax && input.Nave.AlturaLibre > alturaMax)
        {
            incumplimientos.Add(new IncumplimientoNormativo(
                "altura_maxima",
                $"nave.altura_libre ({input.Nave.AlturaLibre:0.##} m) supera la altura máxima " +
                $"admitida en zona {zona.Zona} ({alturaMax:0.##} m).",
                zona.Articulo));
        }

        // F.O.S. = superficie cubierta / superficie de lote. Aproximación válida para el piloto
        // (nave de una sola planta, planta rectangular). El F.O.T. requiere sumar superficie por
        // nivel y todavía no está implementado en el core.
        var fosMax = zona.FosMaxParaFrente(input.Lote.Frente);
        if (fosMax is { } fosMaxValor)
        {
            var superficieLote = input.Lote.Frente * input.Lote.Fondo;
            var superficieCubierta = input.Nave.Largo * input.Nave.Ancho;
            var fos = superficieCubierta / superficieLote;

            if (fos > fosMaxValor)
            {
                incumplimientos.Add(new IncumplimientoNormativo(
                    "fos_maximo",
                    $"La superficie cubierta de la nave ({superficieCubierta:0.##} m²) sobre un lote de " +
                    $"{superficieLote:0.##} m² da un F.O.S. de {fos:P0}, que supera el máximo de zona " +
                    $"{zona.Zona} para ese frente de lote ({fosMaxValor:P0}).",
                    zona.Articulo));
            }
        }

        return incumplimientos;
    }
}
