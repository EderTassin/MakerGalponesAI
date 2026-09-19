using YamlDotNet.Serialization;

namespace Galpones.Core.Rules;

/// <summary>
/// Carga un pack de reglas de zonificación (reglas/&lt;jurisdiccion&gt;/zonificacion.yaml).
/// No interpreta la ordenanza en tiempo de ejecución: solo lee reglas ya traducidas y
/// versionadas por un humano (plugin-revit-galpones.md, sección 3).
/// </summary>
public static class ZonificacionRulePackLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithCaseInsensitivePropertyMatching()
        .IgnoreUnmatchedProperties()
        .Build();

    public static IReadOnlyDictionary<string, ZonaRule> LoadFromFile(string path)
    {
        using var reader = new StreamReader(path);
        return LoadFromTextReader(reader);
    }

    private static IReadOnlyDictionary<string, ZonaRule> LoadFromTextReader(TextReader reader)
    {
        var raw = Deserializer.Deserialize<RulePackYaml>(reader) ?? new RulePackYaml();

        return raw.Zonas
            .Select(z => new ZonaRule(
                z.Zona,
                z.Caracter,
                z.FosMax,
                z.FosMaxFrenteMenor25M,
                z.FosMaxFrenteMayorIgual25M,
                z.FotMax,
                z.AlturaMaxM,
                z.Retiros,
                z.Articulo))
            .ToDictionary(r => r.Zona, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class RulePackYaml
    {
        public List<ZonaRuleYaml> Zonas { get; set; } = [];
    }

    private sealed class ZonaRuleYaml
    {
        public string Zona { get; set; } = string.Empty;
        public string Caracter { get; set; } = string.Empty;

        [YamlMember(Alias = "fos_max", ApplyNamingConventions = false)]
        public double? FosMax { get; set; }

        [YamlMember(Alias = "fos_max_frente_menor_25m", ApplyNamingConventions = false)]
        public double? FosMaxFrenteMenor25M { get; set; }

        [YamlMember(Alias = "fos_max_frente_mayor_igual_25m", ApplyNamingConventions = false)]
        public double? FosMaxFrenteMayorIgual25M { get; set; }

        [YamlMember(Alias = "fot_max", ApplyNamingConventions = false)]
        public double? FotMax { get; set; }

        [YamlMember(Alias = "altura_max_m", ApplyNamingConventions = false)]
        public double? AlturaMaxM { get; set; }

        public string Retiros { get; set; } = string.Empty;
        public string Articulo { get; set; } = string.Empty;
    }
}
